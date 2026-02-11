using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the POST method.
/// </summary>
/// <remarks>
/// (Non-)specification of the POST at https://datatracker.ietf.org/doc/html/rfc4918#section-9.5
///
/// For the actual implemented feature:
///
/// Text/Calendar handlers:
///     - To add members see https://datatracker.ietf.org/doc/html/rfc5995
///     - For free busy requests see https://datatracker.ietf.org/doc/html/rfc6638#section-5
///
/// XML handlers:
///     - For WebDav Push subscriptions see https://github.com/bitfireAT/webdav-push
/// </see>.
/// </remarks>
public partial class PostHandler : HandlerBase, IMethodHandler
{
    private readonly ResourceRepository ResourceRepository;
    private readonly ItemRepository ItemRepository;
    private readonly SchedulingRepository SchedulingRepository;
    private readonly UserRepository UserRepository;

    private readonly ICalendarBuilder CalendarBuilder;

    // TODO: Move to developer configuration (global registry)
    private Dictionary<XName, Func<HttpContext, DavResource, XDocument, Task>> PostXmlHandlers { get; } = [];

    public PostHandler(DavEnvironmentRepository env, RecorderSession recorder, UserRepository userRepository, ResourceRepository resourceRepository, ItemRepository itemRepository, SchedulingRepository schedulingRepository, ICalendarBuilder calendarBuilder) : base(env, recorder)
    {
        ResourceRepository = resourceRepository;
        ItemRepository = itemRepository;
        SchedulingRepository = schedulingRepository;
        CalendarBuilder = calendarBuilder;
        UserRepository = userRepository;
        PostXmlHandlers.Add(XmlNs.Bitfire + "push-register", PushRegisterRequest);
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
        {
            Log.Warning("Content type missing or invalid", request.ContentType);
        }
        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
            // case DavResourceType.Principal:
            case DavResourceType.CalendarItem:
            case DavResourceType.AddressbookItem:
                // case DavResourceType.Addressbook:
                Log.Error("POST on this resource type {uri} not supported", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;
            default:
                break;
        }
        switch (contentType?.MediaType ?? MimeContentTypes.VCalendar)
        {
            case MimeContentTypes.Xml:
            case MimeContentTypes.XmlAlternate:
                await PostXml(httpContext, resource);
                return;

            case MimeContentTypes.VCalendar:
                await PostTextCalendar(httpContext, resource);
                return;

            default:
                if (resource.Current is not null)
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-calendar-data", $"Incorrect content type for calendar: {contentType?.MediaType}");
                    return;
                }
                break;
        }
        Log.Error("POST on this collection type {uri} {collectionType} not supported", request.GetEncodedUrl(), resource.Current?.CollectionType.ToString("g"));
        await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
    }

    private async Task PostTextCalendar(HttpContext httpContext, DavResource resource)
    {
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Write))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.WriteContent);
            return;
        }
        var request = httpContext.Request;
        if (resource.Current is not null)
        {
            if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
            {
                await SchedulingOutboxFreeBusy(httpContext, resource);
                return;
            }
            if (resource.Current.CollectionType == CollectionType.Calendar)
            {
                // https://datatracker.ietf.org/doc/html/rfc5995 to add members
                await AddCalendarItem(httpContext, resource);
                return;
            }
        }
        Log.Error("POST on this collection type {uri} {collectionType} with media type text/calendar not supported", request.GetEncodedUrl(), resource.Current?.CollectionType.ToString("g"));
        await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
    }

    private async Task PostXml(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var (xmlRequestDoc, _) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlRequestDoc is null || xmlRequestDoc?.Root is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest, "POST (application/xml) root element missing or malformed");
            return;
        }
        Recorder.SetRequestBody(xmlRequestDoc);
        if (PostXmlHandlers.TryGetValue(xmlRequestDoc.Root.Name, out var postHandlerType))
        {
            await postHandlerType(httpContext, resource, xmlRequestDoc);
            return;
        }
        await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "supported-report", $"\"{xmlRequestDoc.Root.Name}\" is not supported by post handler.");
    }
}
