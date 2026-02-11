namespace Calendare.Server.Calendar.Scheduling;

public class SchedulingEMailItem
{
    public required string Uid { get; set; }
    public required string EmailFrom { get; set; }
    public required string EmailTo { get; set; }
    // public required VCalendar Calendar { get; set; }
    public required string Body { get; set; }
    public int Sequence { get; set; }
}
