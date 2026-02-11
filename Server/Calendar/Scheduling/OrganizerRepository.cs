using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Models;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.1
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="resource"></param>
    /// <param name="opCode"></param>
    /// <param name="origin"></param>
    /// <param name="currentCalendar"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    private async Task<List<SchedulingItem>?> OrganizerScheduling(HttpContext httpContext, DavResource resource, DbOperationCode opCode, CollectionObject origin, VCalendarUnique currentCalendar, VCalendarUnique? beforeCalendar)
    {
        if (origin.CalendarItem is null || currentCalendar is null || currentCalendar.Builder is null)
        {
            throw new ArgumentNullException($"{origin.Uri} {origin.Uid} with no calendar data");
        }
        if (currentCalendar.Organizer is null)
        {
            return null;    // organizer not defined, no scheduling possible
        }
        if (currentCalendar.Organizer.ScheduleAgent.Value != ScheduleAgent.Server)
        {
            return null; // no server scheduling
        }
        var organizerPrincipal = await UserRepository.GetPrincipalByEmailAsync(currentCalendar.Organizer.Value, httpContext.RequestAborted);
        if (organizerPrincipal is null || organizerPrincipal.UserId != origin.OwnerId || organizerPrincipal.Email is null)
        {
            // user is not the organizer -> no organizer scheduling, trying later attendee scheduling
            // organizer not defined, no server scheduling or wrongly defined (e.g. missing email) -> no scheduling possible
            return null;
        }
        origin.CalendarItem.OrganizerId = organizerPrincipal.UserId;
        origin.CalendarItem.IsScheduling = true;
        Addressbook.TryAdd(organizerPrincipal.Email!, organizerPrincipal);
        return opCode switch
        {
            DbOperationCode.Insert => await OrganizerCreate(httpContext, resource, organizerPrincipal, origin, currentCalendar),
            DbOperationCode.Update => await OrganizerUpdate(httpContext, resource, organizerPrincipal, origin, currentCalendar, beforeCalendar!),
            DbOperationCode.Delete => await OrganizerCancel(httpContext, organizerPrincipal, currentCalendar),
            _ => null, // TODO: Are there other op codes in use?
        };
    }

    private bool IsCoreSchedulingChanged(VCalendarUnique current, VCalendarUnique before)
    {
        if (current.Reference is null || before.Reference is null)
        {
            Log.Warning("TODO: Reference NULL with organizer scheduling possible??");
            return false;
        }
        if (current.Reference.GetInterval(NodaTime.DateTimeZone.Utc) != before.Reference.GetInterval(NodaTime.DateTimeZone.Utc))
        {
            return true;
        }
        // Check if RecurrenceRule is different (on EACH? affected component)
        if (current.Reference.RecurrenceRule is not null && !current.Reference.RecurrenceRule.Equals(before.Reference.RecurrenceRule))
        {
            return true;
        }
        // TODO: Check if RecurrenceDates are different (on affected component)
        // TODO: Check if ExceptionDates are different (on affected component)
        if (current.Occurrences.Count != before.Occurrences.Count || !current.Occurrences.Keys.All(before.Occurrences.ContainsKey))
        {
            return true;
        }
        return false;
    }



    private async Task<List<AttendeeProperty>> FilterAttendeesToInvite(HttpContext httpContext, List<AttendeeProperty> attendees, Principal organizerPrincipal, List<string> excludeMails, bool force)
    {
        List<AttendeeProperty> result = [];
        foreach (var attendee in attendees)
        {
            if (attendee is null)
            {
                continue;
            }
            if (attendee.ScheduleAgent.Value != ScheduleAgent.Server)
            {
                // do not schedule if agent is CLIENT, NONE or any other unknown value https://datatracker.ietf.org/doc/html/rfc6638#section-7.1
                continue;
            }
            if (excludeMails.Contains(attendee.Value, StringComparer.Ordinal))
            {
                continue;
            }
            if (!force && attendee.ParticipationStatus?.Value is not null && attendee.ParticipationStatus.Value != EventParticipationStatus.NeedsAction)
            {
                continue;
            }
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
                continue;
            }
            result.Add(attendee);
        }
        return result;
    }

    private async Task<SchedulingItem?> CreateRequestForAttendee(HttpContext httpContext, AttendeeProperty attendee, string organizerEmail, VCalendarUnique vTemplateCalendar, string? resourceItemName)
    {
        // The handling of potential cancel or reply messages must be done independently (out of scope for this function)
        var inboxRequest = CreateRequestForAttendee(attendee.Value, organizerEmail, vTemplateCalendar);
        if (inboxRequest is not null)
        {
            Addressbook.TryGetValue(attendee.Value, out var attendeePrincipal);
            if (attendeePrincipal is not null)
            {
                var inboxResult = await UpdateToInboxLocalDelivery(httpContext, inboxRequest, attendeePrincipal, resourceItemName);
                if (inboxResult)
                {
                    attendee.ScheduleStatus = ScheduleStatus.Success;
                }
                else
                {
                    Log.Error("Internal scheduling not possible, setup possible not correct or permissions missing");
                    attendee.ScheduleStatus = ScheduleStatus.InsufficientPrivileges;
                    inboxRequest = null;
                }
            }
            else
            {
                // REMOTE delivery by iMip mail
                attendee.ScheduleStatus = ScheduleStatus.Pending;   // TODO: ???
            }
        }
        return inboxRequest;
    }

    private static SchedulingItem? CreateRequestForAttendee(string attendeeEmail, string organizerEmail, VCalendarUnique organizerCalendar)
    {
        var (isInReference, isServerScheduling, recurrences) = DetermineAttendance(organizerCalendar, attendeeEmail);
        if (!isServerScheduling)
        {
            return null;
        }
        VCalendar? inboxCalendar;
        // Check if attendee is in all instances of an recurring event --> yes: send inboxCalendar
        if (isInReference)
        {
            inboxCalendar = organizerCalendar.Calendar.Copy().CleanSchedulingInternals();
            // If attendee is not mentioned in all instances, but in the reference instance -> send inboxCalendar with EXDATE's set for the missing instances
            if (recurrences is not null && recurrences.Count > 0)
            {
                // remove these occurrences and define EXDATE...
                inboxCalendar.RemoveChildren<RecurringComponent>(c => c.RecurrenceId is not null && recurrences.Contains(c.RecurrenceId));
                var ibx = new VCalendarUnique(inboxCalendar);
                ibx.Reference?.ExceptionDates.AddRange(recurrences);
            }
        }
        else
        {
            // If attendee is mentioned only in one or more instances, but not in the reference instance -> send just the instances with RECURRENCE-ID set
            //       https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.6 mentions just "one" instance, not more; so send multiple requests or combine them --> combine them
            inboxCalendar = organizerCalendar.Builder.CreateCalendar();
            if (recurrences is null || recurrences.Count == 0)
            {
                return null; // doesn't exist
            }
            foreach (var recurrenceId in recurrences)
            {
                var templateOcc = organizerCalendar.FindOccurrence(recurrenceId);
                if (templateOcc is not null)
                {
                    templateOcc.CopyTo<RecurringComponent>(inboxCalendar).CleanSchedulingInternals();
                }
            }
        }
        inboxCalendar.Method = "REQUEST";

        var inboxRequest = new SchedulingItem
        {
            Email = attendeeEmail,
            EmailFrom = organizerEmail,
            Calendar = inboxCalendar,
        };
        return inboxRequest;
    }

    private static (bool InReference, bool IsServerScheduling, List<CaldavDateTime>? recurrences) DetermineAttendance(VCalendarUnique calendar, string attendeeEmail)
    {
        List<CaldavDateTime> invited = [];
        List<CaldavDateTime> excluded = [];
        bool invitedReference = false;
        bool serverScheduling = false;
        foreach (var occ in calendar.EnumOccurrences())
        {
            if (occ.Attendees.Get(attendeeEmail) is AttendeeProperty attendee)
            {
                if (attendee.ParticipationStatus?.HasValue == false)
                {
                    attendee.ParticipationStatus.Value = EventParticipationStatus.NeedsAction;
                }
                if (occ.RecurrenceId is null)
                {
                    invitedReference = true;
                }
                else
                {
                    invited.Add(occ.RecurrenceId);
                }
                if (attendee.ScheduleAgent is null || attendee.ScheduleAgent.Value == ScheduleAgent.Server)
                {
                    serverScheduling = true;
                }
            }
            else
            {
                if (occ.RecurrenceId is not null)
                {
                    excluded.Add(occ.RecurrenceId);
                }
            }
        }
        if (invitedReference)
        {
            return excluded.Count == 0 ? (invitedReference, serverScheduling, null) : (invitedReference, serverScheduling, excluded);
        }
        return (invitedReference, serverScheduling, invited);
    }
}
