using System.Collections.Generic;
using System.Linq;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Properties;

namespace Calendare.Server.Calendar.Scheduling;

public static class SchedulingExtensions
{
    public static RecurringComponent CleanSchedulingInternals(this RecurringComponent recurringComponent)
    {
        foreach (var attendee in recurringComponent.Attendees.Value)
        {
            attendee.ScheduleStatus = null;
        }
        return recurringComponent;
    }


    public static VCalendar CleanSchedulingInternals(this VCalendar vCalendar)
    {
        foreach (var ce in vCalendar.Children.OfType<RecurringComponent>())
        {
            CleanSchedulingInternals(ce);
        }
        return vCalendar;
    }

    public static VCalendarUnique ResetParticipationStatus(this VCalendarUnique vCalendar, HashSet<string> protectedAttendees)
    {
        foreach (var ce in vCalendar.EnumOccurrences())
        {
            foreach (var attendee in ce.Attendees.Value)
            {
                if (attendee.ScheduleAgent.Value is null || attendee.ScheduleAgent.Value == ScheduleAgent.Server)
                {
                    if (attendee.Value is not null && !protectedAttendees.Contains(attendee.Value))
                    {
                        attendee.ScheduleStatus = null;
                        attendee.ParticipationStatus.Value = EventParticipationStatus.NeedsAction;
                    }
                }
            }
        }
        return vCalendar;
    }
}
