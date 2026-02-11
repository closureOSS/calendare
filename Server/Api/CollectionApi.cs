using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapCollectionApi(this RouteGroupBuilder api)
    {
        api.MapGet("/{username}", async Task<Results<Ok<List<CollectionResponse>>, ForbidHttpResult, BadRequest>> (string username, UserRepository userRepository, CollectionRepository collectionRepository, HttpContext context) =>
        {
            var (principal, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.Read, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.BadRequest();
            }
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var collections = await collectionRepository.ListByOwnerAsync(new()
            {
                CurrentUser = currentUserPrincipal,
                OwnerUsername = principal.Username,
            }, context.RequestAborted);
            PrivilegeScope scope = currentUserPrincipal.UserId == StockPrincipal.Admin ? PrivilegeScope.Admin : (currentUserPrincipal.Id == principal.Id ? PrivilegeScope.Owner : PrivilegeScope.Authenticated);
            var grants = scope == PrivilegeScope.Authenticated ? await collectionRepository.ListPrivilegesAsync(currentUserPrincipal.Id, context.RequestAborted) : [];
            return TypedResults.Ok(collections.ToView(scope, grants));
        })
        .WithName("GetCollectionByOwner")
        .RequireAuthorization()
        .WithSummary("Get user/principal collections")
        .WithDescription("Returns principal user collections.")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        api.MapGet("/", async Task<Results<Ok<CollectionResponse>, NotFound, ForbidHttpResult, BadRequest>> (
             CollectionRepository collectionRepository, ResourceRepository resourceRepository, HttpContext context,
          [FromQuery(Name = "uri"), Required] string uri) =>
        {
            var (collection, resource) = await TryGetAuthorizedCollection(resourceRepository, collectionRepository, uri, PrivilegeMask.Read | PrivilegeMask.ReadAcl | PrivilegeMask.Write, context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.Forbid();
            }
            if (collection is null)
            {
                return TypedResults.NotFound();
            }
            return TypedResults.Ok(collection.ToView());
        })
        .WithName("GetCollectionByUri")
        .RequireAuthorization()
        .WithSummary("Get collection")
        .WithDescription("Returns collection.")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPut("/", async Task<Results<Ok, ForbidHttpResult, NotFound, BadRequest>> (
            CollectionRepository collectionRepository, ResourceRepository resourceRepository, HttpContext context,
            [FromQuery(Name = "uri"), Required] string uri, [FromBody] CollectionAmendRequest request
            ) =>
        {
            var (collection, resource) = await TryGetAuthorizedCollection(resourceRepository, collectionRepository, uri, PrivilegeMask.WriteProperties, context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.Forbid();
            }
            if (collection is null)
            {
                return TypedResults.NotFound();
            }
            // TODO: If uri in body and query param differ, it's a move. Check if we should support that?!
            var success = await collectionRepository.StoreAsync(collection, request, context.RequestAborted);
            return success ? TypedResults.Ok() : TypedResults.BadRequest();
        })
        .WithName("AmendCollectionByUri")
        .RequireAuthorization()
        .WithSummary("Update some collection meta data")
        .WithDescription("Returns collection.")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapDelete("/", async Task<Results<Ok, ForbidHttpResult, BadRequest>> (
            CollectionRepository collectionRepository, ResourceRepository resourceRepository, HttpContext context,
            [FromQuery(Name = "uri"), Required] string uri
            ) =>
        {
            var (collection, resource) = await TryGetAuthorizedCollection(resourceRepository, collectionRepository, uri, PrivilegeMask.Unbind, context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.Forbid();
            }
            if (collection is null)
            {
                // nothing to do
                return TypedResults.Ok();
            }
            var success = await collectionRepository.DeleteAsync(collection.Id, context.RequestAborted);
            return success is not null ? TypedResults.Ok() : TypedResults.BadRequest();
        })
        .RequireAuthorization()
        .WithName("DeleteCollectionByUri")
        .WithSummary("Delete collection")
        .WithDescription("Delete collection including all contained collections and items")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPost("/", async ([FromBody] CollectionCreateRequest request,
            ResourceRepository resourceRepository, CollectionRepository collectionRepository, HttpContext context) =>
        {
            var resource = await resourceRepository.GetResourceAsync(new CaldavUri(request.Uri), context, context.RequestAborted);
            if (!resource.Privileges.HasAnyOf(PrivilegeMask.Bind))
            {
                //  (DAV:needs-privilege): The DAV:bind privilege MUST be granted to the current user on the parent collection of the Request-URI.
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (resource.Exists)
            {
                return Results.Problem("A collection already exists at that location.", statusCode: StatusCodes.Status409Conflict);
            }
            if (!(resource.ParentResourceType == DavResourceType.Principal || resource.ParentResourceType == DavResourceType.Container))
            {
                return Results.Problem("A calendar can not be created at that location.", statusCode: StatusCodes.Status409Conflict);
            }
            if (resource.ResourceType != DavResourceType.Container)
            {
                return Results.Problem("A calendar can not be nested at that location.", statusCode: StatusCodes.Status409Conflict);
            }
            if (!string.IsNullOrEmpty(request.Timezone))
            {
                if (TimezoneParser.TryReadTimezone(request.Timezone ?? "", out var timeZone))
                {
                    request.Timezone = timeZone!.Id;
                }
                else
                {
                    return Results.Problem("Timezone Id is invalid or unknown.", statusCode: StatusCodes.Status422UnprocessableEntity);
                }
            }
            var collection = request.ToDto();
            collection.ParentId = resource.ParentResourceType == DavResourceType.Principal ? resource.Owner.Id : resource.Parent?.Id;
            if (collection.ParentId is null)
            {
                return Results.Conflict("A parent collection is missing.");
            }
            if (!string.IsNullOrEmpty(collection.Timezone))
            {
                if (TimezoneParser.TryReadTimezone(collection.Timezone ?? "", out var timeZone))
                {
                    collection.Timezone = timeZone!.Id;
                }
                else
                {
                    return Results.Problem("Timezone Id is invalid or unknown", statusCode: StatusCodes.Status422UnprocessableEntity);
                    // return Results.Conflict("Timezone Id is invalid or unknown.");
                }
            }
            collection.OwnerId = resource.Owner.UserId;
            collection.Uri = resource.DavName;
            collection.ParentContainerUri = resource.Uri.ParentCollectionPath;
            collection.Etag = resource.DavName.PrettyMD5Hash();
            collection.AuthorizedMask = resource.Owner.AuthorizedMask;
            if (request.PubliclyReadable)
            {
                collection.GlobalPermitSelf = PrivilegeMask.Read | PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadFreeBusy;
            }
            await collectionRepository.CreateAsync(collection, context.RequestAborted);
            return TypedResults.Created($"/api/collection{resource.DavName}");
        })
        .WithName("CreateCollection")
        .RequireAuthorization()
        .WithSummary("Create a new collection")
        .WithDescription("Creates a collection.")
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        ;

        return api;
    }
}
