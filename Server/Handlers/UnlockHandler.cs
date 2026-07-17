using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the UNLOCK method.
///
/// This is a empty implementation to fullfil standard requirements. No locking or unlocking is taking place.
/// </summary>
/// <remarks>
/// The specification of the UNLOCK method can be found in <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.11"></see>.
/// </remarks>
public partial class UnlockHandler : HandlerBase, IMethodHandler
{

    public UnlockHandler(DavEnvironmentRepository env, RecorderSession recorder) : base(env, recorder)
    {
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
                // case DavResourceType.Calendar:
                // case DavResourceType.Addressbook:
                Log.Error("UNLOCK on this resource type {uri} not supported", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;

            default:
                break;
        }
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Read | PrivilegeMask.Write))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
            return;
        }
        // TODO: Lock-Token header (to get LOCK, e.g. Lock-Token: <urn:uuid:a515cfa4-5da4-22e1-f5b5-00a0451e6bf7>)
        await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
    }

}
