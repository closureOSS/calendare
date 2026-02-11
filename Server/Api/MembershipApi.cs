using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
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
    public static RouteGroupBuilder MapMembershipApi(this RouteGroupBuilder api)
    {
        api.MapGet("/", async Task<Results<Ok<MembershipResponse>, BadRequest<ProblemDetails>>> (
            UserRepository userRepository, HttpContext context,
            [FromQuery(Name = "principal")] string? principalName,
            [FromQuery(Name = "direction")] MembershipDirection direction = MembershipDirection.Members
            ) =>
        {
            // TODO: Check if user has right to read groups memberships if principalName is set
            var principal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (principal.Principal is null)
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "Principal unknown" });
            }
            var memberships = await userRepository.GetPrincipalMembershipsAsync(principal.Principal.UserId, direction, context.RequestAborted);
            var result = memberships.ToMembershipView();
            return TypedResults.Ok(result);
        })
        .WithName("GetMemberships")
        .RequireAuthorization()
        .WithSummary("Get members of own groups and memberships in others groups of principal (or self as default)")
        .WithDescription("")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapGet("/group/{groupName}", async Task<Results<Ok<List<GroupMemberRef>>, NotFound, BadRequest<ProblemDetails>>> (
            [FromRoute, Required] string groupName,
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to read groups memberships if principalName is set
            var group = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, groupName, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (group.Principal is null)
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "Group is unknown or no access" });
            }
            // TODO: Check if it's a group
            var memberships = await userRepository.GetGroupMembersDirectAsync([group.Principal.Id], context.RequestAborted);
            return TypedResults.Ok(memberships.ToMemberlistView());
            // return TypedResults.Ok(memberships);
        })
        .WithName("GetGroupMembers")
        .RequireAuthorization()
        .WithSummary("Get members of group")
        .WithDescription("")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPost("/", async (
            [FromBody, Required] MembershipRequest request,
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to add members to groups
            var principal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal.Principal is null)
            {
                return Results.BadRequest("Unknown principal");
            }
            // TODO: Check all groups if principal is allowed to add/remove members
            // TODO: Check if members are main principals
            await userRepository.AmendGroupMembersAsync(request, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("CreateMemberships")
        .RequireAuthorization()
        .WithSummary("Amends memberships, all members are replaced")
        .WithDescription("")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        api.MapPut("/group/{groupName}/{memberName}", async (
            [FromRoute, Required] string groupName,
            [FromRoute, Required] string memberName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to add members to group if groupName differs from username
            var principal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, groupName, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal.Principal is null)
            {
                return Results.BadRequest("Unknown group");
            }
            var group = await userRepository.GetMembersAsync(principal.Principal.Uri, context.RequestAborted);
            if (group is null || !string.Equals(group.PrincipalType?.Label, PrincipalTypeCode.Group, System.StringComparison.Ordinal))
            {
                return Results.NotFound();
            }
            var member = await userRepository.GetPrincipalAsCollectionAsync(memberName, context.RequestAborted);
            if (member is null)
            {
                return Results.BadRequest("Mutation not allowed");
            }
            var result = await userRepository.AddGroupMemberAsync(group, member, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("AddGroupmembership")
        .RequireAuthorization()
        .WithSummary("Adds a new member to a group")
        .WithDescription("")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        api.MapDelete("/group/{groupName}/{memberName}", async (
            [FromRoute, Required] string groupName,
            [FromRoute, Required] string memberName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to remove members from group if groupName differs from username
            var principal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, groupName, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal.Principal is null)
            {
                return Results.BadRequest("Unknown group");
            }
            var group = await userRepository.GetMembersAsync(principal.Principal.Uri, context.RequestAborted);
            if (group is null || !string.Equals(group.PrincipalType?.Label, PrincipalTypeCode.Group, System.StringComparison.Ordinal))
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
        .WithName("DeleteGroupmembership")
        .RequireAuthorization()
        .WithSummary("Removes a member from a group")
        .WithDescription("")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        api.MapPatch("/group", async (
            [FromQuery(Name = "principal")] string? principalName,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: Check if user has right to add members to group if groupName differs from username
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, principalName, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return Results.BadRequest("Unknown group");
            }
            await userRepository.RebuildPrivilegesAsync(principal, context.RequestAborted);
            return Results.Ok();
        })
        .WithName("RecalcGroupmembership")
        .RequireAuthorization()
        .WithSummary("Recalcs privileges for all groups where the principal is a member")
        .WithDescription("Maintenance function, adding or removing members triggers this automatically")
        .Produces(StatusCodes.Status200OK)
        ;

        return api;
    }
}
