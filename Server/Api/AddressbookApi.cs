using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Server.Api.Models;
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
    public static RouteGroupBuilder MapAddressbookApi(this RouteGroupBuilder app)
    {
        app.MapGet("/uri", async Task<Results<Ok<List<AddressbookItem>>, NotFound, BadRequest<ProblemDetails>>> (
            [FromQuery(Name = "collection"), Required] string? path,
            ResourceRepository resourceRepository, ItemRepository itemRepository, HttpContext context) =>
        {
            path = string.IsNullOrEmpty(path) ? "/" : $"/{path}";
            var resource = await resourceRepository.GetResourceAsync(new Middleware.CaldavUri(path ?? ""), context, context.RequestAborted);
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
                case DavResourceType.Addressbook:
                    var journal = await itemRepository.ListCollectionObjectsAsync(resource.Current!.Id, Guid.Empty, context.RequestAborted);
                    return TypedResults.Ok(journal.ToAddressbookView());

                case DavResourceType.AddressbookItem:
                    return TypedResults.Ok(new List<AddressbookItem>() { resource.Object!.ToAddressbookView() });

                default:
                    return TypedResults.BadRequest(new ProblemDetails { Title = $"Resource {resource.ResourceType} has not calender data" });
            }
        })
        .WithName("GetAddressbook")
        .RequireAuthorization()
        .WithSummary("Get addressbook entries")
        .WithDescription("Returns addressbook entries")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        return app;
    }
}
