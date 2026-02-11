using System;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.2.4
    /// and https://datatracker.ietf.org/doc/html/rfc6638#section-8.1 about HEADER "Schedule-Reply" 'T' or 'F' (default 'T') --> don't send DECLINED if set to 'F'
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="resource"></param>
    /// <param name="currentCalendar"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    private async Task<SchedulingItem?> AttendeeCancel(HttpContext httpContext, DavResource resource, VCalendarUnique currentCalendar)
    {
        var attendeePrincipal = resource.Owner;
        if (attendeePrincipal is null || attendeePrincipal.Email is null)
        {
            return null;
        }
        var organizerPrincipal = await UserRepository.GetPrincipalByEmailAsync(currentCalendar.Organizer?.Value, httpContext.RequestAborted);
        if (organizerPrincipal is not null)
        {
            // INTERNAL -> we should have the organizer's master calendar item
            var organizerContext = await ResourceRepository.GetByUidAsync(httpContext, resource.Object?.Uid, organizerPrincipal.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
            if (organizerContext is null || !organizerContext.Exists || organizerContext.Object is null)
            {
                // https://datatracker.ietf.org/doc/html/rfc6638#section-4.2
                // If the corresponding scheduling object resource cannot be found, the server SHOULD ignore the scheduling message.
                // This is done early - as we have access to the organizer scheduling object - instead of sending out inbox requests
                Log.Warning("Organizer's {organizer} calendar item {uid} not found", organizerPrincipal.Username, resource.Object?.Uid);
                return null;
            }
        }
        var referenceComponent = currentCalendar.Reference ?? currentCalendar.Occurrences.Values.FirstOrDefault();
        if (referenceComponent is null)
        {
            Log.Warning("Cancel on an empty calendar {uid}?", currentCalendar.Uid);
            return null;
        }
        var attendeeSelf = referenceComponent.Attendees.Get(attendeePrincipal.Email);
        if (attendeeSelf is null)
        {
            Log.Error("Attendee {attendee} (=self) not found", attendeePrincipal.Email);
            return null;
        }
        attendeeSelf.ParticipationStatus.Value = EventParticipationStatus.Declined;

        var inboxReply = CreateInboxReply(referenceComponent, attendeeSelf, currentCalendar.Organizer!.Value);
        await UpdateToInboxLocalDelivery(httpContext, inboxReply, organizerPrincipal);
        return inboxReply;
    }
}
