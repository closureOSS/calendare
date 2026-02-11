using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Calendar;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class PutHandler : IMethodHandler
{
    // https://datatracker.ietf.org/doc/html/rfc4791#section-5.3.2
    private async Task AmendCalenderItem(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;

        // the parent must exist
        // the resource type of the parent must be Calendar
        if (resource is null || resource.Parent is null ||
            (resource.ResourceType != DavResourceType.CalendarItem
            && resource.ParentResourceType != DavResourceType.Calendar))
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-9.7.1
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
            return;
        }
        var resourceOriginal = resource.ToLight();
        var ifNoneMatch = request.GetIfNoneMatch();
        if (ifNoneMatch && resource.Object is not null)
        {
            Log.Error("URI {uri} is already mapped (If-None-Match)", resource.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-none-match", "Existing resource matches 'If-None-Match' header - not accepted.");
            return;
        }
        await ItemRepository.LoadCalendarObject(resource.Object, httpContext.RequestAborted);

        string? bodyContent;
        try
        {
            bodyContent = await request.BodyAsStringAsync(httpContext.RequestAborted);
        }
        catch (InvalidDataException ex)
        {
            Log.Error(ex, "Failed to decode {contentEncoding}", request.Headers.ContentEncoding);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.UnsupportedMediaType, XmlNs.Dav + "content-encoding", $"Unable to decode '{request.Headers.ContentEncoding}' content encoding.");
            return;
        }
        Recorder.SetRequestBody(bodyContent);

        var originalBodyContent = resource.Object?.RawData;
        var parseResult = CalendarBuilder.Parser.TryParse(bodyContent, out var vCalendar, $"{httpContext.Request.GetFullPath()}");
        if (!parseResult || vCalendar is null)
        {
            Log.Error("Parsing of request body text/calendar failed {errMsg}", parseResult.ErrorMessage);
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
            return;
        }
        var vCalendarUnique = new VCalendarUnique(vCalendar);
        if (!vCalendarUnique.IsValid)
        {
            Log.Error("Calendar contains multiple unrelated components");
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Caldav + "valid-calendar-object-resource", "Calendar contains multiple unrelated components");
            return;
        }
        VCalendarUnique? vPreviousCalendar = null;
        if (!string.IsNullOrEmpty(originalBodyContent))
        {
            var parseBeforeResult = CalendarBuilder.Parser.TryParse(originalBodyContent, out var vPreviousCalendarWork);
            if (!parseBeforeResult || vPreviousCalendarWork is null)
            {
                Log.Error("Failed to parse previous version {errMsg}", parseBeforeResult.ErrorMessage);
                await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
                return;
            }
            vPreviousCalendar = new VCalendarUnique(vPreviousCalendarWork);
        }
        var collectionObject = vCalendarUnique.CreateCollectionObject(resource, bodyContent);
        var opCode = await VerifyOperation(httpContext, resource, resourceOriginal, collectionObject);
        if ((opCode == DbOperationCode.Update || opCode == DbOperationCode.Insert) && collectionObject is not null && collectionObject.CalendarItem is not null)
        {
            try
            {
                await SchedulingRepository.Put(this, httpContext, opCode, resource, collectionObject, vCalendarUnique, vPreviousCalendar);
                return;
            }
            catch (Exception e)
            {
                Recorder.SetResponse(e);
                Log.Error(e, "Update {davName} failed", collectionObject.Uri);
                await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
                throw;
            }
        }
        if (opCode != DbOperationCode.Failure)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
        }
    }

    private async Task AmendCalender(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        // TODO: Reject PUT on non-empty calendar collection
        //
        // CalDAV does not define the result of a PUT on a collection.  We treat that as an import
        var bodyContent = await request.BodyAsStringAsync(httpContext.RequestAborted);
        Recorder.SetRequestBody(bodyContent);
        var parseResult = CalendarBuilder.Parser.TryParse(bodyContent, out var vCalendar, $"{httpContext.Request.GetFullPath()}");
        if (!parseResult || vCalendar is null)
        {
            Log.Error("Failed to parse {errMsg}", parseResult.ErrorMessage);
            response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
            return;
        }
        if (resource.Current is null)
        {
            if (resource.ParentResourceType == DavResourceType.Principal ||
                (resource.Parent is not null && resource.ParentResourceType == DavResourceType.Container))
            {
                // create calendar
                var collection = new Calendare.Data.Models.Collection
                {
                    Uri = resource.Uri.Path!,
                    ParentContainerUri = $"{resource.Uri.ParentCollectionPath}",
                    ParentId = resource.Parent?.Id ?? resource.Owner.Id,
                    DisplayName = vCalendar.CalendarName,
                    Description = vCalendar.CalendarDescription,
                    // ParentContainer = resource.Parent is not null ? resource.Parent.DavName : null,
                    // DavDisplayName = resource.Uri.Collection,
                    CollectionType = CollectionType.Calendar,
                    Etag = $"{resource.Owner.Id}{resource.Uri.Path!}".PrettyMD5Hash(),
                    OwnerId = resource.Owner.UserId,
                };
                resource.Current = await CollectionRepository.CreateAsync(collection, httpContext.RequestAborted);
            }
            if (resource.Current is null)
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
                return;
            }
        }
        else
        {
            // TODO: Check if CalendarName/Description/Timezone/... should be updated
        }
        var parser = new CalendarSplitter(resource.Owner, resource.CurrentUser, resource.Uri);
        var collectionObjects = parser.Split(vCalendar);
        collectionObjects.ForEach(x => x.Collection = resource.Current);
        await ItemRepository.CreateAsync(collectionObjects, httpContext.RequestAborted);
        response.StatusCode = (int)HttpStatusCode.Created;
    }
}
