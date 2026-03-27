using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Migrations;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapSiteApi(this RouteGroupBuilder api)
    {
        api.MapGet("/version", async (DavEnvironmentRepository env, CalendareContext dbms, IMigrationRepository migr, HttpContext context) =>
        {
            var response = new FeatureResponse
            {
                Version = ThisAssembly.AssemblyInformationalVersion,
                PathBase = env.PathBase,
            };
            foreach (var feature in Enum.GetValues<CalendareFeatures>())
            {
                response.Features.Add(feature);
            }
            foreach (var fs in env.GetFeatureSets())
            {
                var featureList = new FeatureByClient { ClientType = fs };
                foreach (var feature in env.ResolveFeatures(fs))
                {
                    featureList.Enabled.Add(feature);
                }
                response.FeaturesEnabled.Add(featureList);
            }
            response.DbmsSchema = [.. await dbms.Database.GetAppliedMigrationsAsync(context.RequestAborted)];
            response.DbmsDataMigrations = [.. await migr.GetAppliedMigrationsAsync(context.RequestAborted)];
            return TypedResults.Ok(response);
        })
        .WithName("GetVersion")
        .AllowAnonymous()
        .WithSummary("Get Calendare version")
        .WithDescription("Get version and features by calender client information")
        ;

        api.MapGet("/site/ping", (HttpContext context) =>
        {
            return TypedResults.Ok();
        })
        .WithName("Ping")
        .RequireAuthorization()
        .WithSummary("Verify state of connection and authentication")
        ;

        api.MapDelete("/site", async Task<Results<Ok, UnauthorizedHttpResult>> (DavEnvironmentRepository env, SiteRepository siteRepository, UserRepository userRepository, HttpContext context) =>
        {
            if (env.IsTestMode != true)
            {
                return TypedResults.Unauthorized();
            }
            var currentUserPrincipal = await TryGetAdministrator(userRepository, context.User.Identity, PrivilegeMask.AdminSysOps, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.Unauthorized();
            }
            var cnt = await siteRepository.DeleteAllAsync(resetInstallation: false, context.RequestAborted);
            return TypedResults.Ok();
        })
        .WithName("DeleteWholeSite")
        .RequireAuthorization()
        .WithSummary("Delete all data of site")
        .WithDescription("Removes all data of the site; can only be used in TEST mode")
        .WithTags(["Testing"])
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        ;

        api.MapDelete("/site/trxjournal", async Task<Results<Ok, UnauthorizedHttpResult>> (SiteRepository siteRepository, UserRepository userRepository, HttpContext context) =>
        {
            var currentUserPrincipal = await TryGetAdministrator(userRepository, context.User.Identity, PrivilegeMask.AdminSysOps, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.Unauthorized();
            }
            // TODO: add cut off time to prune transaction log
            var cnt = await siteRepository.DeleteTrxJournal(context.RequestAborted);
            return TypedResults.Ok();
        })
        .WithName("DeleteTrxJournal")
        .RequireAuthorization()
        .WithSummary("Deletes transaction journal")
        .WithTags(["Operation"])
        .WithDescription("Deletes transaction journal")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        ;

        api.MapGet("/sync", async Task<Results<Ok<SyncTokenResponse>, NotFound, UnauthorizedHttpResult, BadRequest<ProblemDetails>>> (
            [FromQuery(Name = "collection"), Required] string collectionUri, DavEnvironmentRepository env, ItemRepository itemRepository, HttpContext context) =>
        {
            if (env.IsTestMode != true)
            {
                return TypedResults.Unauthorized();
            }
            var token = await itemRepository.GetLatestSyncToken($"/{collectionUri}", context.RequestAborted);
            if (token is not null && token.Id > Guid.Empty)
            {
                return TypedResults.Ok(new SyncTokenResponse { Token = token.Uri });
            }
            return TypedResults.NotFound();
        })
        .WithName("GetLatestSyncToken")
        .RequireAuthorization()
        .WithSummary("Gets the latest sync token for a collection")
        .WithDescription("Gets the latest sync token for a collection; can only be used in TEST mode")
        .WithTags(["Testing"])
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        ;

        return api;
    }
}
