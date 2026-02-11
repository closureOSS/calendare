using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Options;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;


namespace Calendare.Server.Api;

public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapCredentialApi(this RouteGroupBuilder api)
    {
        api.MapGet("/", async Task<Results<Ok<PrincipalResponse>, ForbidHttpResult, NotFound>> (UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.None, context.RequestAborted);
            if (currentUserPrincipal is null)
            {
                return TypedResults.Forbid();
            }
            return TypedResults.Ok(currentUserPrincipal.ToView(currentUserId: currentUserPrincipal.UserId));
        })
        .WithName("GetPrincipalOfMyself")
        .RequireAuthorization()
        .WithSummary("Verify user credentials")
        .WithDescription("Returns current user's basic infos")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        api.MapGet("/user/{username}", async Task<Results<Ok<List<CredentialResponse>>, ForbidHttpResult>> (
            string username,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            return TypedResults.Ok((await credentialRepository.ListCredentials(principal, context.RequestAborted)).ToView());
        })
        .WithName("GetCredentialsOfUser")
        .RequireAuthorization()
        .WithSummary("Get credentials of user")
        .WithDescription("Returns credential entries")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        ;

        api.MapGet("/types", (StaticDataRepository staticDataRepository, HttpContext context) =>
        {
            return TypedResults.Ok(staticDataRepository.UserAccessTypeList.Values);
        })
        .WithName("GetCredentialTypes")
        .RequireAuthorization()
        .WithSummary("Get types of credentials")
        .WithDescription("Returns credential types")
        ;

        api.MapGet("/randomsecret", ([FromQuery()] int length = 24) =>
        {
            var result = new CredentialSecretResponse { Secret = PasswordGenerator.RandomPassword(Math.Min(Math.Max(length, 8), 64)) };
            return TypedResults.Ok(result);
        })
        .WithName("CreateRandomSecret")
        .RequireAuthorization()
        .WithSummary("Generates random string to be used as password")
        .WithDescription("Returns random secret (as string)")
        ;

        api.MapPatch("/user/{username}/{credentialId}/lock", async Task<Results<Ok<CredentialResponse>, ForbidHttpResult, BadRequest>> (
            string username, int credentialId,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credential = await credentialRepository.UpdateLock(principal, credentialId, isLocked: false, context.RequestAborted);
            return credential is not null ? TypedResults.Ok(credential.ToView()) : TypedResults.BadRequest();
        })
        .WithName("UnlockCredential")
        .RequireAuthorization()
        .WithSummary("Unlock credential")
        .WithDescription("Returns credential entry")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapDelete("/user/{username}/{credentialId}/lock", async Task<Results<Ok<CredentialResponse>, ForbidHttpResult, BadRequest>> (
            string username, int credentialId,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credential = await credentialRepository.UpdateLock(principal, credentialId, isLocked: true, context.RequestAborted);
            return credential is not null ? TypedResults.Ok(credential.ToView()) : TypedResults.BadRequest();
        })
        .WithName("LockCredential")
        .RequireAuthorization()
        .WithSummary("Lock credential")
        .WithDescription("Returns credential entry")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPatch("/user/{username}/{credentialId}/reset", async Task<Results<Ok<CredentialResponse>, ForbidHttpResult, UnprocessableEntity<ProblemDetails>, BadRequest>> (
            string username, int credentialId, [FromBody] UserCredentialRequest request,
            UserManagementRepository userManagementRepository, UserRepository userRepository,
            CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Username))
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Missing username or password" });
            }
            var password = BetterPasswordHasher.HashPassword(request.Password);
            var credential = await credentialRepository.Reset(principal, credentialId, request.Username, password, context.RequestAborted);
            return credential is not null ? TypedResults.Ok(credential.ToView()) : TypedResults.BadRequest();
        })
        .WithName("SetCredentialPassword")
        .RequireAuthorization()
        .WithSummary("Set password credential")
        .WithDescription("Returns credential entry")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapDelete("/user/{username}/{credentialId}", async Task<Results<Ok, ForbidHttpResult, BadRequest>> (
            string username, int credentialId,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            await credentialRepository.Delete(principal, credentialId, context.RequestAborted);
            return TypedResults.Ok();
        })
        .WithName("DeleteCredential")
        .RequireAuthorization()
        .WithSummary("Delete credential")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPost("/user/{username}", async Task<Results<Created, ForbidHttpResult, UnprocessableEntity, BadRequest>> (
            string username, [FromBody] UserCredentialRequest request,
            UserManagementRepository userManagementRepository, UserRepository userRepository,
            CredentialRepository credentialRepository, StaticDataRepository staticData, HttpContext context) =>
        {
            var (principal, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            if (string.IsNullOrWhiteSpace(request.CredentialType))
            {
                return TypedResults.BadRequest();
            }
            var credentialType = staticData.UserAccessTypeList.Values.FirstOrDefault(c => string.Equals(c.Label, request.CredentialType, StringComparison.Ordinal));
            if (credentialType is null)
            {
                return TypedResults.BadRequest();
            }
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                switch (request.Template)
                {
                    case "EMAIL":
                        request.Username = principal.Email;
                        break;

                    case "USERNAME":
                        request.Username = principal.Username;
                        break;

                    default:
                        return TypedResults.UnprocessableEntity();
                }
            }
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                request.Password = PasswordGenerator.RandomPassword();
                Log.Warning("Empty password for credential {credentialType} autofilled", request.CredentialType);
            }
            var password = BetterPasswordHasher.HashPassword(request.Password);
            var response = await credentialRepository.Create(principal, credentialType, request.Username!, password, context.RequestAborted);
            if (response is null)
            {
                return TypedResults.BadRequest();
            }
            return TypedResults.Created($"/api/user/{response.Usr?.Username ?? response.Accesskey}/{response.Id}");
        })
        .WithName("CreateCredential")
        .RequireAuthorization()
        .WithSummary("Create credential")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPatch("/autolink", async Task<Results<Ok, NoContent, NotFound, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (CredentialRepository credentialRepository, UserRepository userRepository, HttpContext context) =>
        {
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.All, context.RequestAborted);
            if (currentUserPrincipal is not null || context.User.Identity?.Name is null)
            {
                return TypedResults.NoContent();
            }
            var email = context.User.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "email", StringComparison.Ordinal))?.Value;
            var emailVerified = context.User.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "email_verified", StringComparison.Ordinal))?.Value;
            if (emailVerified is null || !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase) || email is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Autoprovisiong needs valid email in access token" });
            }
            var issuer = context.User.Claims.FirstOrDefault(c => string.Equals(c.Type, "iss", StringComparison.Ordinal))?.Value;
            if (issuer is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Autoprovisiong needs valid issuer in access token" });
            }
            var credential = await credentialRepository.LinkByEmail(email, context.User.Identity.Name, issuer, context.RequestAborted);
            if (credential is null)
            {
                return TypedResults.NotFound();
            }
            return TypedResults.Ok();
        })
        .WithName("AutoLinkCurrentUser")
        .RequireAuthorization()
        .WithSummary("Link current user to a principal")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        api.MapPatch("/link/{sub}", async Task<Results<Ok, NoContent, NotFound, ForbidHttpResult, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (string sub, [FromBody, Required] UserCredentialRequest request, CredentialRepository credentialRepository, UserRepository userRepository, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Username))
            {
                return TypedResults.BadRequest(new ProblemDetails() { Title = "Username and password is required" });
            }
            // doing same checks as with /autolink to avoid creating an already linked account credentials
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.All, context.RequestAborted);
            if (currentUserPrincipal is not null || context.User.Identity?.Name is null)
            {
                return TypedResults.NoContent();
            }
            var verifiedUser = await userRepository.GetVerifiedUser(request.Username, request.Password, context.RequestAborted);
            if (verifiedUser is null)
            {
                return TypedResults.Forbid();
            }
            var issuer = context.User.Claims.FirstOrDefault(c => string.Equals(c.Type, "iss", StringComparison.Ordinal))?.Value;
            if (issuer is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Autoprovisiong needs valid issuer in access token" });
            }
            var credential = await credentialRepository.Link(verifiedUser.Value.User, sub, issuer, context.RequestAborted);
            if (credential is null)
            {
                return TypedResults.NotFound();
            }
            return TypedResults.Ok();
        })
        .WithName("LinkCurrentUser")
        .RequireAuthorization()
        .WithSummary("Link user to an existing principal")
        .WithDescription("Link user identified by a JWT bearer token to an existing principal, defined by username/password or accesskey/secret pair")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        api.MapPost("/autoprovision", async Task<Results<Created, NoContent, Conflict, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (
            [FromBody] UserRegisterRequest request, PrincipalRepository principalRepository, CredentialRepository credentialRepository,
            UserManagementRepository userManagementRepository, UserRepository userRepository,
            StaticDataRepository staticData, IOptions<UserDefaultOptions> userDefaults,
            HttpContext context) =>
        {
            // doing same checks as with /autolink to avoid creating an already linked account
            var (_, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, PrivilegeMask.All, context.RequestAborted);
            if (currentUserPrincipal is not null || context.User.Identity?.Name is null)
            {
                return TypedResults.NoContent();
            }
            var email = context.User.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "email", StringComparison.Ordinal))?.Value;
            var emailVerified = context.User.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "email_verified", StringComparison.Ordinal))?.Value;
            if (emailVerified is null || !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase) || email is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Autoprovisiong needs valid email in access token" });
            }
            var issuer = context.User.Claims.FirstOrDefault(c => string.Equals(c.Type, "iss", StringComparison.Ordinal))?.Value;
            if (issuer is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Autoprovisiong needs valid issuer in access token" });
            }
            var credential = await credentialRepository.LinkByEmail(email, context.User.Identity.Name, issuer, context.RequestAborted);
            if (credential is not null)
            {
                return TypedResults.NoContent();
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
            else
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Timezone is required" });
            }
            var principalType = staticData.PrincipalTypeList[PrincipalTypes.Individual];
            var username = request.Username ?? context.User.Identity.Name;
            var user = new Usr
            {
                Username = username,
                Email = email,
                EmailOk = SystemClock.Instance.GetCurrentInstant(),
                DateFormatType = userDefaults.Value.DateFormatType ?? UserDefaults.DateFormatType,
                Locale = userDefaults.Value.Locale ?? UserDefaults.Locale,
                IsActive = true,
            };
            var credentialAuto = new UsrCredential
            {
                Usr = user,
                CredentialTypeId = CredentialTypes.JwtBearer,
                Accesskey = context.User.Identity.Name,
                Secret = issuer,
                Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
            };
            user.Credentials.Add(credentialAuto);
            // var credentialPwd = new UsrCredential
            // {
            //     Usr = user,
            //     CredentialTypeId = CredentialTypes.Password,
            //     Accesskey = username,
            //     Secret = BetterPasswordHasher.HashPassword(PasswordGenerator.RandomPassword()),
            //     Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
            // };
            // user.Credentials.Add(credentialPwd);
            userManagementRepository.CreateDefaultCollections(user, principalType, request.Timezone ?? UserDefaults.TzId, request.Color, request.DisplayName, request.Description, []);
            try
            {
                var newUsername = await principalRepository.CreateAsync(user, admin: null, ct: context.RequestAborted);
                if (newUsername is null)
                {
                    return TypedResults.Conflict();
                }
                return TypedResults.Created($"/api/user/{newUsername}");
            }
            catch (Exception e)
            {
                Log.Warning("Failed to create account {error}", e.Message);
                return TypedResults.Conflict();
            }
        })
        .WithName("AutoProvisionCurrentUser")
        .RequireAuthorization()
        .WithSummary("Create account and default collections for current user")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        return api;
    }
}
