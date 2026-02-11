using NodaTime;

namespace Calendare.Server.Api.Models;

public class CalendarScheduleItem
{
    public string Uri { get; set; } = default!;
    public string Uid { get; set; } = default!;
    public string Etag { get; set; } = default!;
    public string? ScheduleTag { get; set; }
    public string? RawData { get; set; }
    public string? VObjectType { get; set; }
    public Instant Created { get; set; }
    public Instant Modified { get; set; }

    public Instant? LastModified { get; set; }
    public Instant? Dtstamp { get; set; }
    public Instant? Dtstart { get; set; }
    public Instant? Dtend { get; set; }
    public Instant? Due { get; set; }
    public string? Summary { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
    public string? Class { get; set; }
    public string? Transp { get; set; }
    public string? Rrule { get; set; }
    public string? Url { get; set; }
    public double PercentComplete { get; set; }
    public string? Timezone { get; set; }
    public string? Status { get; set; }
    public Instant? Completed { get; set; }
    public Instant? FirstInstanceStart { get; set; }
    public Instant? LastInstanceEnd { get; set; }
    public Instant? DtstartOrig { get; set; }
    public Instant? DtendOrig { get; set; }
}


