using Calendare.Server.Models;
using Calendare.VSyntaxReader.Components;

namespace Calendare.Server.Calendar.Scheduling;

public class SchedulingItem
{
    public required string Email { get; set; }
    public required VCalendar Calendar { get; set; }    // TODO: Use VCalendarUnique?
    public DavResource? Resource { get; set; }
    public string? EmailFrom { get; set; }
    public bool IsResolved { get; set; }
    public bool IsDelete { get; set; }
}
