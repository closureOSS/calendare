using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
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
            => exceptionHandlerApp.Run(async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionHandlerFeature?.Error;

                // 2. Default to 500, but check if the exception specifies its own status code
                int statusCode = StatusCodes.Status500InternalServerError;

                if (exception is BadHttpRequestException badRequestEx)
                {
                    statusCode = badRequestEx.StatusCode; // This will capture the 400
                }
                await Results.Problem(statusCode: statusCode).ExecuteAsync(context);
            }));

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
