using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    // Modify https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.1.2
    private async Task<List<SchedulingItem>?> OrganizerUpdate(HttpContext httpContext, DavResource resource, Principal organizerPrincipal, CollectionObject origin, VCalendarUnique currentCalendar, VCalendarUnique beforeCalendar)
    {
        if (string.IsNullOrEmpty(organizerPrincipal.Email)) throw new InvalidOperationException("Organizer e-mail is required");
        // TODO: https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.8
        //       If DTSTART, DTEND, ... changes on an existing recurrence instance all attendee PARTSTAT must be set to NEEDS-ACTION
        List<SchedulingItem> result = [];
        var isCoreChanged = IsCoreSchedulingChanged(currentCalendar, beforeCalendar);
        if (isCoreChanged)
        {
            currentCalendar.ResetParticipationStatus([organizerPrincipal.Email]);
        }
        HashSet<string> notifiedAttendees = [organizerPrincipal.Email];
        var lcrc = new ListComparer<RecurringComponent>(currentCalendar.EnumOccurrences(), beforeCalendar.EnumOccurrences(), new RecurringComponentInstanceComparer());
        foreach (var rc in lcrc.Values.Where(ai => ai.Status != ListItemState.RightOnly))
        {
            bool notifyAll = false;
            var lct = new ListComparer<AttendeeProperty>(rc.Target.Attendees.Value ?? [], rc.Source?.Attendees.Value ?? [], new AttendeeEmailComparer());
            foreach (var att in lct.Values.Where(ai => ai.Status == ListItemState.RightOnly))
            {
                var attendee = att.Target;
                if (notifiedAttendees.Add(attendee.Value) == false)
                {
                    continue;   // send just one CANCEL to attendee
                }
                var cancelRequest = await CreateCancelForAttendee(httpContext, attendee, organizerPrincipal, rc.Target, false);
                if (cancelRequest is not null)
                {
                    result.Add(cancelRequest);
                    notifyAll = notifyAll || rc.Target.RecurrenceId is null;
                }
            }
            foreach (var att in lct.Values.Where(ai => ai.Status != ListItemState.RightOnly))
            {
                var attendee = att.Target;
                if (notifyAll || att.Source is null || string.IsNullOrEmpty(attendee.ScheduleStatus))
                {
                    // Inform new attendee about event
                    var attendees = await FilterAttendeesToInvite(httpContext, [attendee], organizerPrincipal, [.. notifiedAttendees], notifyAll);
                    foreach (var attendeeToInvite in attendees)
                    {
                        var isNewAttendee = notifiedAttendees.Add(attendeeToInvite.Value);
                        var request = await CreateRequestForAttendee(httpContext, attendeeToInvite, organizerPrincipal.Email!, currentCalendar, isNewAttendee && att.Source is null ? resource.Uri.ItemName : null);
                        if (request is not null)
                        {
                            result.Add(request);
                        }
                    }
                }
            }
        }
        // check if attendees on some deleted occurrences need to be informed by CANCEL method
        foreach (var rc in lcrc.Values.Where(ai => ai.Status == ListItemState.RightOnly))
        {
            foreach (var attendee in rc.Target.Attendees.Value ?? [])
            {
                if (notifiedAttendees.Add(attendee.Value))
                {
                    var cancelRequest = await CreateCancelForAttendee(httpContext, attendee, organizerPrincipal, rc.Target, false);
                    if (cancelRequest is not null)
                    {
                        result.Add(cancelRequest);
                    }
                }
            }
        }
        if (result.Count > 0)
        {
            origin.UpdateWith(currentCalendar, isCoreChanged);
        }
        return result;
    }
}
