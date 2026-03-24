using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Webpush;

public static class StartupExtensions
{
    public static IServiceCollection AddWebPush(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(WebpushWorker)).AddStandardResilienceHandler();
        return services;
    }
}
