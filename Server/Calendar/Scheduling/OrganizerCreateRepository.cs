using System.Collections.Generic;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    private async Task<List<SchedulingItem>?> OrganizerCreate(HttpContext httpContext, DavResource resource, Principal organizerPrincipal, CollectionObject origin, VCalendarUnique currentCalendar)
    {
        List<SchedulingItem> result = [];
        HashSet<string> toNotifyList = [];
        if (currentCalendar.Reference is not null)
        {
            var attendees = await FilterAttendeesToInvite(httpContext, currentCalendar.Reference.Attendees.Value, organizerPrincipal, [], false);
            foreach (var attendee in attendees)
            {
                var inboxRequest = await CreateRequestForAttendee(httpContext, attendee, organizerPrincipal.Email!, currentCalendar, resource.Uri.ItemName);
                if (inboxRequest is not null)
                {
                    result.Add(inboxRequest);
                    toNotifyList.Add(attendee.Value);
                }
            }
        }
        foreach (var ce in currentCalendar.Occurrences.Values)
        {
            var attendees = await FilterAttendeesToInvite(httpContext, ce.Attendees.Value, organizerPrincipal, [.. toNotifyList], false);
            foreach (var attendee in attendees)
            {
                // search for all occurrences with this attendee
                // create custom request with just the found occurrences (check RFC for further details)
                var inboxRequest = await CreateRequestForAttendee(httpContext, attendee, organizerPrincipal.Email!, currentCalendar, resource.Uri.ItemName);
                if (inboxRequest is not null)
                {
                    result.Add(inboxRequest);
                    toNotifyList.Add(attendee.Value);
                }
            }
        }
        origin.UpdateWith(currentCalendar, true);
        return result;
    }
}
