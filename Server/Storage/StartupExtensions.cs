using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Storage;

public static class StartupExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Provider") ?? throw new InvalidOperationException("Provider config is missing.");
        switch (provider.ToLowerInvariant())
        {
            case "filesystem":
                services.Configure<FileStorageOptions>(configuration.GetSection("Filesystem"));
                services.AddScoped<IDavStorage, FileStorage>();
                break;

            case "s3":
                services.Configure<S3StorageOptions>(configuration.GetSection("S3"));
                services.AddScoped<IDavStorage, S3Storage>();
                break;

            case "none":
            default:
                break;
        }
        // services.ConfigureOptions<StorageOptions>();
        return services;
    }
}
