using System;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{

    // https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.2.3
    private async Task<SchedulingItem?> AttendeeUpdate(HttpContext httpContext, DavResource resource, CollectionObject origin, VCalendarUnique currentCalendar, VCalendarUnique? beforeCalendar)
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
        if (beforeCalendar is null)
        {
            Log.Error("Update without a previous version?");
            throw new Exception("Update without a previous version?");
        }
        if (!ApplyAllowedAttendeeChanges(currentCalendar, attendeePrincipal.Email, beforeCalendar))
        {
            Log.Error("TODO: Should send an error CALDAV:allowed-attendee-scheduling-object-change");
            throw new Exception("TODO: Should send an error CALDAV:allowed-attendee-scheduling-object-change");
        }
        origin.UpdateWith(currentCalendar, false);
        var replyCalendar = currentCalendar.Builder.CreateCalendar();
        replyCalendar.Method = "REPLY";
        EventParticipationStatus participationStatus = EventParticipationStatus.NeedsAction;
        foreach (var mc in currentCalendar.EnumOccurrences())
        {
            var attendee = mc.Attendees.Get(attendeePrincipal.Email);
            if (attendee is null)
            {
                continue;   // TODO: can this happen that current user is not mentioned in the attendee list?
            }

            // Lookup attendee in original calendar to compute changes
            var beforeOccurrence = beforeCalendar?.FindAttendee(mc.RecurrenceId, attendeePrincipal.Email);
            // Check if changes are allowed and relevant ... (currently only PARTSTAT is checked)
            if (beforeOccurrence is null || beforeOccurrence.Value.Attendee is null || (beforeOccurrence.Value.Attendee is not null &&
            (attendee.ParticipationStatus.Value != beforeOccurrence.Value.Attendee.ParticipationStatus.Value && attendee.ParticipationStatus.Value != participationStatus))
            )
            {
                var replyComponent = replyCalendar.CreateChild(mc.GetType()) as RecurringComponent ?? throw new InvalidOperationException(nameof(RecurringComponent));
                //replyComponent.RequestStatus = "2.0;Success";
                replyComponent.MergeWith(ReplyProperties, mc);
                replyComponent.Attendees.Add(attendee.Copy());
                // cleanup organizer schedulestatus
                var replyOrganizer = replyComponent.Organizer;
                if (replyOrganizer is not null) { replyOrganizer.ScheduleStatus = null; }
                var mcOrganizer = mc.Organizer;
                if (mcOrganizer is not null) { mcOrganizer.ScheduleStatus = ScheduleStatus.Success; } // TODO: ???
                                                                                                      // https://datatracker.ietf.org/doc/html/rfc6638#appendix-B.4
                                                                                                      // REQUEST-STATUS:2.0;Success
                origin.UpdateWith(currentCalendar, false);
            }
            if (mc == currentCalendar.Reference)
            {
                participationStatus = attendee.ParticipationStatus.Value ?? EventParticipationStatus.NeedsAction;
            }
        }
        if (currentCalendar.Reference is not null)
        {
            // Handling of added EXDATE -> Send DECLINE for a single occurrence to organizer
            var exdates = currentCalendar.Reference.ExceptionDates.Dates;
            var beforeExdates = beforeCalendar?.Reference?.ExceptionDates?.Dates?.Select(z => z.ToInstant()).ToList();
            if (exdates is not null)
            {
                var newExdates = beforeExdates is null ? exdates : [.. exdates.Where(x => beforeExdates.Contains(x.ToInstant()))];
                var attendee = currentCalendar.Reference.Attendees.Get(attendeePrincipal.Email)?.Copy();
                if (attendee is not null && newExdates is not null && newExdates.Count != 0)
                {
                    attendee.ParticipationStatus.Value = EventParticipationStatus.Declined;
                    foreach (var exdate in newExdates)
                    {
                        var replyComponent = replyCalendar.CreateChild(currentCalendar.Reference.GetType()) as RecurringComponent ?? throw new InvalidOperationException(nameof(RecurringComponent));
                        //replyComponent.RequestStatus = "2.0;Success";
                        replyComponent.MergeWith(ReplyOccurrenceProperties, currentCalendar.Reference);
                        replyComponent.RecurrenceId = exdate;
                        replyComponent.Attendees.Add(attendee);
                    }
                }
            }
        }
        if (replyCalendar.Children.Count > 0)
        {
            origin.UpdateWith(currentCalendar, false);
            var inboxReply = new SchedulingItem
            {
                Email = currentCalendar.Organizer!.Value,
                EmailFrom = attendeePrincipal.Email,
                Calendar = replyCalendar,
            };
            await UpdateToInboxLocalDelivery(httpContext, inboxReply, organizerPrincipal);
            return inboxReply;
        }
        return null;
    }

    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.2.1
    /// </summary>
    /// <param name="myEmail"></param>
    /// <param name="currentCalendar"></param>
    /// <param name="beforeCalendar"></param>
    /// <returns></returns>
    private static bool ApplyAllowedAttendeeChanges(VCalendarUnique currentCalendar, string myEmail, VCalendarUnique beforeCalendar)
    {
        // var participationStatus = EventParticipationStatus.NeedsAction;
        var lcrc = new ListComparer<RecurringComponent>(beforeCalendar.EnumOccurrences(), currentCalendar.EnumOccurrences(), new RecurringComponentInstanceComparer());
        foreach (var cc in lcrc.Values)
        {
            switch (cc.Status)
            {
                case ListItemState.Both:
                    if (cc.Source is not null)
                    {
                        cc.Source.MergeWith(ProtectedProperties, cc.Target);
                        if (!ApplyParticipationChanges(cc.Source, myEmail, cc.Target))
                        {
                            return false;
                        }
                    }
                    break;
                case ListItemState.RightOnly:
                    // TODO: Check if added occurrence is allowed/valid. Only synthetic occurrences with the same participants are valid.
                    break;
                case ListItemState.LeftOnly:    // we preserve all pre-existing occurrences
                    currentCalendar.Calendar.AddChild(cc.Target);
                    break;
                case ListItemState.Unknown:
                default:
                    break;
            }
        }
        return true;
    }

    private static bool ApplyParticipationChanges(RecurringComponent currentComponent, string myEmail, RecurringComponent? beforeComponent)
    {
        var participationStatus = EventParticipationStatus.NeedsAction;
        var lct = new ListComparer<AttendeeProperty>(currentComponent.Attendees.Value ?? [], beforeComponent?.Attendees.Value ?? [], new AttendeeEmailComparer());
        foreach (var ai in lct.Values)
        {
            AttendeeProperty? attendee;
            AttendeeProperty? targetAttendee = null;
            switch (ai.Status)
            {
                case ListItemState.Both:
                    attendee = ai.Source!.Copy();  // we use the original item
                    targetAttendee = ai.Target;
                    break;
                case ListItemState.RightOnly: // possible with new occurrences
                    attendee = ai.Target.Copy();
                    break;
                case ListItemState.LeftOnly: // attendee is not allowed to change
                default:
                    return false;
            }
            if (attendee is not null)
            {
                if (attendee.ScheduleAgent.Value == ScheduleAgent.Client)
                {
                    if (targetAttendee is not null)
                    {
                        attendee.ScheduleStatus = targetAttendee.ScheduleStatus;
                        attendee.ParticipationStatus.Value = targetAttendee.ParticipationStatus.Value;
                    }
                }
                if (attendee.Value.Equals(myEmail, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    if (targetAttendee is not null)
                    {
                        if (participationStatus == EventParticipationStatus.NeedsAction && currentComponent.RecurrenceId is null)
                        {
                            if (targetAttendee.ParticipationStatus.Value != attendee.ParticipationStatus.Value)
                            {
                                participationStatus = targetAttendee.ParticipationStatus.Value ?? EventParticipationStatus.NeedsAction;
                            }
                        }
                        if (participationStatus == EventParticipationStatus.NeedsAction)
                        {
                            attendee.ParticipationStatus.Value = targetAttendee.ParticipationStatus.Value;
                        }
                    }
                    if (participationStatus != EventParticipationStatus.NeedsAction)
                    {
                        attendee.ParticipationStatus.Value = participationStatus;
                    }
                }
                currentComponent.Attendees.Add(attendee); // we replace with the modified original item
            }
            else
            {
                Log.Error("Attendee {attendeeEmail} mismatch in {uid} {recurrenceId}", myEmail, currentComponent.Uid, currentComponent.RecurrenceId);
                return false;
            }
        }
        return true;
    }
}
