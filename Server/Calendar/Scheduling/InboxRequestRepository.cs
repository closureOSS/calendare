using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    private async Task<SchedulingItem?> InboxRequest(HttpContext httpContext, SchedulingItem inbox)
    {
        if (inbox.Resource is null) return null;
        var attendeePrincipal = inbox.Resource.Owner;
        var inboxCalendar = new VCalendarUnique(inbox.Calendar);
        if (inboxCalendar.IsEmpty || !inboxCalendar.IsValid)
        {
            // empty calendar --> nothing to do
            return null;
        }
        VCalendar? attendeeCalendar = null;
        var attendeeContext = await ResourceRepository.GetByUidAsync(httpContext, inboxCalendar.Uid, attendeePrincipal.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
        if (attendeeContext is not null && attendeeContext.Object is not null)
        {
            // exists already -> do merge
            var parseResult = inbox.Calendar.Builder!.Parser.TryParse(attendeeContext.Object.RawData, out var targetCalendar, $"{attendeeContext.Uri}");
            if (!parseResult || targetCalendar is null)
            {
                Log.Error("Failed to parse {errMsg}", parseResult.ErrorMessage);
                return null;
            }
            attendeeCalendar = targetCalendar.Copy();
            var lcrc = new ListComparer<RecurringComponent>(attendeeCalendar.Children.OfType<RecurringComponent>(), inboxCalendar.EnumOccurrences(), new RecurringComponentInstanceComparer());
            foreach (var ci in lcrc.Values)
            {
                switch (ci.Status)
                {
                    case ListItemState.Both:
                        ci.Target.MergeWith(InboxSyncProperties, ci.Source);
                        SyncAttendees(ci.Target.Attendees, ci.Source?.Attendees.Value);
                        break;
                    case ListItemState.RightOnly:
                        attendeeCalendar.AddChild(ci.Target);
                        break;
                    case ListItemState.LeftOnly:
                        if (ci.Target.RecurrenceId is not null)
                        {
                            attendeeCalendar.RemoveChildren<RecurringComponent>(rc => rc.Uid is not null && rc.Uid.Equals(ci.Target.Uid) && ci.Target.RecurrenceId.CompareTo(rc.RecurrenceId) == 0);
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        else
        {
            // doesn't exist -> do insert
            attendeeContext ??= await ResourceRepository.GetResourceAsync(new Middleware.CaldavUri($"/{attendeePrincipal.Uri}/{CollectionUris.DefaultCalendar}/{inbox.Resource.Uri.ItemName}"), httpContext, httpContext.RequestAborted);
            if (attendeeContext.Exists)
            {
                Log.Error("{uri} exists already with {uidExisting} instead of {uid}", attendeeContext.Uri, attendeeContext.Object?.Uid, inboxCalendar.Uid);
                return null;
            }
            attendeeCalendar = inbox.Calendar.Copy();
            attendeeCalendar.Method = null;
        }
        if (attendeeCalendar is null) throw new InvalidOperationException(nameof(attendeeCalendar));
        var calendarItem = new SchedulingItem
        {
            Email = inbox.Email,
            Calendar = attendeeCalendar,
            Resource = attendeeContext,
            IsResolved = true,
        };
        return calendarItem;
    }

    private static void SyncAttendees(AttendeePropertyList attendees, List<AttendeeProperty>? right)
    {
        var lct = new ListComparer<AttendeeProperty>(attendees.Value ?? [], right ?? [], new AttendeeEmailComparer());
        foreach (var ai in lct.Values)
        {
            switch (ai.Status)
            {
                case ListItemState.Both:
                    if (ai.Target.ScheduleAgent.Value == ScheduleAgent.Server)
                    {
                        ai.Target.ParticipationStatus.Value = ai.Source!.ParticipationStatus.Value;
                        ai.Target.ScheduleStatus = null;
                        attendees.Add(ai.Target);
                    }
                    break;
                case ListItemState.RightOnly:
                    attendees.Add(ai.Target);
                    break;
                case ListItemState.LeftOnly:
                    attendees.Remove(ai.Target.Value);
                    break;
                default:
                    break;
            }
        }
    }
}
