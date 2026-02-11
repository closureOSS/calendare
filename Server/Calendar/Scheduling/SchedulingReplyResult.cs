using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Properties;

namespace Calendare.Server.Calendar.Scheduling;

public class SchedulingReplyResult
{
    public bool IsModified { get; set; }
    public RecurringComponent? OrganizerComponent { get; set; }
    public EventParticipationStatus PreviousParticipationStatus { get; set; }
    public EventParticipationStatus ParticipationStatus { get; set; }

}
