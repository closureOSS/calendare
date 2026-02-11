using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class ObjectCalendar
{
    public int Id { get; set; }

    public int CollectionObjectId { get; set; }
    public CollectionObject CollectionObject { get; set; } = null!;

    public Instant? Dtstart { get; set; }
    public Instant? Dtend { get; set; }
    public Instant? Due { get; set; }
    public Instant? Completed { get; set; }

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

    public int Sequence { get; set; }
    public bool IsScheduling { get; set; }
    public int? OrganizerId { get; set; }
    public Usr? Organizer { get; set; }

    public Instant? Dtstamp { get; set; }
    public Instant? Created { get; set; }
    public Instant? LastModified { get; set; }

    public Instant? FirstInstanceStart { get; set; }
    public Instant? FirstInstanceEnd { get; set; }
    public Instant? LastInstanceStart { get; set; }
    public Instant? LastInstanceEnd { get; set; }

    public ICollection<ObjectCalendarAttendee> Attendees { get; set; } = new List<ObjectCalendarAttendee>();
}


public class ObjectCalendarConfiguration : IEntityTypeConfiguration<ObjectCalendar>
{
    public void Configure(EntityTypeBuilder<ObjectCalendar> builder)
    {
        builder.HasMany(c => c.Attendees).WithOne(c => c.Calendar).IsRequired(true).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Organizer).WithMany().IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
