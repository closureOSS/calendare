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

        api.MapGet("/user/{username}/{credentialSubject}", async Task<Results<Ok<CredentialResponse>, NotFound, ForbidHttpResult>> (
            string username, string credentialSubject, [FromQuery()] string? credentialType,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.ReadAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var (credential, multiple) = await credentialRepository.GetCredential(new(principal, credentialSubject, credentialType), context.RequestAborted);
            if (credential is null)
            {
                return TypedResults.NotFound();
            }
            return TypedResults.Ok(credential.ToView());
        })
        .WithName("GetCredentialOfUser")
        .RequireAuthorization()
        .WithSummary("Get credential of user")
        .WithDescription("Returns credential")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
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

        api.MapPatch("/user/{username}/{credentialSubject}/lock", async Task<Results<Ok<CredentialResponse>, ForbidHttpResult, BadRequest>> (
            string username, string credentialSubject, [FromQuery()] string? credentialType,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credentialRef = new CredentialRef(principal, credentialSubject, credentialType);
            var credential = await credentialRepository.UpdateLock(credentialRef, doLock: true, context.RequestAborted);
            return credential is not null ? TypedResults.Ok(credential.ToView()) : TypedResults.BadRequest();
        })
        .WithName("LockCredential")
        .RequireAuthorization()
        .WithSummary("Lock credential")
        .WithDescription("Returns credential entry")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapDelete("/user/{username}/{credentialSubject}/lock", async Task<Results<Ok<CredentialResponse>, ForbidHttpResult, BadRequest>> (
            string username, string credentialSubject, [FromQuery()] string? credentialType,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credentialRef = new CredentialRef(principal, credentialSubject, credentialType);
            var credential = await credentialRepository.UpdateLock(credentialRef, doLock: false, context.RequestAborted);
            return credential is not null ? TypedResults.Ok(credential.ToView()) : TypedResults.BadRequest();
        })
        .WithName("UnlockCredential")
        .RequireAuthorization()
        .WithSummary("Unlock credential")
        .WithDescription("Returns credential entry")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        ;

        api.MapPatch("/user/{username}/{credentialSubject}/reset", async Task<Results<Ok<CredentialCreateResponse>, ForbidHttpResult, UnprocessableEntity<ProblemDetails>>> (
            string username, string credentialSubject, [FromQuery()] string? credentialType, [FromBody] UserCredentialResetRequest? request,
            UserManagementRepository userManagementRepository, UserRepository userRepository,
            CredentialRepository credentialRepository, IOptions<UserConstraintOptions> userConstraints, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            if (request is null)
            {
                request = new UserCredentialResetRequest { Password = PasswordGenerator.RandomPassword(), };
            }
            else if (string.IsNullOrWhiteSpace(request.Password))
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Missing password" });
            }
            var credentialRef = new CredentialRef(principal, credentialSubject, credentialType);
            var passwordHash = BetterPasswordHasher.HashPassword(request.Password);
            var credential = await credentialRepository.Reset(credentialRef, passwordHash, context.RequestAborted);
            if (credential is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Credential not identified uniquely" });
            }
            var response = credential.ToCreateResponse(request.Password);
            switch (credential.CredentialType.Label)
            {
                case CredentialTypes.AccessKeyCode:
                    {
                        if (string.IsNullOrEmpty(userConstraints.Value.ApplicationKeySecret))
                        {
                            return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Server setup doesn't allow application keys" });
                        }
                        response.Secret = await ApplicationKeyRepository.CreateTokenAsync(userConstraints.Value, credential.Accesskey, request.Password, context.RequestAborted);
                    }
                    break;
                default:
                    break;
            }
            return TypedResults.Ok(response);
        })
        .WithName("SetCredentialPassword")
        .RequireAuthorization()
        .WithSummary("Change password of credential. If no password supplied one will be generated.")
        .WithDescription("Returns credential entry with secret")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        ;

        api.MapDelete("/user/{username}/{credentialSubject}", async Task<Results<NoContent, ForbidHttpResult, UnprocessableEntity<ProblemDetails>>> (
            string username, string credentialSubject, [FromQuery()] string? credentialType,
            UserRepository userRepository, CredentialRepository credentialRepository, HttpContext context) =>
        {
            var (principal, _) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credentialRef = new CredentialRef(principal, credentialSubject, credentialType);
            var result = await credentialRepository.Delete(credentialRef, context.RequestAborted);
            if (!result)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Credential not identified uniquely" });
            }
            return TypedResults.NoContent();
        })
        .WithName("DeleteCredential")
        .RequireAuthorization()
        .WithSummary("Delete credential")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        ;

        api.MapPost("/user/{username}", async Task<Results<Created<CredentialCreateResponse>, ForbidHttpResult, UnprocessableEntity<ProblemDetails>, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> (
            string username, [FromBody] UserCredentialCreateRequest request,
            UserManagementRepository userManagementRepository, UserRepository userRepository,
            CredentialRepository credentialRepository, StaticDataRepository staticData, IOptions<UserConstraintOptions> userConstraints, HttpContext context) =>
        {
            var (principal, currentUserPrincipal) = await TryGetAuthorizedPrincipal(userRepository, context.User.Identity, username, PrivilegeMask.WriteAcl, context.RequestAborted);
            if (principal is null)
            {
                return TypedResults.Forbid();
            }
            var credentialTypeLabel = string.Empty;
            switch (request.Template)
            {
                case UserCredentialCreateTemplate.Email:
                    credentialTypeLabel = CredentialTypes.PasswordCode;
                    request.Username = principal.Email;
                    break;

                case UserCredentialCreateTemplate.Username:
                    credentialTypeLabel = CredentialTypes.PasswordCode;
                    request.Username = principal.Username;
                    break;

                case UserCredentialCreateTemplate.Generic:
                    credentialTypeLabel = CredentialTypes.PasswordCode;
                    if (string.IsNullOrWhiteSpace(request.Username))
                    {
                        return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Generic requires username" });
                    }
                    break;

                case UserCredentialCreateTemplate.ApplicationKey:
                    credentialTypeLabel = CredentialTypes.AccessKeyCode;
                    if (string.IsNullOrWhiteSpace(request.Username))
                    {
                        request.Username = PasswordGenerator.RandomUriSegment();
                    }
                    if (!string.IsNullOrWhiteSpace(request.Password))
                    {
                        return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Application key no password allowed" });
                    }
                    if (string.IsNullOrWhiteSpace(request.Description))
                    {
                        return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Application key requires description" });
                    }
                    break;

                case UserCredentialCreateTemplate.JwtBearer:
                    return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Unsupported credential type" });

                default:
                    return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Unknown credential create template" });
            }
            var credentialType = staticData.UserAccessTypeList.Values.FirstOrDefault(c => string.Equals(c.Label, credentialTypeLabel, StringComparison.Ordinal));
            if (credentialType is null)
            {
                return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Unknown credential type" });
            }
            string? passwordHash = null;
            if (!credentialTypeLabel.Equals(CredentialTypes.JwtBearerCode, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    request.Password = PasswordGenerator.RandomPassword();
                }
                passwordHash = BetterPasswordHasher.HashPassword(request.Password);
            }
            var credential = await credentialRepository.Create(principal, credentialType, request.Username!, passwordHash, request.Description, context.RequestAborted);
            if (credential is null)
            {
                return TypedResults.Conflict(new ProblemDetails() { Title = "Credential creation failed" });
            }
            var response = credential.ToCreateResponse(request.Password);
            switch (request.Template)
            {
                case UserCredentialCreateTemplate.ApplicationKey:
                    {
                        if (string.IsNullOrEmpty(userConstraints.Value.ApplicationKeySecret))
                        {
                            return TypedResults.UnprocessableEntity(new ProblemDetails() { Title = "Server setup doesn't allow application keys" });
                        }
                        response.Secret = await ApplicationKeyRepository.CreateTokenAsync(userConstraints.Value, request.Username, request.Password, context.RequestAborted);
                    }
                    break;

                case UserCredentialCreateTemplate.JwtBearer:
                    response.Secret = null;
                    break;

                default:
                    break;
            }
            // TODO: Build safe URI
            return TypedResults.Created($"/api/user/{credential.Usr?.Username}/{credential.Accesskey}", response);
        })
        .WithName("CreateCredential")
        .RequireAuthorization()
        .WithSummary("Create (additional) credential for an user account")
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status409Conflict, ApplicationProblemJson)
        .ProducesProblem(StatusCodes.Status400BadRequest, ApplicationProblemJson)
        ;

        api.MapPatch("/autolink", async Task<Results<Ok, NoContent, NotFound, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (
            CredentialRepository credentialRepository, UserRepository userRepository, HttpContext context) =>
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

        api.MapPatch("/link/{sub}", async Task<Results<Ok, NoContent, NotFound, ForbidHttpResult, UnprocessableEntity<ProblemDetails>, BadRequest<ProblemDetails>>> (
            string sub, [FromBody, Required] UserCredentialLoginRequest request,
            CredentialRepository credentialRepository, UserRepository userRepository, HttpContext context) =>
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
            var credentialAuto = CredentialRepository.BuildJwtBearerCredential(user, context.User.Identity.Name, issuer);
            user.Credentials.Add(credentialAuto);
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
