using System;
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
/// The specification of the PUT method can be found in <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.7"></see>.
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
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.SupportedCalendarData, $"Incorrect content type for calendar: {contentType.MediaType}");
                return;
            }
            await AmendCalender(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.CalendarItem || (resource.ParentResourceType == DavResourceType.Calendar && string.Equals(contentType?.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase)))
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCalendar, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.SupportedCalendarData, $"Incorrect content type for calendar: {contentType.MediaType}");
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
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.SupportedAddressData, $"Incorrect content type for addressbook: {contentType.MediaType}");
                return;
            }
            await AmendAddressbook(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.AddressbookItem || string.Equals(contentType?.MediaType, MimeContentTypes.VCard, StringComparison.OrdinalIgnoreCase))
        {
            if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCard, StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.SupportedAddressData, $"Incorrect content type for addressbook: {contentType.MediaType}");
                return;
            }
            // add single vcard addressbook item
            await AmendAddressbookItem(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.BlobItem || (resource.ParentResourceType == DavResourceType.Container && resource.ResourceType == DavResourceType.Unknown))
        {
            await AmendBlob(httpContext, resource);
            return;
        }
        if (resource.ResourceType == DavResourceType.Container)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.MethodNotAllowed, "Use MKCOL on collections");
            return;
        }
        switch (resource.ParentResourceType)
        {
            case DavResourceType.Principal:
            case DavResourceType.Root:
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden, "A principal collection may only contain collections.");
                return;

            default:
                Log.Error("TODO: Check what cases would trigger this error");
                await WriteStatusAsync(httpContext, HttpStatusCode.NotImplemented);
                // await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
                return;
        }
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
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.CollectionMustExist, "The destination collection does not exist.");
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
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfMatch, $"Existing resource Etag of \"{ifmatch}\" does not match \"{existingEtag}\"");
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
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfMatch, $"Existing resource schedule tag of \"{ifmatchSchedule}\" does not match \"{existingEtag}\"");
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
