using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6638#section-4.2
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="resource"></param>
    /// <param name="opCode"></param>
    /// <param name="origin"></param>
    /// <param name="currentCalendar"></param>
    /// <param name="beforeCalendar"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<SchedulingItem?> AttendeeScheduling(HttpContext httpContext, DavResource resource, DbOperationCode opCode, CollectionObject origin, VCalendarUnique currentCalendar, VCalendarUnique? beforeCalendar)
    {
        if (origin.CalendarItem is null || currentCalendar is null || currentCalendar.Builder is null)
        {
            throw new ArgumentNullException($"{origin.Uri} {origin.Uid} with no calendar data");
        }
        var organizer = currentCalendar.Organizer;
        if (organizer is null || organizer.ScheduleAgent.Value != ScheduleAgent.Server)
        {
            // organizer required with agent 'SERVER' ->  no scheduling possible
            return null;
        }
        if (!httpContext.Request.GetDoScheduleReply())
        {
            // user (attendee) doesn't want any scheduling replies to be sent --> don't schedule as requested
            return null;
        }
        return opCode switch
        {

            DbOperationCode.Insert => await AttendeeCreate(httpContext, resource, origin, currentCalendar),
            DbOperationCode.Update => await AttendeeUpdate(httpContext, resource, origin, currentCalendar, beforeCalendar),
            DbOperationCode.Delete => await AttendeeCancel(httpContext, resource, currentCalendar),
            _ => null,
        };
    }

    private static SchedulingItem CreateInboxReply(RecurringComponent referenceComponent, AttendeeProperty attendeeSelf, string emailTo)
    {
        var replyCalendar = referenceComponent.Builder!.CreateCalendar();
        replyCalendar.Method = "REPLY";
        var replyComponent = replyCalendar.CreateChild(referenceComponent.GetType()) as RecurringComponent ?? throw new ArgumentNullException(nameof(referenceComponent));
        replyComponent.MergeWith(ReplyProperties, referenceComponent);
        replyComponent.Attendees.Add(attendeeSelf);
        var inboxReply = new SchedulingItem
        {
            Email = emailTo,
            EmailFrom = attendeeSelf.Value,
            Calendar = replyCalendar,
        };
        return inboxReply;
    }

    private async Task<bool> UpdateToInboxLocalDelivery(HttpContext httpContext, SchedulingItem inboxReply, Principal? principal, string? resourceItemName = null)
    {
        if (principal is not null)
        {
            // LOCAL Delivery
            var inboxObject = await ResourceRepository.GetResourceAsync(new($"/{principal.Uri}/{CollectionUris.CalendarInbox}/{(string.IsNullOrEmpty(resourceItemName) ? $"{Guid.NewGuid()}.ics" : resourceItemName)}"), httpContext, httpContext.RequestAborted);
            //   if (inboxContext is null || inboxContext.Parent is null || inboxContext.ParentResourceType != DavResourceType.Container
            //                            || inboxContext.Parent.CollectionType != CollectionType.SchedulingInbox
            //                            )
            //     {
            //         // TODO: Error, scheduling not possible, internal setup not correct (or permissions missing?)
            //         Log.Error("Internal scheduling not possible, setup possible not correct or permissions missing");
            //         attendee.ScheduleStatus = ScheduleStatus.InsufficientPrivileges;
            inboxReply.Resource = inboxObject;
            return true;
        }
        return false;
    }

    // https://datatracker.ietf.org/doc/html/rfc5546#section-3.2.3 for Events
    // https://datatracker.ietf.org/doc/html/rfc5546#section-3.4.3 for Todos
    private static readonly List<string> ReplyProperties = [
        PropertyName.DateStamp,
        PropertyName.Organizer,
        PropertyName.RecurrenceId,
        PropertyName.Uid,
        PropertyName.Sequence,
        PropertyName.Created,
        PropertyName.DateEnd,
        PropertyName.DateStart,
        PropertyName.Duration,
        PropertyName.RecurrenceExceptionDate,
        PropertyName.LastModified,
        PropertyName.RecurrenceDate,
        PropertyName.RecurrenceRule,
        PropertyName.RecurrenceExceptionRule,
        PropertyName.RequestStatus,
        PropertyName.Status,
        PropertyName.TimeTransparency,
        // Todos
        PropertyName.Due,
        PropertyName.Completed,
        PropertyName.Percent,
    ];

    private static readonly List<string> ReplyOccurrenceProperties = [
        PropertyName.Uid,
        PropertyName.DateStamp,
        PropertyName.Created,
        PropertyName.LastModified,
        PropertyName.Organizer,
        PropertyName.Sequence,
        PropertyName.RequestStatus,
    ];

    private static readonly List<string> ProtectedProperties = [
        PropertyName.Uid,
        PropertyName.DateStart,
        PropertyName.DateEnd,
        PropertyName.DateStamp,
        PropertyName.Due,
        PropertyName.Duration,
        PropertyName.Organizer,
        PropertyName.RecurrenceRule,
        PropertyName.RecurrenceExceptionRule,
        PropertyName.RecurrenceDate,
        PropertyName.Sequence,
    ];
}
