using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the OPTIONS method.
/// </summary>
/// <remarks>
/// Report a class 1 and 3 compliant CalDAV server with all enabled features
/// </remarks>
public class OptionsHandler : HandlerBase, IMethodHandler
{
    private readonly List<string> SupportedMethods;

    public OptionsHandler(DavEnvironmentRepository env, RecorderSession recorder, IOptions<CaldavOptions> config) : base(env, recorder)
    {
        SupportedMethods = [.. config.Value.Handlers.Keys.Where(x => !x.Contains('#'))];
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var response = httpContext.Response;
        // response.Headers["MS-Author-Via"] = "DAV";
        SetCapabilitiesHeader(response);

        if (resource.ParentResourceType == DavResourceType.Unknown)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest, "The collection path is invalid.");
            return;
        }
        // Handle access to resources without privileges (simple case CurrentUser != Owner)
        if (!resource.Privileges.HasAnyOf())
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
            return;
        }
        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
                response.Headers.Allow = "OPTIONS, PROPFIND, REPORT";
                break;

            case DavResourceType.Principal:
            case DavResourceType.User:
                var principalMethods = SupportedMethods.Except(["MKCALENDAR", "MKCOL", "POST"], System.StringComparer.Ordinal);
                response.Headers.Allow = string.Join(", ", principalMethods);
                break;

            case DavResourceType.AddressbookItem:
            case DavResourceType.CalendarItem:
                var objectMethods = SupportedMethods.Except(["MKCALENDAR", "MKCOL", "POST", "PROPPATCH"], System.StringComparer.Ordinal);
                response.Headers.Allow = string.Join(", ", objectMethods);
                break;

            case DavResourceType.Calendar:
            case DavResourceType.Addressbook:
                var collectionMethods = SupportedMethods.Except(["MKCALENDAR", "MKCOL",], System.StringComparer.Ordinal);
                response.Headers.Allow = string.Join(", ", collectionMethods);
                break;

            case DavResourceType.Container:
                if (!resource.Exists)
                {
                    await WriteStatusAsync(httpContext, HttpStatusCode.NotFound, "No collection found at that location.");
                    return;
                }
                var containerMethods = SupportedMethods.Except(["POST", "PUT",], System.StringComparer.Ordinal);
                response.Headers.Allow = string.Join(", ", containerMethods);
                break;

            case DavResourceType.Unknown:
            default:
                if (!resource.Exists)
                {
                    await WriteStatusAsync(httpContext, HttpStatusCode.NotFound, "No collection found at that location.");
                    return;
                }
                response.Headers.Allow = string.Join(", ", SupportedMethods);
                break;
        }
        response.StatusCode = (int)HttpStatusCode.OK;
    }
}
