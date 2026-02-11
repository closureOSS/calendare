using System;
using Calendare.Data.Models;
using Calendare.Data.Options;
using Calendare.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Calendare.Data;

public static class StartupExtensions
{
    public static IServiceCollection AddCalendareNpgsql(this IServiceCollection services, IConfigurationSection configuration)
    {
        services.AddDbContextPool<CalendareContext>(opt => opt.ConfigureCalendareNpgsql(configuration));
        return services;
    }

    public static DbContextOptionsBuilder ConfigureCalendareNpgsql(this DbContextOptionsBuilder builder, IConfigurationSection configuration)
    {
        return builder.UseNpgsql(
            configuration.GetConnectionStringPostgresOptions(),
            options => options
                .MapEnum<CollectionType>()
                .MapEnum<CollectionSubType>()
                .UseNodaTime()
            )
            .AddInterceptors(new SoftDeleteInterceptor())
            .EnableDetailedErrors()
#if DEBUG
            .EnableSensitiveDataLogging()
            // .ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
#endif
            .UseSnakeCaseNamingConvention();
    }

    private static string GetConnectionStringPostgresOptions(this IConfigurationSection configuration)
    {
        var options = configuration.Get<PostgresOptions>() ?? new PostgresOptions { ConnectionString = "Host=calendare-cluster-rw;Username=app;Database=app;" };

        var csb = new NpgsqlConnectionStringBuilder(options.ConnectionString)
        {
            ApplicationName = "Calendare",
        };
        if (!string.IsNullOrEmpty(options.User)) csb.Username = options.User ?? "app";
        if (!string.IsNullOrEmpty(options.Password)) csb.Password = options.Password;
        if (!string.IsNullOrEmpty(options.Host)) csb.Host = options.Host;
        if (options.Port is not null) csb.Port = options.Port.Value;
        if (!string.IsNullOrEmpty(options.Dbname)) csb.Database = options.Dbname ?? "app";
        return csb.ConnectionString;
    }
}
