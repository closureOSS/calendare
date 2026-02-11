using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calendare.Server.Models;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    private async Task<List<SchedulingItem>?> OrganizerCancel(HttpContext httpContext, Principal organizerPrincipal, VCalendarUnique currentCalendar)
    {
        List<SchedulingItem> result = [];
        if (currentCalendar.Reference is null)
        {
            // organizer copy has no reference?? -> nothing can be done
            return result;
        }
        HashSet<string> notifiedAttendees = [];
        foreach (var ce in currentCalendar.EnumOccurrences())
        {
            foreach (var attendee in ce.Attendees.Value)
            {
                if (notifiedAttendees.Add(attendee.Value) == false)
                {
                    continue;   // send just one CANCEL to attendee
                }
                var inboxRequest = await CreateCancelForAttendee(httpContext, attendee, organizerPrincipal, ce, true);
                if (inboxRequest is not null)
                {
                    result.Add(inboxRequest);
                }
            }
        }
        return result;
    }

    private async Task<SchedulingItem?> CreateCancelForAttendee(HttpContext httpContext, AttendeeProperty attendee, Principal organizerPrincipal, RecurringComponent ce, bool cancelAll)
    {
        if (attendee.ScheduleAgent.Value != ScheduleAgent.Server)
        {
            // do not schedule if agent is CLIENT, NONE or any other unknown value https://datatracker.ietf.org/doc/html/rfc6638#section-7.1
            return null;
        }
        var builder = (ce.Parent as VCalendar)?.Builder ?? throw new ArgumentNullException(nameof(ce));
        // if (attendee.ParticipationStatus.Value is not null && attendee.ParticipationStatus.Value != EventParticipationStatus.NeedsAction)
        // {
        //     return null;
        // }
        if (!Addressbook.TryGetValue(attendee.Value, out var attendeePrincipal))
        {
            attendeePrincipal = await UserRepository.GetPrincipalByEmailAsync(attendee.Value, httpContext.RequestAborted);
            if (attendeePrincipal is not null)
            {
                Addressbook.TryAdd(attendeePrincipal.Email!, attendeePrincipal);
            }
        }
        if (attendeePrincipal is not null && attendeePrincipal.UserId == organizerPrincipal.UserId)
        {
            // Skip attendee who is the organizer, don't notify ourself
            return null;
        }
        if (attendee.ParticipationStatus.HasValue == true && attendee.ParticipationStatus.Value == EventParticipationStatus.Declined)
        {
            return null;
        }

        var cancelCalendar = builder.CreateCalendar();
        cancelCalendar.Method = "CANCEL";
        var cancelComponent = cancelCalendar.CreateChild(ce.GetType()) as RecurringComponent ?? throw new InvalidOperationException(nameof(RecurringComponent));
        cancelComponent.MergeWith(CancelProperties, ce);
        var cancelAttendee = cancelComponent.Attendees.Add(attendee.Copy());
        cancelAttendee.ScheduleStatus = null;   //ScheduleStatus.Success;
        var cancelRequest = new SchedulingItem
        {
            Email = attendee.Value,
            EmailFrom = organizerPrincipal.Email,
            Calendar = cancelCalendar,
        };
        await UpdateToInboxLocalDelivery(httpContext, cancelRequest, attendeePrincipal);
        // else
        // {
        //     // REMOTE delivery by iMip mail
        //     attendee.ScheduleStatus = ScheduleStatus.Pending;   // TODO: ???
        // }
        return cancelRequest;
    }

    private readonly List<string> CancelProperties = [
        PropertyName.DateStart, PropertyName.DateEnd,
        PropertyName.DateStamp, PropertyName.Due, PropertyName.Duration,
        PropertyName.Completed,
        PropertyName.Uid, PropertyName.TimeTransparency,
        PropertyName.Created, PropertyName.LastModified,
        PropertyName.RecurrenceId, PropertyName.Organizer,
        PropertyName.Sequence, PropertyName.RequestStatus,
    ];
}
