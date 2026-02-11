using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    private async Task<List<SchedulingItem>?> InboxReply(HttpContext httpContext, SchedulingItem msg)
    {
        if (msg.Resource is null || msg.Calendar.Builder is null) return null;
        var organizer = msg.Resource.Owner;
        if (organizer is null)
        {
            // organizer not defined -> setup incorrect, no scheduling possible
            return null;
        }
        if (msg.Resource.Object?.CalendarItem is not null)
        {
            msg.Resource.Object.CalendarItem.OrganizerId = organizer.UserId;
        }
        var inboxCalendar = new VCalendarUnique(msg.Calendar);
        if (inboxCalendar.IsEmpty || !inboxCalendar.HasAttendees || string.IsNullOrEmpty(inboxCalendar.Uid))
        {
            // empty calendar or no attendee --> nothing to do
            Log.Warning("Reply {uri} {uid} is empty or contains not attendee", msg.Resource.Uri, inboxCalendar.Uid);
            return null;
        }
        var attendee = inboxCalendar.EnsureSingleAttendee();
        if (attendee is null)
        {
            Log.Error("Reply {uri} {uid} must contain exactly one attendee", msg.Resource.Uri, inboxCalendar.Uid);
            return null;
        }

        var organizerContext = await ResourceRepository.GetByUidAsync(httpContext, inboxCalendar.Uid, organizer.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
        if (organizerContext is null || !organizerContext.Exists || organizerContext.Object is null)
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-4.2
            // If the corresponding scheduling object resource cannot be found, the server SHOULD ignore the scheduling message.
            Log.Warning("Organizer's {organizer} calendar item {uid} not found", organizer.Username, inboxCalendar.Uid);
            return null;
        }
        if (!msg.Calendar.Builder.Parser.TryParse(organizerContext.Object.RawData, out var organizerCalendarNative) || organizerCalendarNative is null)
        {
            Log.Error("Organizer's {organizer} calendar {uid} failed to load", organizer.Username, inboxCalendar.Uid);
            return null;
        }
        var organizerCalendar = new VCalendarUnique(organizerCalendarNative);
        // scenario R -> reference exists, zero occurrences
        // scenario O -> no reference, one occurrence
        // scenario M -> no reference, multiple occurrences (not yet seen)
        // scenario A -> reference and one or more occurrences (not yet seen)
        HashSet<string> toNotifyList = [];
        if (inboxCalendar.Reference is not null)    // scenarios R and A
        {
            var replyResult = ApplyReply(organizerCalendar, inboxCalendar.Reference, attendee);
            if (replyResult.IsModified && replyResult.OrganizerComponent is not null)
            {
                // apply participation status of attendee to all (synthetic) occurrences of the organizer calendar with the same part. state
                var changedOccurrences = ApplyParticipationStatusToOccurrences(organizerCalendar, attendee, replyResult.PreviousParticipationStatus, replyResult.ParticipationStatus);
                // notify other attendees about changes on the reference occurrence
                var add = DetectNotification([replyResult.OrganizerComponent, .. changedOccurrences], [organizer.Email!, attendee.Value]);
                toNotifyList.UnionWith(add);
            }
        }
        if (inboxCalendar.Occurrences.Count > 0)    // scenarios O, M and A
        {
            foreach (var mc in inboxCalendar.Occurrences.Values)
            {
                var replyResult = ApplyReply(organizerCalendar, mc, attendee);
                if (replyResult.IsModified && replyResult.OrganizerComponent is not null)
                {
                    // notify other attendees about changes on the this occurrence
                    var add = DetectNotification([replyResult.OrganizerComponent], [organizer.Email!, attendee.Value]);
                    toNotifyList.UnionWith(add);
                }
            }
        }
        organizerContext.Object.UpdateWith(organizerCalendar.Calendar, false);
        List<SchedulingItem> result = [
            new SchedulingItem
            {
                Calendar = organizerCalendar.Calendar,
                Email = organizer.Email!,
                Resource = organizerContext,
                IsResolved = true,
            }
        ];
        result.AddRange(await NotifyWithRequest(httpContext, organizerCalendar, organizer.Email!, toNotifyList?.ToArray()));
        return result;
    }

    /// <summary>
    /// Apply inbox reply to organizer scheduling object
    /// </summary>
    /// <param name="organizerCalendar"></param>
    /// <param name="replyComponent"></param>
    /// <returns></returns>
    private static SchedulingReplyResult ApplyReply(VCalendarUnique organizerCalendar, RecurringComponent replyComponent, AttendeeProperty attendee)
    {
        var result = new SchedulingReplyResult();
        // check if attendee's participation changes and also new EXDATE's (decline for one or more occurrences)
        var organizerOccurrence = organizerCalendar.FindOccurrence(replyComponent.RecurrenceId);
        if (organizerOccurrence is null && replyComponent.RecurrenceId is not null)
        {
            if (organizerCalendar.Calendar.IsValidRecurrenceDate(replyComponent.RecurrenceId))
            {
                organizerOccurrence = organizerCalendar.Calendar.AddNewOccurrence(replyComponent.RecurrenceId);
            }
            else
            {
                Log.Error("The recurrenceId {recurrenceId} is not valid in {uid}", replyComponent.RecurrenceId, organizerCalendar.Uid);
            }
        }
        if (organizerOccurrence is null)
        {
            Log.Error("We should have at least a copy-created occurrence in {uid} -> skipping", organizerCalendar.Uid);
            return result;
        }
        result.OrganizerComponent = organizerOccurrence;
        var organizerAttendee = organizerOccurrence.Attendees.Get(attendee.Value);
        if (organizerAttendee is null)
        {
            Log.Error("Attendee of reply for {uid}{recurrenceId} not found in organizer original {attendee}", replyComponent.Uid, replyComponent.RecurrenceId, attendee.Value);
            return result;   // attendee is not mentioned in the event instance -> skipping
        }
        var inboxAttendee = replyComponent.Attendees.Get(attendee.Value);
        if (inboxAttendee is null)
        {
            Log.Error("Attendee of reply for {uid}{recurrenceId} not found in inbox {attendee}", replyComponent.Uid, replyComponent.RecurrenceId, attendee.Value);
            return result;   // attendee is not mentioned in the event instance -> skipping
        }
        result.ParticipationStatus = inboxAttendee.ParticipationStatus.Value ?? EventParticipationStatus.NeedsAction;
        result.PreviousParticipationStatus = organizerAttendee.ParticipationStatus.Value ?? EventParticipationStatus.NeedsAction;
        if (result.ParticipationStatus == result.PreviousParticipationStatus)
        {
            return result; // participation status is unchanged -> skipping as nothing to do
        }
        organizerAttendee.ParticipationStatus.Value = inboxAttendee.ParticipationStatus.Value;
        organizerAttendee.ScheduleStatus = ScheduleStatus.Success;
        result.IsModified = true;
        return result;
    }

    /// <summary>
    /// Apply participation status of attendee to all (synthetic) occurrences of the organizer calendar with the same part. state
    /// </summary>
    /// <param name="organizerCalendar"></param>
    /// <param name="attendee"></param>
    /// <param name="previousStatus"></param>
    /// <param name="newStatus"></param>
    private static List<RecurringComponent> ApplyParticipationStatusToOccurrences(VCalendarUnique organizerCalendar, AttendeeProperty attendee, EventParticipationStatus previousStatus, EventParticipationStatus newStatus)
    {
        List<RecurringComponent> changedOccurrences = [];
        foreach (var occ in organizerCalendar.Occurrences.Values)
        {
            var occAttendee = occ.Attendees.Get(attendee.Value);
            if (occAttendee is null || occAttendee.ParticipationStatus.Value != previousStatus)
            {
                continue;
            }
            occAttendee.ParticipationStatus.Value = newStatus;
            occAttendee.ScheduleStatus = ScheduleStatus.Success;
            changedOccurrences.Add(occ);
        }
        return changedOccurrences;
    }

    /// <summary>
    /// Identify other attendees to notify about participation changes
    /// </summary>
    /// <param name="organizerOccurrence"></param>
    /// <param name="skipEmails"></param>
    /// <returns></returns>
    private static HashSet<string> DetectNotification(List<RecurringComponent> organizerOccurrences, List<string> skipEmails)
    {
        HashSet<string> toNotifyList = [];
        foreach (var occ in organizerOccurrences)
        {
            foreach (var notifyAttendee in occ.Attendees.Value)
            {
                if (!string.IsNullOrEmpty(notifyAttendee.Value) && !toNotifyList.Contains(notifyAttendee.Value))
                {
                    if (skipEmails.Contains(notifyAttendee.Value, System.StringComparer.Ordinal))
                    {
                        continue;
                    }
                    toNotifyList.Add(notifyAttendee.Value);
                }
            }
        }
        return toNotifyList;
    }

    private async Task<List<SchedulingItem>> NotifyWithRequest(HttpContext httpContext, VCalendarUnique organizerCalendar, string emailFrom, string[]? emails)
    {
        if (emails is null || emails.Length == 0)
        {
            return [];
        }
        List<SchedulingItem> result = [];
        foreach (var email in emails)
        {
            var schedulingItem = CreateRequestForAttendee(email, emailFrom, organizerCalendar);
            if (schedulingItem is not null)
            {
                // Check if LOCAL delivery possible
                var attendeePrincipal = await UserRepository.GetPrincipalByEmailAsync(email, httpContext.RequestAborted);
                if (attendeePrincipal is not null)
                {
                    var attendeeContext = await ResourceRepository.GetByUidAsync(httpContext, organizerCalendar.Uid, attendeePrincipal.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
                    if (attendeeContext is not null)
                    {
                        await UpdateToInboxLocalDelivery(httpContext, schedulingItem, attendeePrincipal);
                        result.Add(schedulingItem);  // we inform only local user about changes in participation, otherwise too many e-mails
                    }
                }
            }
        }
        return result;
    }
}
