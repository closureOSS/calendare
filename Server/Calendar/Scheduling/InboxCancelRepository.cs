using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    // Cancel message from organizer to an attendee
    private async Task<List<SchedulingItem>?> InboxCancel(HttpContext httpContext, SchedulingItem msg)
    {
        if (msg.Resource is null || msg.Calendar.Builder is null) return null;
        var targetAttendee = msg.Resource.Owner;
        if (targetAttendee is null)
        {
            Log.Error("Attendee not defined -> setup incorrect, can't apply cancel");
            return null;
        }
        var cancelCalendar = new VCalendarUnique(msg.Calendar);
        if (!cancelCalendar.IsValid || cancelCalendar.IsEmpty || string.IsNullOrEmpty(cancelCalendar.Uid))
        {

            Log.Error("Cancel calendar invalid or empty --> don't know how to apply anything");
            return null;
        }
        var targetContext = await ResourceRepository.GetByUidAsync(httpContext, cancelCalendar.Uid, targetAttendee.UserId, CollectionType.Calendar, CollectionSubType.Default, httpContext.RequestAborted);
        if (targetContext is null || !targetContext.Exists || targetContext.Object is null)
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-4.2
            // If the corresponding scheduling object resource cannot be found, the server SHOULD ignore the scheduling message.
            Log.Warning("Target calendar {username} calendar item {uid} not found; nothing to do for a delete", targetAttendee.Username, cancelCalendar.Uid);
            return null;
        }
        if (!cancelCalendar.Builder.Parser.TryParse(targetContext.Object.RawData, out var targetCalendarRaw) || targetCalendarRaw is null)
        {
            Log.Error("Organizer's {username} calendar {uid} failed to load", targetAttendee.Username, cancelCalendar.Uid);
            return null;
        }
        var targetCalendar = new VCalendarUnique(targetCalendarRaw);
        if (cancelCalendar.Reference is not null)
        {
            // cancel WHOLE scheduling object -> DELETE
            // TODO: Remove all inbox items related to this object?!
            return [
                new SchedulingItem
                {
                    Calendar = targetCalendar.Calendar,
                    Email = targetAttendee.Email!,
                    Resource = targetContext,
                    IsResolved = true,
                    IsDelete = true,
                }
            ];
        }
        else
        {
            // cancel one or more occurrences -> UPDATE existing
            //
            if (targetCalendar.Reference is null)
            {
                // we are invited to just some occurrences -> and now delete them ...
                return [
                    new SchedulingItem
                    {
                        Calendar = targetCalendar.Calendar,
                        Email = targetAttendee.Email!,
                        Resource = targetContext,
                        IsResolved = true,
                        IsDelete = true,
                    }
                ];
            }
            else
            {
                // - OR - we are invited to all occurrences (possibly with exceptions) --> now add more exceptions
                var exceptionDates = cancelCalendar.EnumOccurrences().Select(c => c.RecurrenceId!).ToList();
                targetCalendar.Reference?.ExceptionDates.AddRange(exceptionDates);
                targetCalendar.Calendar.RemoveChildren<RecurringComponent>(rc => rc.Uid is not null && rc.RecurrenceId is not null && exceptionDates.Contains(rc.RecurrenceId));
                return [
                    new SchedulingItem
                {
                    Calendar = targetCalendar.Calendar,
                    Email = targetAttendee.Email!,
                    Resource = targetContext,
                    IsResolved = true,
                }
                ];
            }
        }
    }
}
