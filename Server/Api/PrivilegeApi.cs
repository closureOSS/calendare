using System.ComponentModel.DataAnnotations;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
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
    public static RouteGroupBuilder MapPrivilegeApi(this RouteGroupBuilder api)
    {
        api.MapGet("/", async Task<Results<Ok<PrivilegeResponse>, BadRequest<ProblemDetails>>> (
            UserRepository userRepository, StaticDataRepository staticDataRepository, HttpContext context,
            [FromQuery(Name = "grantee")] string? granteeUsername,
            [FromQuery(Name = "empty")] bool empty = false,
            [FromQuery(Name = "transitive")] bool transitive = true
        ) =>
        {
            var granteePrincipal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, granteeUsername, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (granteePrincipal.Principal is null)
            {
                return TypedResults.BadRequest(new ProblemDetails { Title = "Unknown user" });
            }
            var aces = await userRepository.GetPrivilegesGrantedByAsync(granteePrincipal.Principal.Id, transitive, context.RequestAborted);
            return TypedResults.Ok(aces.ToResponse(staticDataRepository, reverse: true, includeEmpty: empty));
        })
        .WithName("GetPrivilegesIncoming")
        .RequireAuthorization()
        .WithSummary("Get privileges granted BY others (receiving access)")
        .WithDescription("")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapGet("/grants", async Task<Results<Ok<PrivilegeResponse>, BadRequest<ProblemDetails>>> (
            UserRepository userRepository, CollectionRepository collectionRepository, StaticDataRepository staticDataRepository, HttpContext context,
            [FromQuery(Name = "grantor")] string? grantorPath,
            [FromQuery(Name = "empty")] bool empty = false,
            [FromQuery(Name = "transitive")] bool transitive = true
        ) =>
        {
            var (result, principal, grantor) = await ReadGrantor(userRepository, collectionRepository, context.User.Identity, grantorPath, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (principal is null || grantor is null)
            {
                return TypedResults.BadRequest(new ProblemDetails { Title = "Unknown user" });
            }
            // TODO: Check if principal has access to collection
            // TODO: Unnest resource
            var aces = await userRepository.GetPrivilegesGrantedToAsync([grantor.Id, grantor.OwnerId], transitive, context.RequestAborted);
            return TypedResults.Ok(aces.ToResponse(staticDataRepository, reverse: false, includeEmpty: empty));
        })
        .WithName("GetPrivilegesOutgoing")
        .RequireAuthorization()
        .WithSummary("Get privileges granted from grantor TO others (giving access)")
        .WithDescription("")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPost("/", async (
            UserRepository userRepository, CollectionRepository collectionRepository, StaticDataRepository staticDataRepository, HttpContext context,
            [FromBody, Required] PrivilegeRequest request
        ) =>
        {
            // TODO: Check if user has right to add members to groups
            var (result, principal, grantor) = await ReadGrantor(userRepository, collectionRepository, context.User.Identity, request.GrantorUri, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null || grantor is null)
            {
                return Results.BadRequest("Unknown principal");
            }
            // TODO: Check if principal is allowed to amend privileges on request.GrantorUri
            await userRepository.AmendRelationshipAsync(grantor, request.Groups, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("CreatePrivileges")
        .RequireAuthorization()
        .WithSummary("Amends all privileges (adding and removing)")
        .WithDescription("Does not support custom privileges")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;


        api.MapPut("/grants", async (
            [FromQuery(Name = "grantee"), Required] string granteeUsername,
            [FromQuery(Name = "grantor")] string? grantorPath,
            [FromQuery(Name = "reltype")] string? relationshipType,
            UserRepository userRepository, PrincipalRepository principalRepository, CollectionRepository collectionRepository, HttpContext context) =>
        {
            var (result, principal, grantor) = await ReadGrantor(userRepository, collectionRepository, context.User.Identity, grantorPath, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null || grantor is null)
            {
                return result;
            }
            var granteePrincipal = await principalRepository.GetPrincipalAsync(new PrincipalQuery
            {
                CurrentUser = principal,
                Username = granteeUsername,
                IsTracking = true,
            }, context.RequestAborted);
            if (granteePrincipal is null)
            {
                return Results.Conflict($"Grantee {granteeUsername} unknown");
            }
            var relationship = await userRepository.GetRelationshipTypeAsync(relationshipType ?? "Administers", context.RequestAborted);
            if (relationship is null)
            {
                return Results.Conflict($"Relationship type {relationshipType} unknown");
            }
            var ace = new AccessControlEntity
            {
                Grantee = granteePrincipal,
                Privileges = relationship.Privileges,
                GrantType = relationship,
            };
            try
            {
                await userRepository.AmendRelationshipAsync(grantor, [ace], context.RequestAborted);
                await userRepository.RebuildPrivilegesAsync(principal, context.RequestAborted); // TODO: Hack for testing dependencies
            }
            catch
            {
                return Results.NotFound();
            }
            return Results.Ok();
        })
        .WithName("AddPrivilege")
        .RequireAuthorization()
        .WithSummary("Grants privileges to another user")
        .WithDescription("")
        ;

        api.MapDelete("/grants", async (
            [FromQuery(Name = "grantee"), Required] string granteeUsername,
            [FromQuery(Name = "grantor")] string? grantorPath,
            UserRepository userRepository, CollectionRepository collectionRepository, HttpContext context) =>
        {
            var (result, principal, grantor) = await ReadGrantor(userRepository, collectionRepository, context.User.Identity, grantorPath, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null || grantor is null)
            {
                return result;
            }
            var granteePrincipal = await userRepository.GetPrincipalAsync(granteeUsername, context.RequestAborted);
            if (granteePrincipal is null)
            {
                return Results.Conflict($"Grantee {granteeUsername} unknown");
            }
            var rel = await userRepository.DeleteRelationshipAsync(grantor, granteePrincipal, context.RequestAborted);
            if (rel is null)
            {
                return Results.BadRequest();
            }
            return rel.Value ? Results.Ok() : Results.NotFound();
        })
        .WithName("DeletePrivileges")
        .RequireAuthorization()
        .WithSummary("Removes privileges to another user")
        .WithDescription("")
        ;

        return api;
    }

    private static async Task<(IResult Result, Principal? Principal, Collection? Grantor)> ReadGrantor(UserRepository userRepository, CollectionRepository collectionRepository, IIdentity? identity, string? grantorPath, PrivilegeMask accessRights, CancellationToken ct)
    {
        var principal = await userRepository.GetCurrentUserPrincipalAsync(identity, ct);
        if (principal is null)
        {
            return (Results.BadRequest("Unknown user"), null, null);
        }
        if (string.IsNullOrEmpty(grantorPath))
        {
            grantorPath = $"/{principal.Username}/";
        }
        var grantor = await collectionRepository.GetAsync(grantorPath, ct);
        if (grantor is null)
        {
            return (Results.Conflict($"Grantor {grantorPath} unknown"), principal, null);
        }
        // TODO: Check access rights
        return (Results.Ok(), principal, grantor);
    }
}
