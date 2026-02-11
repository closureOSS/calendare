using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class ObjectCalendarAttendee
{
    public int Id { get; set; }

    public ObjectCalendar Calendar { get; set; } = null!;
    public int CalendarId { get; set; }

    public string? Status { get; set; }
    public string? Role { get; set; }
    public bool? Rsvp { get; set; }
    // PARTSTAT https://datatracker.ietf.org/doc/html/rfc5546#section-2.1.1
    public string? ParticipationStatus { get; set; }
    public string? CommonName { get; set; }
    public string? EMail { get; set; }
    public string? Language { get; set; }
    public string? EMailState { get; set; }
    public string? AttendeeState { get; set; }
    public string? AttendeeType { get; set; }

    // SCHEDULE-AGENT https://datatracker.ietf.org/doc/html/rfc6638#section-7.1
    // SERVER, CLIENT or NONE (null is SERVER)
    public string? ScheduleAgent { get; set; }

    // https://datatracker.ietf.org/doc/html/rfc5546#section-2.1.5
    public int? LastSequence { get; set; }
    public Instant? LastDtStamp { get; set; }

    public int? AttendeeId { get; set; }
    public Usr? Attendee { get; set; }

    /*
    $qry->Bind(':attendee', $attendee );
    $qry->Bind(':status',   $v->GetParameterValue('STATUS') );
    $qry->Bind(':partstat', $v->GetParameterValue('PARTSTAT') );
    $qry->Bind(':cn',       $v->GetParameterValue('CN') );
    $qry->Bind(':role',     $v->GetParameterValue('ROLE') );
    $qry->Bind(':rsvp',     $v->GetParameterValue('RSVP') );
    $qry->Bind(':property', $v->Render() )
    */
    // see also schedule-functions.php 

    public Instant? Created { get; set; }
    public Instant? Modified { get; set; }
}


public class ObjectCalendarAttendeeConfiguration : IEntityTypeConfiguration<ObjectCalendarAttendee>
{
    public void Configure(EntityTypeBuilder<ObjectCalendarAttendee> builder)
    {
        builder.HasOne(c => c.Attendee).WithMany().IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(c => c.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
    }
}
