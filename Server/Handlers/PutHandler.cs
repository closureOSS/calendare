using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
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
/// Implementation of the PUT method.
/// </summary>
/// <remarks>
/// The specification of the PUT method can be found in the
/// TODO <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_MKCOL">
/// CalDav specification
/// </see>.
/// </remarks>
public partial class PutHandler : HandlerBase, IMethodHandler
{
    private readonly ItemRepository ItemRepository;
    private readonly SchedulingRepository SchedulingRepository;
    private readonly CollectionRepository CollectionRepository;
    private readonly ICalendarBuilder CalendarBuilder;

    public PutHandler(DavEnvironmentRepository env, CollectionRepository collectionRepository, ItemRepository itemRepository, SchedulingRepository schedulingRepository, ICalendarBuilder calendarBuilder, RecorderSession recorder) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
        ItemRepository = itemRepository;
        SchedulingRepository = schedulingRepository;
        CalendarBuilder = calendarBuilder;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
        {
            Log.Warning("Content type missing or invalid", request.ContentType);
        }
        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
                Log.Error("PUT on this resource type {uri} not supported", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;
            default:
                break;
        }
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Write))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.WriteContent);
            return;
        }
        if (resource.ResourceType == DavResourceType.Calendar ||
            (resource.ResourceType == DavResourceType.Container && resource.ParentResourceType == DavResourceType.Principal && string.Equals(contentType?.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase))
        )
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-calendar-data", $"Incorrect content type for calendar: {contentType.MediaType}");
                return;
            }
            await AmendCalender(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.CalendarItem || string.Equals(contentType?.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase))
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-calendar-data", $"Incorrect content type for calendar: {contentType.MediaType}");
                return;
            }
            // add single calendar item
            await AmendCalenderItem(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.Addressbook || (resource.ResourceType == DavResourceType.Container && resource.ParentResourceType == DavResourceType.Principal && string.Equals(contentType?.MediaType, MimeContentTypes.VCard, StringComparison.Ordinal)))
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCard, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-address-data", $"Incorrect content type for addressbook: {contentType.MediaType}");
                return;
            }
            await AmendAddressbook(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.AddressbookItem || string.Equals(contentType?.MediaType, MimeContentTypes.VCard, StringComparison.OrdinalIgnoreCase))
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCard, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-address-data", $"Incorrect content type for addressbook: {contentType.MediaType}");
                return;
            }
            // the parent must exist
            // the resource type of the parent must be Calendar
            if (resource is null || resource.Parent is null || resource.ParentResourceType != DavResourceType.Addressbook)
            {
                // https://datatracker.ietf.org/doc/html/rfc4918#section-9.7.1
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
                return;
            }
            // add single vcard addressbook item
            await AmendAddressbookItem(httpContext, resource);
            return;
        }
        if (resource.ParentResourceType == DavResourceType.Principal || resource.ParentResourceType == DavResourceType.Root)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden, "A principal collection may only contain collections.");
            return;
        }
        {
            // TODO: Which cases are to be handled here at all

            string? bodyContent;
            try
            {
                bodyContent = await request.BodyAsStringAsync(httpContext.RequestAborted);
            }
            catch (InvalidDataException ex)
            {
                Log.Error(ex, "Failed to decode");
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.UnsupportedMediaType, XmlNs.Dav + "content-encoding", "Unable to decode 'xxx' content encoding.");
                return;
            }
            Recorder.SetRequestBody(bodyContent);

            // unknown content type
            if (resource.Parent?.CollectionType == CollectionType.Calendar)
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Caldav + "supported-calendar-data", $"Incorrect content type for calendar: {contentType?.MediaType}");
                return;
            }
            if (resource.Parent?.CollectionType == CollectionType.Addressbook)
            {
                //   $request->PreconditionFailed(412,'urn:ietf:params:xml:ns:carddav:supported-address-data',
                //   translate('Incorrect content type for addressbook: ') . $request->content_type );
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-address-data", $"Incorrect content type for addressbook: {contentType?.MediaType}");
                return;
            }
            // if(parent.IsPrincipal)
            // {
            //     response.StatusCode = (int)HttpStatusCode.Forbidden;
            //     return;
            // }

            Log.Error("Handling of content type {contentType} not supported", contentType?.MediaType);
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
        }

        await WriteStatusAsync(httpContext, HttpStatusCode.NotImplemented);
    }

    private async Task<DbOperationCode> VerifyOperation(HttpContext httpContext, DavResource resource, DavResourceRef resourceOriginal, CollectionObject? collectionObject)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (resource.Parent is null)
        {
            if (resource is null || resource.Parent is null)
            {
                // https://datatracker.ietf.org/doc/html/rfc4918#section-9.7.1
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
                return DbOperationCode.Failure;
            }
        }
        if (collectionObject is null)
        {
            // TODO: Check status code (parsing of body failed)
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return DbOperationCode.Failure;
        }
        collectionObject.Collection ??= resource.Parent;
        var ifmatch = request.GetIfMatch();
        if (ifmatch is not null)
        {
            var existingEtag = resourceOriginal.DavEtag;
            if (existingEtag is null || !string.Equals(existingEtag, ifmatch, StringComparison.Ordinal))
            {
                if (existingEtag is not null)
                {
                    SetEtagHeader(response, existingEtag);
                }
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-match", $"Existing resource Etag of \"{ifmatch}\" does not match \"{existingEtag}\"");
                return DbOperationCode.Failure;
            }
        }
        var ifmatchSchedule = request.GetIfScheduleTagMatch();
        if (ifmatchSchedule is not null)
        {
            var existingEtag = resourceOriginal.ScheduleTag;
            if (existingEtag is null || !string.Equals(existingEtag, ifmatchSchedule, StringComparison.Ordinal))
            {
                if (existingEtag is not null)
                {
                    SetScheduleHeader(response, existingEtag);
                }
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-match", $"Existing resource schedule tag of \"{ifmatchSchedule}\" does not match \"{existingEtag}\"");
                return DbOperationCode.Failure;
            }
        }
        return string.IsNullOrEmpty(resourceOriginal.DavEtag) ? DbOperationCode.Insert : DbOperationCode.Update;
    }


    private async Task AmendCollectionObject(HttpContext httpContext, DbOperationCode? opCode, CollectionObject collectionObject)
    {
        try
        {
            switch (opCode)
            {
                case DbOperationCode.Insert:
                    await ItemRepository.CreateAsync(collectionObject, httpContext.RequestAborted);
                    await WriteStatusAsync(httpContext, HttpStatusCode.Created);
                    break;

                case DbOperationCode.Update:
                    await ItemRepository.UpdateAsync(collectionObject, httpContext.RequestAborted);
                    await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                    break;

                default:
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Update {davName} failed", collectionObject.Uri);
            await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            return;
        }
        return;
    }
}
