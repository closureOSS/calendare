using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;


namespace Calendare.Server.Api;

public static partial class AdministrationApi
{
    public const string ApplicationProblemJson = "application/problem+json";

    private static async Task<(Principal? Principal, Principal? CurrentUserPrincipal)> TryGetAuthorizedPrincipal(UserRepository userRepository, IIdentity? identity, PrivilegeMask accessRights, CancellationToken ct) => await TryGetAuthorizedPrincipal(userRepository, identity, "", accessRights, ct);

    private static async Task<(Principal? Principal, Principal? CurrentUserPrincipal)> TryGetAuthorizedPrincipal(UserRepository userRepository, IIdentity? identity, string? principalName, PrivilegeMask accessRights, CancellationToken ct)
    {
        var currentUserPrincipal = await userRepository.GetCurrentUserPrincipalAsync(identity, ct);
        if (currentUserPrincipal is null)
        {
            return (null, null);
        }
        if (string.IsNullOrEmpty(principalName))
        {
            return (currentUserPrincipal, currentUserPrincipal);
        }
        var principal = await userRepository.GetPrincipalAsync(principalName, ct);
        if (principal is null)
        {
            return (null, currentUserPrincipal);
        }
        if (currentUserPrincipal.UserId != StockPrincipal.Admin && currentUserPrincipal.Id != principal.Id)
        {
            var privileges = await userRepository.CheckPrivilegeAsync(principal, currentUserPrincipal, ct);
            if (!privileges.HasAnyOf(accessRights))
            {
                return (null, currentUserPrincipal);
            }
        }
        return (principal, currentUserPrincipal);
    }

    private static async Task<(Collection? collection, DavResource? resource)> TryGetAuthorizedCollection(ResourceRepository resourceRepository, CollectionRepository collectionRepository, string uri, PrivilegeMask accessRights, HttpContext context, CancellationToken ct)
    {
        var resource = await resourceRepository.GetResourceAsync(new CaldavUri(uri), context, context.RequestAborted);
        if (!resource.Privileges.HasAnyOf(accessRights))
        {
            return (null, null);
        }
        if (resource.Exists == false || resource.Uri is null || resource.Uri.Path is null)
        {
            return (null, resource);
        }
        var collection = await collectionRepository.GetAsync(resource.Uri.Path, context.RequestAborted);
        if (collection is null)
        {
            // throw new InvalidOperationException("Something failed");
            return (null, resource);
        }
        return (collection, resource);
    }
}
