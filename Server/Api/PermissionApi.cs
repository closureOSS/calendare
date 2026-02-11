using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapPermissionApi(this RouteGroupBuilder api)
    {
        api.MapGet("/", async Task<Results<Ok<PermissionResponse>, ForbidHttpResult, NotFound, BadRequest>> (
            [FromQuery(Name = "uri"), Required] string uri,
            CollectionRepository collectionRepository, ResourceRepository resourceRepository, HttpContext context) =>
        {
            var (collection, resource) = await TryGetAuthorizedCollection(resourceRepository, collectionRepository, uri, PrivilegeMask.ReadAcl | PrivilegeMask.WriteAcl, context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.Forbid();
            }
            if (collection is null)
            {
                return TypedResults.NotFound();
            }
            var result = new PermissionResponse
            {
                Uri = collection.Uri,
                Username = collection.Owner.Username,
                CollectionType = collection.CollectionType,
                CollectionSubType = collection.CollectionSubType,
                PrincipalType = collection.PrincipalType,
                AuthorizedProhibit = collection.AuthorizedProhibit,
                GlobalPermitSelf = collection.GlobalPermitSelf,
                OwnerProhibit = collection.OwnerProhibit,
                Permissions = resource.Privileges,
                IsRoot = resource.CurrentUser.Id == StockPrincipal.Admin,
            };
            return TypedResults.Ok(result);
        })
        .WithName("GetPermissions")
        .RequireAuthorization()
        .WithSummary("Get permissions of collection")
        .WithDescription("Returns permission set for collection")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        api.MapGet("/self", async Task<Results<Ok<PermissionResponse>, NotFound, BadRequest>> (
           UserRepository userRepository, HttpContext context) =>
        {
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.None, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.NotFound();
            }
            var cupr = currentUserPrincipal.ToView(currentUserPrincipal.UserId);
            var adminPermissions = await userRepository.CheckPrivilegeAsync(new() { Id = StockPrincipal.Admin }, currentUserPrincipal, context.RequestAborted);
            var result = new PermissionResponse
            {
                Uri = currentUserPrincipal.Uri,
                Username = currentUserPrincipal.Username,
                CollectionType = currentUserPrincipal.CollectionType,
                CollectionSubType = currentUserPrincipal.CollectionSubType,
                PrincipalType = currentUserPrincipal.PrincipalType,
                AuthorizedProhibit = currentUserPrincipal.AuthorizedProhibit,
                GlobalPermitSelf = currentUserPrincipal.GlobalPermitSelf,
                OwnerProhibit = currentUserPrincipal.OwnerProhibit,
                Permissions = cupr.Permissions,
                IsRoot = cupr.IsRoot,
                IsAdmin = currentUserPrincipal.UserId == StockPrincipal.Admin || (adminPermissions & PrivilegeMask.Bind) == PrivilegeMask.Bind,
            };
            return TypedResults.Ok(result);
        })
        .WithName("GetPermissionsSelf")
        .RequireAuthorization()
        .WithSummary("Get permissions of current users")
        .WithDescription("Returns permission set for current user")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;


        api.MapPatch("/", async Task<Results<Ok, ForbidHttpResult, NotFound, BadRequest>> (
            [FromBody] PermissionRequest request,
            CollectionRepository collectionRepository, ResourceRepository resourceRepository, HttpContext context) =>
        {
            var (collection, resource) = await TryGetAuthorizedCollection(resourceRepository, collectionRepository, request.Uri, PrivilegeMask.WriteAcl, context, context.RequestAborted);
            if (resource is null)
            {
                return TypedResults.Forbid();
            }
            if (collection is null)
            {
                return TypedResults.NotFound();
            }
            await collectionRepository.AmendPermission(collection, request, context.RequestAborted);
            return TypedResults.Ok();
        })
        .WithName("SetPermissions")
        .RequireAuthorization()
        .WithSummary("Set permissions of collection")
        .WithDescription("Returns permission set for collection")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        return api;
    }
}
