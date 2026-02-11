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
    // https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.2.2
    // Special case where an Attendee creates the scheduling object (e.g. from an e-mail)
    private async Task<SchedulingItem?> AttendeeCreate(HttpContext httpContext, DavResource resource, CollectionObject origin, VCalendarUnique currentCalendar)
    {
        var attendeePrincipal = resource.Owner;
        if (attendeePrincipal is null || attendeePrincipal.Email is null)
        {
            return null;
        }
        if (currentCalendar.Organizer is null || currentCalendar.Organizer.Value is null)
        {
            // no organizer email address found --> no group scheduling
            return null;
        }
        var organizerPrincipal = await UserRepository.GetPrincipalByEmailAsync(currentCalendar.Organizer?.Value, httpContext.RequestAborted);
        if (organizerPrincipal is not null)
        {
            var organizerContext = await ResourceRepository.GetByUidAsync(httpContext, resource.Object?.Uid, organizerPrincipal.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
            if (organizerContext is not null && organizerContext.Object is not null)
            {
                if (!currentCalendar.Builder.Parser.TryParse(organizerContext.Object.RawData, out var organizerCalendar))
                {
                    Log.Error("Organizer's {organizer} calendar {uid} failed to load", organizerPrincipal.Username, resource.Object?.Uid);
                    return null;
                }
            }
            else
            {
                // TODO: No organizer scheduling object exists (possibly deleted, attendee responding to old request) --> what is the correct behaviour? reject?!
                Log.Warning("Organizer scheduling object {uid} doesn't exist", currentCalendar.Uid);
                return null;
            }
        }
        var referenceComponent = currentCalendar.Reference ?? currentCalendar.Occurrences.Values.FirstOrDefault();
        if (referenceComponent is null)
        {
            Log.Error("Empty calendar {uid}?", currentCalendar.Uid);
            return null;
        }
        var attendeeSelf = referenceComponent.Attendees.Get(attendeePrincipal.Email);
        if (attendeeSelf is null)
        {
            Log.Error("Attendee with email {attendee} not found in calendar {uid}", attendeePrincipal.Email, currentCalendar.Uid);
            return null;

        }
        if (attendeeSelf.ParticipationStatus.Value is null || attendeeSelf.ParticipationStatus.Value == EventParticipationStatus.NeedsAction)
        {
            // participation status is Needs-Action --> no reply is necessary
            origin.UpdateWith(currentCalendar, true);   // true --> should not matter as it's the first event
            return null;
        }

        var inboxReply = CreateInboxReply(referenceComponent, attendeeSelf, currentCalendar.Organizer!.Value);
        await UpdateToInboxLocalDelivery(httpContext, inboxReply, organizerPrincipal);
        currentCalendar.Organizer.ScheduleStatus = ScheduleStatus.Success;
        origin.UpdateWith(currentCalendar, true);  // true --> should not matter as it's the first event
        return inboxReply;
    }
}
