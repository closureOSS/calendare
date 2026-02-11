using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Api;

public static class StartupExtensions
{
    public static void MapAdministration(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
            await Results.Problem(statusCode: statusCodeContext.HttpContext.Response.StatusCode)
            .ExecuteAsync(statusCodeContext.HttpContext));

        app.UseExceptionHandler(exceptionHandlerApp
            => exceptionHandlerApp.Run(async context
                => await Results.Problem().ExecuteAsync(context)));

        var api = app.MapGroup("/api")
            .ProducesProblem(StatusCodes.Status500InternalServerError, AdministrationApi.ApplicationProblemJson)
            .ProducesProblem(StatusCodes.Status400BadRequest, AdministrationApi.ApplicationProblemJson)
            .WithTags(["Calendare"]);
        api.MapSiteApi();
        api.MapGroup("/site/statistics").MapStatisticsApi();
        api.MapGroup("/user").MapUserApi();
        api.MapGroup("/credentials").MapCredentialApi();
        api.MapGroup("/permission").MapPermissionApi();
        api.MapGroup("/privilege").MapPrivilegeApi();
        api.MapGroup("/membership").MapMembershipApi().MapProxyMembershipApi();
        api.MapGroup("/collection").MapCollectionApi();
        api.MapGroup("/object").MapObjectCollectionApi();
        api.MapGroup("/calendar").MapCalendarApi();
        api.MapGroup("/addressbook").MapAddressbookApi();
        api.MapGroup("/mailbox").MapMailboxApi();
    }
}
