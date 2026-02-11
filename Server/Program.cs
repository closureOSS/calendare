// #define CLIENTCERT_AUTH

using System;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Calendare.Data;
using Calendare.Server.Api;
using Calendare.Server.Calendar;
using Calendare.Server.Middleware;
using Calendare.Server.Migrations;
using Calendare.Server.Options;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Webpush;
using ClosureOSS.JwtBearer;
using idunno.Authentication.Basic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
#if CLIENTCERT_AUTH
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
#endif

Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                );

#if CLIENTCERT_AUTH
    // https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth?view=aspnetcore-10.0
    builder.Services.Configure<KestrelServerOptions>(options =>
    {
        options.ConfigureHttpsDefaults(options =>
            options.ClientCertificateMode = ClientCertificateMode.AllowCertificate);
    });
    builder.Services.AddCertificateForwarding(options =>
    {
        options.CertificateHeader = "ssl-client-cert";
        options.HeaderConverter = (headerValue) =>
        {
            X509Certificate2? clientCertificate = null;

            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                clientCertificate = X509Certificate2.CreateFromPem(WebUtility.UrlDecode(headerValue));
            }

            return clientCertificate!;
        };
    });
#endif

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // options.SerializerOptions.WriteIndented = true;
        // options.SerializerOptions.IncludeFields = true;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // options.SerializerOptions.TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? new DefaultJsonTypeInfoResolver() : CalendareSerializerContext.Default;
    });

    builder.Services.AddCalendareNpgsql(builder.Configuration.GetSection("Postgresql"));
    // builder.Services.AddDatabaseDeveloperPageExceptionFilter();
    builder.Services.AddScoped<IMigrationRepository, MigrationRepository>();
    builder.Services.AddHostedService<MigrationWorker>();
    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.MapType<Instant>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" });
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Calendare Server",
            Version = ThisAssembly.AssemblyFileVersion,
            Description = "Administration API for the Calendare Server",
            Contact = new OpenApiContact
            {
                Url = new Uri("https://github.com/closureOSS/calendare"),
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://mit-license.org/"),
            },
        });
        // c.AddOperationFilterInstance
        c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme.",
        });
        c.AddSecurityRequirement((doc) => new OpenApiSecurityRequirement()
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, doc)] = [],
        });
    });

    builder.Services.AddRequestDecompression();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy",
            builder => builder
                    .SetIsOriginAllowed(origin => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromSeconds(2520.0)
                    ));
    });

    builder.Services.Configure<CalendareOptions>(builder.Configuration.GetSection("Calendare"));
    builder.Services.Configure<UserDefaultOptions>(builder.Configuration.GetSection("Calendare:UserDefaults"));
    builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection("Calendare:Administrator"));
    builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("WebPush:Vapid"));
    builder.Services.AddCaldav();

    builder.Services.AddVSyntaxReaderExtended(TimezoneResolvers.Static);

    builder.Services.Configure<JwtBearerProviderOptions>(builder.Configuration.GetSection("JwtBearer"));
    builder.Services.ConfigureJwtBearerProvider();

    builder.Services
        .AddAuthentication(opts =>
        {
            opts.DefaultScheme = "JWT_OR_BASICAUTH";
            // opts.DefaultScheme = BasicAuthenticationDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = "JWT_OR_BASICAUTH";
        })
        .AddBasic(opts =>
        {
            opts.AllowInsecureProtocol = true;  // The connection between ingress and pod is unsecured, should be false for an terminating connection
            opts.Realm = "Calendare";
            opts.Events = new BasicAuthenticationEvents
            {
                OnValidateCredentials = async context =>
                {
                    var userRepo = context.HttpContext.RequestServices.GetRequiredService<UserRepository>();
                    var claims = await userRepo.VerifyAsync(context.Username, context.Password, context.Scheme.Name, context.HttpContext.RequestAborted);
                    if (claims is not null)
                    {
                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                        context.Success();
                    }
                    else
                    {
                        context.Fail("invalid credentials");
                    }
                },
            };
        })
        .AddJwtBearer()
#if CLIENTCERT_AUTH
        .AddCertificate(opts =>
        {
            opts.Events = new Microsoft.AspNetCore.Authentication.Certificate.CertificateAuthenticationEvents
            {
                OnCertificateValidated = context =>
                {
                    Log.Information("Certificate validation triggered");
                    return Task.CompletedTask;
                }
            };
        })
#endif
        .AddPolicyScheme("JWT_OR_BASICAUTH", "JWT_OR_BASICAUTH", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                // filter by auth type
                string? authorization = context.Request.Headers[HeaderNames.Authorization];
                if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.Ordinal))
                    return JwtBearerDefaults.AuthenticationScheme;
#if CLIENTCERT_AUTH
                var hasCertificate = context.Connection.ClientCertificate is not null;
                if (hasCertificate) return CertificateAuthenticationDefaults.AuthenticationScheme;
#endif
                return BasicAuthenticationDefaults.AuthenticationScheme;
            };
        })
        ;
    builder.Services.AddAuthorization();

    builder.Services.Configure<RecorderOptions>(builder.Configuration.GetSection("Recorder"));
    // builder.Services.AddHttpLogging(o =>
    // {
    //     // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0
    //     o.CombineLogs = true;
    //     o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    //     o.RequestBodyLogLimit = 4096;
    //     o.ResponseBodyLogLimit = 4096;
    // });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapSwagger("/openapi/{documentName}/openapi.{extension:regex(^(json|ya?ml)$)}", options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
        });
    }
    app.MapHealthChecks("/health").RequireHost("*:5001");
    // app.UseHttpLogging();
    app.UseCors("CorsPolicy");
    app.UseCertificateForwarding();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRequestDecompression();
    app.UseCaldav();
    app.MapAdministration();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Calendare terminated unexpectedly");
    return;
}
finally
{
    await Log.CloseAndFlushAsync();
}
