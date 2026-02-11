using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Data;
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

        api.MapDelete("/site", async (DavEnvironmentRepository env, SiteRepository siteRepository, HttpContext context) =>
        {
            if (env.IsTestMode != true)
            {
                return Results.Unauthorized();
            }
            var cnt = await siteRepository.DeleteAllAsync(context.RequestAborted);
            return Results.Ok();
        })
        .WithName("DeleteWholeSite")
        .RequireAuthorization()
        .WithSummary("Delete all data of site")
        .WithDescription("Removes all data of the site; can only be used in TEST mode")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        ;

        api.MapDelete("/site/trxjournal", async (SiteRepository siteRepository, HttpContext context) =>
        {
            // TODO: add cut off time to prune transaction log
            var cnt = await siteRepository.DeleteTrxJournal(context.RequestAborted);
            return Results.Ok();
        })
        .WithName("DeleteTrxJournal")
        .RequireAuthorization()
        .WithSummary("Deletes transaction journal")
        .WithDescription("Deletes transaction journal")
        ;

        api.MapGet("/sync", async Task<Results<Ok<SyncTokenResponse>, NotFound, BadRequest<ProblemDetails>>> (
            [FromQuery(Name = "collection"), Required] string collectionUri, ItemRepository itemRepository, HttpContext context) =>
        {
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
        .WithDescription("Gets the latest sync token for a collection, only for test automation")
        .WithTags(["Testing"])
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        return api;
    }
}
