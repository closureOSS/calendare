using System;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Models;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using NodaTime;
using Serilog;

namespace Calendare.Server.Calendar;

public static class AttendeeExtensions
{
    public static RecurringComponent? AddNewOccurrence(this VCalendar vCalendar, CaldavDateTime recurrenceId)
    {
        var recurrenceDate = recurrenceId.ToInstant();
        var occurencesList = vCalendar.GetOccurrences(new Interval(recurrenceDate, recurrenceDate));
        if (occurencesList is null || occurencesList.Count == 0)
        {
            Log.Error("Not found attendee supplied occurrence {recurrenceId}", recurrenceId);
            return null;
        }
        var occurrenceItem = occurencesList[0];
        var sourceOccurrence = occurencesList[0].Source;
        var occurrence = sourceOccurrence.CopyTo(vCalendar) as RecurringComponent ?? throw new ArgumentNullException(nameof(vCalendar));
        occurrence.RemoveProperties([PropertyName.RecurrenceRule, PropertyName.RecurrenceDate, PropertyName.RecurrenceExceptionDate, PropertyName.RecurrenceExceptionRule]);
        occurrence.RecurrenceId = recurrenceId;
        occurrence.DateStart = new CaldavDateTime(occurrenceItem.Interval.Start.InZone(occurrenceItem.Source.DateStart?.Zone ?? DateTimeZone.Utc), occurrenceItem.Source.DateStart?.IsDateOnly ?? false);
        var dateEnd = occurrence.FindFirstProperty<DateTimeProperty>(PropertyName.DateEnd);
        if (dateEnd is not null)
        {
            dateEnd.Value = new CaldavDateTime(occurrenceItem.Interval.End.InZone(dateEnd?.Value?.Zone ?? DateTimeZone.Utc), occurrenceItem.Source.DateStart?.IsDateOnly ?? false);
        }
        else
        {
            var duration = occurrence.FindFirstProperty<DurationProperty>(PropertyName.Duration);
            if (duration is not null)
            {
                var durationInSeconds = Period.FromSeconds(Convert.ToInt64(occurrenceItem.Interval.Duration.TotalSeconds));
                var durationNormalized = durationInSeconds.Normalize();
                if (durationNormalized != duration.Value)
                {
                    duration.Value = durationNormalized;
                }
            }
        }

        return occurrence;
    }
}
