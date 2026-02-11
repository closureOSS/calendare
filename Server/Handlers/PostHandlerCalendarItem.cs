using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Calendare.Server.Calendar;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class PostHandler : HandlerBase, IMethodHandler
{
    /// <summary>
    /// Add member https://datatracker.ietf.org/doc/html/rfc5995
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="resource"></param>
    /// <returns></returns>
    private async Task AddCalendarItem(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
        {
            Log.Warning("Content type missing or invalid", request.ContentType);
        }
        if (contentType is not null && !string.Equals(contentType.MediaType, MimeContentTypes.VCalendar, StringComparison.Ordinal))
        {
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "supported-calendar-data", $"Incorrect content type for calendar: {contentType.MediaType}");
            return;
        }
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
        var parseResult = CalendarBuilder.Parser.TryParse(bodyContent, out var vCalendar);
        if (!parseResult || vCalendar is null)
        {
            Log.Error("Parsing of request body text/calendar failed {errMsg}", parseResult.ErrorMessage);
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
            return;
        }

        var vCalendarUnique = new VCalendarUnique(vCalendar);
        if (!vCalendarUnique.IsValid || string.IsNullOrEmpty(vCalendarUnique.Uid))
        {
            Log.Error("Calendar contains multiple unrelated components");
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Caldav + "valid-calendar-object-resource", "Calendar contains multiple unrelated components");
            return;
        }
        // TODO: try to load resource via UID ... should return nothing
        // var targetContext = await ResourceRepository.GetByUidAsync(httpContext, vCalendarUnique.Uid, resource.Owner.UserId, CollectionType.Calendar, httpContext.RequestAborted);
        var calendarItemContext = await ResourceRepository.GetResourceAsync(new($"/{resource.DavName}/{vCalendarUnique.Uid}.ics"), httpContext, httpContext.RequestAborted);
        if (calendarItemContext.Exists)
        {
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "duplicate", $"Uid exists \"{vCalendarUnique.Uid}\" already");
            return;
        }
        var collectionObject = vCalendarUnique.CreateCollectionObject(calendarItemContext, bodyContent);
        if (collectionObject is null)
        {
            // TODO: Check error status code
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        collectionObject.Collection ??= resource.Current!;
        try
        {
            await SchedulingRepository.Put(this, httpContext, DbOperationCode.Insert, resource, collectionObject, vCalendarUnique, null);
            return;
        }
        catch (Exception e)
        {
            Recorder.SetResponse(e);
            Log.Error(e, "Update {davName} failed", collectionObject.Uri);
            //await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            throw;
        }
    }
}
