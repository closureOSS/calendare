using System.Collections.Generic;
using System.Threading.Tasks;
using Calendare.Server.Api.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
namespace Calendare.Server.Api;

public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapObjectCollectionApi(this RouteGroupBuilder api)
    {

        api.MapGet("/id/calendar/{id:int}", async Task<Results<Ok<CalendarScheduleItem>, NotFound>> (int id, ItemRepository itemRepository, HttpContext context) =>
        {
            var collection = await itemRepository.ListCollectionObjectsByIdAsync(id, context.RequestAborted);
            return collection is not null ? TypedResults.Ok(collection.ToView()) : TypedResults.NotFound();
        })
        .WithName("GetObjectById")
        .RequireAuthorization()
        .WithSummary("Get object by Id (Debugging)")
        .WithDescription("Returns object or not found.")
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        api.MapGet("/uid/calendar/{uid}", async Task<Results<Ok<List<CalendarScheduleItem>>, NotFound>> (string uid, ItemRepository itemRepository, HttpContext context) =>
        {
            var collection = await itemRepository.ListCollectionObjectsByUidAsync(uid, context.RequestAborted);
            return collection is not null ? TypedResults.Ok(collection.ToView()) : TypedResults.NotFound();
        })
        .WithName("GetObjectsByUid")
        .RequireAuthorization()
        .WithSummary("Get objects by Uid (Debugging)")
        .WithDescription("Returns objects.")
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        return api;
    }
}
