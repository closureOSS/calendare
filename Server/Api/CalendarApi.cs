using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calendare.Server.Api.Models;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Calendare.Server.Api;

public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapCalendarApi(this RouteGroupBuilder api)
    {
        api.MapGet("/uid/{uid}", async Task<Results<Ok<List<CalendarScheduleItem>>, NotFound, BadRequest<ProblemDetails>>> (string uid, ResourceRepository resourceRepository, ItemRepository itemRepository, HttpContext context) =>
        {
            var resource = await resourceRepository.GetResourceAsync(new CaldavUri(uid ?? ""), context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.BadRequest(new ProblemDetails { Title = $"Uid {uid} not found" });
            }
            if (!resource.Exists)
            {
                return TypedResults.NotFound();
            }
            switch (resource.ResourceType)
            {
                case DavResourceType.Calendar:
                    var journal = await itemRepository.ListCollectionObjectsAsync(resource.Current!.Id, Guid.Empty, context.RequestAborted);
                    return TypedResults.Ok(journal.ToView());

                case DavResourceType.CalendarItem:
                    return TypedResults.Ok(new List<CalendarScheduleItem>() { resource.Object!.ToView() });

                default:
                    return TypedResults.BadRequest(new ProblemDetails { Title = $"Resource {resource.ResourceType} has not calender data" });
            }
        })
        .WithName("GetCalendarByUid")
        .RequireAuthorization()
        .WithSummary("Get calendar entries by uid")
        .WithDescription("Returns calendar entries")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;


        api.MapGet("/uri", async Task<Results<Ok<List<CalendarScheduleItem>>, NotFound, BadRequest<ProblemDetails>>> (
            [FromQuery(Name = "path")] string? path,
            ResourceRepository resourceRepository, ItemRepository itemRepository, HttpContext context) =>
        {
            path = string.IsNullOrEmpty(path) ? "/" : $"/{path}";
            var resource = await resourceRepository.GetResourceAsync(new CaldavUri(path ?? ""), context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.BadRequest(new ProblemDetails { Title = $"Resource {path} not found" });
            }
            if (!resource.Exists)
            {
                return TypedResults.NotFound();
            }
            switch (resource.ResourceType)
            {
                case DavResourceType.Calendar:
                    var journal = await itemRepository.ListCollectionObjectsAsync(resource.Current!.Id, Guid.Empty, context.RequestAborted);
                    return TypedResults.Ok(journal.ToView());

                case DavResourceType.CalendarItem:
                    return TypedResults.Ok(new List<CalendarScheduleItem>() { resource.Object!.ToView() });

                default:
                    return TypedResults.BadRequest(new ProblemDetails { Title = $"Resource {resource.ResourceType} has not calender data" });

            }
        })
        .WithName("GetCalendar")
        .RequireAuthorization()
        .WithSummary("Get calendar entries")
        .WithDescription("Returns calendar entries")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        return api;
    }
}
