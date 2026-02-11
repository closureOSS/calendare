using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
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
    public static RouteGroupBuilder MapProxyMembershipApi(this RouteGroupBuilder api)
    {
        api.MapGet("/proxy/{relType}", async Task<Results<Ok<List<GroupMemberRef>>, NotFound, BadRequest<ProblemDetails>>> (
            [FromRoute, Required] RelationshipTypes relType,
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to read groups memberships if principalName is set
            var proxyPrincipal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.Read, context.RequestAborted);
            if (proxyPrincipal.Principal is null)
            {
                return TypedResults.BadRequest(new ProblemDetails { Title = "Unknown user principal" });
            }
            var group = await ReadProxyGroup(userRepository, proxyPrincipal.Principal, relType, context.RequestAborted);
            if (group is null)
            {
                return TypedResults.NotFound();
            }
            var memberships = await userRepository.GetGroupMembersDirectAsync([group.Id], context.RequestAborted);
            return TypedResults.Ok(memberships.ToMemberlistView());
        })
        .WithName("GetProxyMembers")
        .RequireAuthorization()
        .WithSummary("Get members of proxy group")
        .WithDescription("By default the own proxy group members are returned, otherwise from the given principal")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPut("/proxy/{relType}/{memberName}", async (
            [FromRoute, Required] RelationshipTypes relType,
            [FromRoute, Required] string memberName,
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to add members to proxyPrincipal's proxy groups
            var proxyPrincipal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.Read, context.RequestAborted);
            if (proxyPrincipal.Principal is null)
            {
                return Results.BadRequest("Unknown user principal");
            }
            var groupToAdd = await ReadProxyGroup(userRepository, proxyPrincipal.Principal, relType, context.RequestAborted);
            if (groupToAdd is null)
            {
                return Results.NotFound();
            }
            var groupToRemove = await ReadProxyGroup(userRepository, proxyPrincipal.Principal, relType == RelationshipTypes.ReadWrite ? RelationshipTypes.Read : RelationshipTypes.ReadWrite, context.RequestAborted);
            if (groupToRemove is null)
            {
                return Results.NotFound();
            }
            var member = await userRepository.GetPrincipalAsCollectionAsync(memberName, context.RequestAborted);
            if (member is null)
            {
                return Results.BadRequest("Mutation not allowed");
            }
            if (member.Groups.Any(c => c.Id == groupToRemove.Id))
            {
                var resultRemove = await userRepository.RemoveGroupMemberAsync(groupToRemove, member, context.RequestAborted);
            }
            var result = await userRepository.AddGroupMemberAsync(groupToAdd, member, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("AddProxyMembership")
        .RequireAuthorization()
        .WithSummary("Adds a member to the proxy group Read or ReadWrite of the user or supplied principal")
        .WithDescription("")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        api.MapDelete("/proxy/{relType}/{memberName}", async (
            [FromRoute, Required] RelationshipTypes relType,
            [FromRoute, Required] string memberName,
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to remove members to proxyPrincipal's proxy groups
            // TODO: also check if member to be removed is current user. If true, allow removal
            var proxyPrincipal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.Read, context.RequestAborted);
            if (proxyPrincipal.Principal is null)
            {
                return Results.BadRequest("Unknown user principal");
            }
            var group = await ReadProxyGroup(userRepository, proxyPrincipal.Principal, relType, context.RequestAborted);
            if (group is null)
            {
                return Results.NotFound();
            }
            var member = await userRepository.GetPrincipalAsCollectionAsync(memberName, context.RequestAborted);
            if (member is null)
            {
                return Results.BadRequest("Mutation not allowed");
            }
            var result = await userRepository.RemoveGroupMemberAsync(group, member, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("DeleteProxyMembership")
        .RequireAuthorization()
        .WithSummary("Removes member from a proxy group Read or ReadWrite of the user or supplied principal")
        .WithDescription("")
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;
        return api;
    }

    private static async Task<Collection?> ReadProxyGroup(UserRepository userRepository, Principal principal, RelationshipTypes relType, CancellationToken ct)
    {
        var group = await userRepository.GetMembersAsync(principal.UserId, relType == RelationshipTypes.ReadWrite ? CollectionSubType.CalendarProxyWrite : CollectionSubType.CalendarProxyRead, ct);
        if (group is null || !string.Equals(group.PrincipalType?.Label, PrincipalTypeCode.Group, System.StringComparison.Ordinal))
        {
            return null;
        }
        return group;
    }
}
