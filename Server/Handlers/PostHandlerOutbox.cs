using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Calendar;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Reports;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using NodaTime;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class PostHandler : HandlerBase, IMethodHandler
{
    /// <summary>
    /// Implementation of the POST method - Scheduling Outbox
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="resource"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    private async Task SchedulingOutboxFreeBusy(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

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
        var parseResult = CalendarBuilder.Parser.TryParse(bodyContent, out var vcal);
        if (!parseResult || vcal is null)
        {
            Log.Error("Parsing of request body text/calendar failed {errMsg}", parseResult.ErrorMessage);
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
            return;
        }
        AttendeePropertyList? attendeePropertyList = null;
        OrganizerProperty? organizer = null;
        var evalRange = new Interval();
        string uid = string.Empty;
        string? maskUid = null;
        if (vcal.Children.Where(c => c is VFreebusy).FirstOrDefault() is VFreebusy freeBusyRequest)
        {
            uid = freeBusyRequest.Uid;
            maskUid = freeBusyRequest.MaskUid;
            attendeePropertyList = freeBusyRequest.Attendees;
            organizer = freeBusyRequest.Organizer;
            evalRange = new Interval(freeBusyRequest.DateStart, freeBusyRequest.DateEnd);
        }
        else if (vcal.Children.Where(c => c is RecurringComponent).FirstOrDefault() is RecurringComponent recurringComponent)
        {
            if (recurringComponent.Uid is not null)
            {
                uid = recurringComponent.Uid;
            }
            attendeePropertyList = recurringComponent.Attendees;
            organizer = recurringComponent.Organizer;
            evalRange = recurringComponent.GetInterval(DateTimeZone.Utc);
        }
        else
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType, "VFREEBUSY or similar component not found");
            return;
        }
        var xmlScheduleResponse = new XElement(XmlNs.Caldav + "schedule-response");
        var xmlDoc = xmlScheduleResponse.CreateDocument();
        xmlDoc.Root!.Add(new XAttribute(XNamespace.Xmlns + XmlNs.DavPrefix, XmlNs.Dav));
        if (organizer is not null && attendeePropertyList is not null && attendeePropertyList.Value.Count > 0)
        {
            var organizerPrincipal = await UserRepository.GetPrincipalByEmailAsync(organizer.Value ?? string.Empty, httpContext.RequestAborted);
            foreach (var attendee in attendeePropertyList.Value)
            {
                var xmlResponse = new XElement(XmlNs.Caldav + "response");
                xmlScheduleResponse.Add(xmlResponse);
                //         var xmlRecipient = new XElement(XmlNs.Caldav + "recipient", new XElement(XmlNs.Dav + "href", attendee.Value.AbsoluteUri));
                var xmlRecipient = new XElement(XmlNs.Caldav + "recipient", new XElement(XmlNs.Dav + "href", $"mailto:{attendee.Value}"));
                xmlResponse.Add(xmlRecipient);
                // TODO: Identify attendee (Local/Remote, and if local check permission)
                // If local, and permission are okay, load calendar objects to build free busy report
                var attendeePrincipal = await UserRepository.GetPrincipalByEmailAsync(attendee.Value!, httpContext.RequestAborted);
                if (attendeePrincipal is not null)
                {
                    var freeBusyReply = CalendarBuilder.CreateCalendar();
                    freeBusyReply.Method = "REPLY";
                    var freebusyComponent = freeBusyReply.CreateChild<VFreebusy>() ?? throw new InvalidOperationException(nameof(freeBusyReply));
                    freebusyComponent.DateStamp = default;
                    freebusyComponent.Uid = uid;
                    freebusyComponent.DateStart = evalRange.Start;
                    freebusyComponent.DateEnd = evalRange.End;
                    if (maskUid is not null)
                    {
                        freebusyComponent.MaskUid = maskUid;
                    }
                    var attendeeLight = attendee.Copy();
                    attendeeLight.Raw.Parameters.Clear();
                    freebusyComponent.Properties.AddRange([organizer, attendeeLight]);
                    var query = new CalendarObjectQuery
                    {
                        CurrentUser = resource.CurrentUser,
                        OwnerId = attendeePrincipal.UserId,
                        Period = evalRange,
                        ExcludePrivate = attendeePrincipal.UserId != (organizerPrincipal?.UserId ?? -1),
                        ExcludeTransparent = true,
                        VObjectTypes = [ComponentName.VEvent, ComponentName.VAvailability],
                    };
                    var calendarObjects = await ItemRepository.ListCalendarObjectsAsync(query, httpContext.RequestAborted);
                    if (calendarObjects is not null && calendarObjects.Count > 0)
                    {
                        var calendarComponents = CalendarBuilder.LoadCalendars(calendarObjects);
                        var availabilities = await httpContext.LoadAvailabilityProperty(attendeePrincipal.UserId, httpContext.RequestAborted);
                        if (availabilities is not null)
                        {
                            calendarComponents.AddRange(availabilities);
                        }
#if DEBUG
                        var debugFreeBusyCalendar = CalendarBuilder.SerializeForDebug(calendarComponents);
#endif
                        var timezone = FreeBusyQueryReport.GetTimezoneForFloatingDates(attendeePrincipal, calendarObjects);
                        var freeBusyEntries = calendarComponents.GetFreeBusyEntries(evalRange, maskUid: maskUid, calendarTimeZone: timezone);
                        freebusyComponent.SetFreeBusyEntries(freeBusyEntries);
                    }
                    var xmlRequestStatus = new XElement(XmlNs.Caldav + "request-status", ScheduleStatus.Success);
                    xmlResponse.Add(xmlRequestStatus);
                    var xmlCalendarData = new XElement(XmlNs.Caldav + "calendar-data", freeBusyReply.Serialize());
                    xmlResponse.Add(xmlCalendarData);
                }
                else
                {
                    var xmlRequestStatus = new XElement(XmlNs.Caldav + "request-status", ScheduleStatus.UnknownRecipient);
                    xmlResponse.Add(xmlRequestStatus);
                }
            }
        }

        await response.BodyXmlAsync(xmlDoc, HttpStatusCode.MultiStatus, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }
}
