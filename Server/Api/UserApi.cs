using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Serilog;

namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapUserApi(this RouteGroupBuilder api)
    {

        api.MapGet("/", async Task<Results<Ok<List<PrincipalResponse>>, NotFound, UnauthorizedHttpResult>> (
            PrincipalRepository principalRepository, UserRepository userRepository, HttpContext context,
            [FromQuery(Name = "search")] string? searchTerm,
            [FromQuery(Name = "types")] string[] typeFilter,
            [FromQuery(Name = "unrestricted")] bool unrestricted = false,
            [FromQuery(Name = "technical")] bool withSystemAccounts = false
        ) =>
        {
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.None, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.NotFound();
            }
            if (currentUserPrincipal.UserId != StockPrincipal.Admin)
            {
                withSystemAccounts = false;
            }
            else
            {
                withSystemAccounts = true;
                unrestricted = true;
            }
            var query = new PrincipalListQuery
            {
                CurrentUser = currentUserPrincipal,
                IsTracking = false,
                Unrestricted = unrestricted,
                SearchTerm = searchTerm,
                IncludeSystemAccounts = withSystemAccounts,
            };
            if (typeFilter is not null)
            {
                query.PrincipalTypes = [];
                foreach (var type in typeFilter)
                {
                    if (string.Equals(type, "*", StringComparison.InvariantCulture))
                    {
                        query.PrincipalTypes = null;
                        break;
                    }
                    var principalType = await principalRepository.GetPrincipalTypeAsync(type, context.RequestAborted);
                    if (principalType is not null)
                    {
                        query.PrincipalTypes.Add(principalType);
                    }
                }
            }
            var response = await principalRepository.ListPrincipalsAsync(query, context.RequestAborted);
            return TypedResults.Ok(response);
        })
        .WithName("GetUserList")
        .RequireAuthorization()
        .WithSummary("List principal information")
        .WithDescription("Returns visible principals")
        ;

        api.MapGet("/{username}", async Task<Results<Ok<PrincipalResponse>, NotFound>> (string username,
        UserRepository userRepository, PrincipalRepository principalRepository, HttpContext context) =>
        {
            var (principal, currentPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.ReadFreeBusy, context.RequestAborted);
            if (principal is null || currentPrincipal is null)
            {
                return TypedResults.NotFound();
            }
            var full = await principalRepository.GetUserAsync(new()
            {
                CurrentUser = currentPrincipal,
                Username = principal.Username,
                IncludeProxy = true,
                IsTracking = false,
            }, context.RequestAborted);
            var principalCollection = full?.Collections.FirstOrDefault(c => c.CollectionType == CollectionType.Principal && c.ParentId == null)?.ToPrincipal();
            if (full is null || principalCollection is null)
            {
                Log.Error("Re-reading complete principal {principal} information fails", principal.Username);
                return TypedResults.NotFound();
            }
            var response = principalCollection.ToView(
                currentPrincipal.UserId,
                full.Collections.Any(c => c.CollectionSubType == CollectionSubType.CalendarProxyRead || c.CollectionSubType == CollectionSubType.CalendarProxyWrite || string.Equals(c.PrincipalType?.Label, PrincipalTypeCode.Group, StringComparison.Ordinal)),
                full.Collections.Count(c => c.CollectionSubType == CollectionSubType.SchedulingInbox || c.CollectionSubType == CollectionSubType.SchedulingOutbox) == 2
            );
            return TypedResults.Ok(response);
        })
        .WithName("GetUser")
        .RequireAuthorization()
        .WithSummary("Get user information")
        .WithDescription("Returns principal")
        ;


        api.MapPost("/", async Task<Results<Created, Conflict, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (
            [FromBody] UserRegisterRequest request, UserManagementRepository userManagementRepository, PrincipalRepository principalRepository,
            UserRepository userRepository, HttpContext context) =>
        {
            // TODO: [HIGH] Who has the rights to create new principals??!
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.None, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "User principal unknown" });
            }
            var user = request.ToDto();
            if (user is null)
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "Malformed user request" });
            }
            var principalType = await userRepository.GetPrincipalTypeAsync(request.Type, context.RequestAborted);
            if (principalType is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Principal type not supported or recognized" });
            }
            if (!string.IsNullOrEmpty(request.Timezone))
            {
                if (TimezoneParser.TryReadTimezone(request.Timezone ?? "", out var timeZone))
                {
                    request.Timezone = timeZone!.Id;
                }
                else
                {
                    return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Timezone Id is invalid or unknown" });
                }
            }
            // user.Principal!.Type = principalType;
            if (!user.Verify())
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Username or e-mail is not valid" });
            }
            if (string.Equals(principalType.Label, PrincipalTypeCode.Individual, StringComparison.Ordinal))
            {
                var access = request.ToAccessDto();
                if (!string.IsNullOrEmpty(access.Secret))
                {
                    // return Results.Problem("Password must be set", statusCode: StatusCodes.Status400BadRequest);
                    access.Usr = user;
                    user.Credentials.Add(access);
                }
                else
                {
                    Log.Warning("No access credentials created as no password was given");
                }
            }
            userManagementRepository.CreateDefaultCollections(user, principalType, request.Timezone ?? "UTC", request.Color, request.DisplayName, request.Description, request.SkipCollections);
            var newUsername = await principalRepository.CreateAsync(user, currentUserPrincipal.UserId != StockPrincipal.Admin ? currentUserPrincipal : null, context.RequestAborted);
            if (newUsername is null)
            {
                return TypedResults.Conflict();
            }
            return TypedResults.Created($"/api/user/{newUsername}");
        })
        .WithName("CreateUser")
        .RequireAuthorization()
        .WithSummary("Create user account and all default collections")
        // .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        api.MapPut("/{username}", async Task<Results<Ok<PrincipalResponse>, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>, NotFound>> (
            [FromRoute] string username, [FromBody] UserAmendRequest request,
            PrincipalRepository principalRepository, UserRepository userRepository, HttpContext context) =>
        {
            var existingUser = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteProperties, context.RequestAborted);
            if (existingUser.Principal is null)
            {
                return TypedResults.NotFound();
            }
            if (string.IsNullOrEmpty(request.Timezone) || !TimezoneParser.TryReadTimezone(request.Timezone ?? "", out var timeZone))
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Timezone Id is invalid or unknown" });
            }
            request.Timezone = timeZone!.Id;
            // TODO: [HIGH] Implement update user
            var updatedUser = await principalRepository.UpdateAsync(existingUser.Principal, request, context.RequestAborted);
            return updatedUser is not null ? TypedResults.Ok(updatedUser.Collections.First().ToPrincipal().ToView()) : TypedResults.BadRequest(new ProblemDetails());
        })
        .WithName("UpdateUser")
        .RequireAuthorization()
        .WithSummary("Amend user information")
        .WithDescription("Returns user registration data")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        api.MapPatch("/{username}/email/confirm", async Task<Results<Ok<PrincipalResponse>, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>, NotFound>> (
            [FromRoute] string username, [FromBody] UserConfirmEmailRequest request,
            PrincipalRepository principalRepository, UserRepository userRepository, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.ConfirmationToken))
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "Missing email confirmation token" });
            }
            var existingUser = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteProperties, context.RequestAborted);
            if (existingUser.Principal is null)
            {
                return TypedResults.NotFound();
            }
            var currentUser = await principalRepository.GetUserAsync(new() { CurrentUser = new(), Username = existingUser.Principal.Username, IsTracking = false }, context.RequestAborted);
            if (currentUser is null)
            {
                return TypedResults.NotFound();
            }
            if (existingUser.CurrentUserPrincipal?.UserId != StockPrincipal.Admin)
            {
                if (!currentUser.CheckVerificationToken(request.ConfirmationToken.Trim()))
                {
                    return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Invalid email confirmation token" });
                }
            }
            var updatedUser = await principalRepository.ConfirmEmail(existingUser.Principal, context.RequestAborted);
            return updatedUser is not null ? TypedResults.Ok(updatedUser.Collections.First().ToPrincipal().ToView()) : TypedResults.BadRequest(new ProblemDetails());
        }
        )
        .WithName("ConfirmUserEmail")
        .RequireAuthorization()
        .WithSummary("Confirm email address")
        .WithDescription("Returns user registration data")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPatch("/{username}/email/generate", async Task<Results<Ok<PrincipalResponse>, BadRequest<ProblemDetails>, NotFound>> (
            [FromRoute] string username,
            PrincipalRepository principalRepository, UserRepository userRepository, HttpContext context) =>
        {
            var existingUser = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteProperties, context.RequestAborted);
            if (existingUser.Principal is null)
            {
                return TypedResults.NotFound();
            }
            var currentUser = await principalRepository.GetUserAsync(new() { CurrentUser = new(), Username = existingUser.Principal.Username, IsTracking = false }, context.RequestAborted);
            if (currentUser is null)
            {
                return TypedResults.NotFound();
            }
            var (email, token) = currentUser.GetEmailVerificationToken();
            // TODO: Send token to email address
            Log.Information("Email {email} with verification token {token}", email, token);
            return currentUser is not null ? TypedResults.Ok(currentUser.Collections.First().ToPrincipal().ToView()) : TypedResults.BadRequest(new ProblemDetails());
        }
        )
        .WithName("SendEmailConfirmationCode")
        .RequireAuthorization()
        .WithSummary("Generates a email confirmation code and triggers delivery")
        .WithDescription("Returns user registration data")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapDelete("/{username}", async (string username, PrincipalRepository principalRepository, UserRepository userRepository, HttpContext context) =>
        {
            var principal = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.All, context.RequestAborted);
            if (principal.Principal is null)
            {
                return Results.Forbid();
            }
            var existingUser = await principalRepository.DeleteAsync(principal.Principal.Username, context.RequestAborted);
            if (existingUser is null)
            {
                return Results.NotFound();
            }
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .RequireAuthorization()
        .WithSummary("Deletes a user registration")
        .WithDescription("Deletes a user with all related data (calendars, addressbooks, ...)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        return api;
    }
}
