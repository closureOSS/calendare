using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the GET and HEAD method.
/// </summary>
/// <remarks>
/// GET or HEAD method see https://datatracker.ietf.org/doc/html/rfc4918#section-9.4
/// </see>.
/// </remarks>
public partial class GetHandler : HandlerBase, IMethodHandler
{
    private readonly ItemRepository ItemRepository;
    private readonly ICalendarBuilder CalendarBuilder;

    public GetHandler(DavEnvironmentRepository env, ItemRepository itemRepository, ICalendarBuilder calendarBuilder, RecorderSession recorder) : base(env, recorder)
    {
        ItemRepository = itemRepository;
        CalendarBuilder = calendarBuilder;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var isHeadRequest = string.Equals(request.Method, "HEAD", System.StringComparison.Ordinal);
        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
                Log.Error("PUT/HEAD not supported on this resource type {uri}", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;
        }

        if (resource.ResourceType == DavResourceType.Calendar || resource.ResourceType == DavResourceType.CalendarItem)
        {
            if (!resource.Privileges.HasAnyOf(PrivilegeMask.Read))
            {
                await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
                return;
            }
            if (resource.ResourceType == DavResourceType.CalendarItem)
            {
                await GetCalendarItem(httpContext, resource, isHeadRequest);
            }
            else
            {
                await GetCalendar(httpContext, resource, isHeadRequest);
            }
        }
        else if (resource.ResourceType == DavResourceType.Addressbook || resource.ResourceType == DavResourceType.AddressbookItem)
        {
            if (!resource.Privileges.HasAnyOf(PrivilegeMask.Read))
            {
                await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
                return;
            }
            if (resource.ResourceType == DavResourceType.AddressbookItem)
            {
                await GetAddressbookItem(httpContext, resource, isHeadRequest);
            }
            else
            {
                await GetAddressbook(httpContext, resource, isHeadRequest);
            }
        }
        else
        {
            Log.Error("Resource type {resourceType} doesn't support GET requests or unknown", resource.ResourceType);
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
        }
    }
}
