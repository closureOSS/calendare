using Microsoft.AspNetCore.Routing;

namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapStatisticsApi(this RouteGroupBuilder api)
    {
        // TODO: [Medium] Restrict select for normal user to Read privilege access, for admin as-is.
        // api.MapGet("/collections", async (SiteStatisticsRepository statisticsRepository, HttpContext context) =>
        // {
        //     return TypedResults.Ok(await statisticsRepository.ComputeCollectionStatistics(context.RequestAborted));
        // })
        // .WithName("GetStatisticCollection")
        // .RequireAuthorization()
        // .WithSummary("Get statistics about collections")
        // ;

        return api;
    }
}
