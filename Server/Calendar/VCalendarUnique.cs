using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Models;
using Calendare.VSyntaxReader.Properties;
using NodaTime;
using Serilog;

namespace Calendare.Server.Calendar;

public class VCalendarUnique
{
    public VCalendar Calendar { get; init; }
    public bool IsValid { get; init; }
    public bool IsEmpty { get; init; }
    public bool HasAttendees { get; init; }
    public string? Uid { get; init; }
    public ICalendarComponent? Component { get; init; }
    public RecurringComponent? Reference { get; init; }
    public ReadOnlyDictionary<Instant, RecurringComponent> Occurrences => Occurrences_.AsReadOnly();
    private Dictionary<Instant, RecurringComponent> Occurrences_ { get; } = [];
    public bool HasOccurrences { get; init; }
    public CalendarBuilder Builder => Calendar.Builder ?? throw new InvalidOperationException("Calendar.Builder undefined");
    public OrganizerProperty? Organizer { get; init; }

    public VCalendarUnique(VCalendar vCalendar)
    {
        Calendar = vCalendar;
        Reference = null;
        Organizer = null;
        IsValid = HasAttendees = HasOccurrences = false;
        if (vCalendar.TryGetUniqueId(out var uid))
        {
            Uid = uid;
            IsValid = true;
            IsEmpty = true;
            var components = !string.IsNullOrEmpty(Uid) ? vCalendar.GetRecurringComponents(Uid) : vCalendar.Children.OfType<RecurringComponent>();
            if (components is not null && components.Any())
            {
                Reference = components.FirstOrDefault(x => x.RecurrenceId is null);
                Organizer = Reference?.Organizer;
                foreach (var c in components)
                {
                    if (HasAttendees == false && c.Attendees.Value.Count > 0)
                    {
                        HasAttendees = true;
                    }
                    Organizer ??= c.Organizer;  // pick up the first organizer if not set on reference component
                    if (c.RecurrenceId is not null)
                    {
                        Occurrences_[c.RecurrenceId.ToInstant() ?? Instant.MaxValue] = c;
                    }
                    else if (Reference != c)
                    {
                        IsValid = false;
                        Occurrences_[c.DateStart?.ToInstant() ?? Instant.MaxValue] = c;
                        Log.Error("TODO: RecurrenceId is missing, but it's not the reference component");
                    }
                }
                IsEmpty = false;
                HasOccurrences = Occurrences_.Count > 0;
            }
            else
            {
                // TODO: Handle non-recurring component VAVAILABILITY
                var availabilities = vCalendar.Children.OfType<VAvailability>();
                if (availabilities is not null && availabilities.Count() == 1)
                {
                    IsEmpty = false;
                    Component = availabilities.First();
                }
            }
            IsValid = IsValid && (Reference is not null || HasOccurrences || Component is not null);
        }
        // TODO: Investigate if this should be extended to non-recurring components such as VAVAILABILITY, VPOLL, ...
    }

    public RecurringComponent? FindOccurrence(CaldavDateTime? recurrenceId)
    {
        var id = recurrenceId?.ToInstant();
        if (id is null)
        {
            return Reference;
        }
        if (Occurrences.TryGetValue(id.Value, out var rc))
        {
            return rc;
        }
        return null;
    }

    public (RecurringComponent? Occurrence, AttendeeProperty? Attendee) FindAttendee(CaldavDateTime? recurrenceId, string? attendeeEmail)
    {
        var instance = FindOccurrence(recurrenceId);
        if (instance is null || string.IsNullOrEmpty(attendeeEmail))
        {
            return (instance, null);
        }
        return (instance, instance.Attendees.Get(attendeeEmail));
    }

    public IEnumerable<RecurringComponent> EnumOccurrences()
    {
        if (Reference is not null)
        {
            yield return Reference;
        }
        foreach (var rc in Occurrences.Values)
        {
            yield return rc;
        }
    }

    public AttendeeProperty? EnsureSingleAttendee()
    {
        AttendeeProperty? attendee = null;
        if (HasAttendees)
        {
            if (Reference is not null)
            {
                if (Reference.Attendees.Value.Count == 1)
                {
                    attendee = Reference.Attendees.Value[0];
                }
            }
            foreach (var occ in Occurrences.Values)
            {
                if (occ.Attendees.Value.Count != 1)
                {
                    return null;
                }
                attendee ??= occ.Attendees.Value[0];
                if (attendee is not null && !attendee.Value.Equals(occ.Attendees.Value[0].Value))
                {
                    return null;
                }
            }
        }
        return attendee;
    }
}
