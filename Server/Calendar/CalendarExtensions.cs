using System;
using System.Globalization;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Models;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using NodaTime;
using Serilog;

namespace Calendare.Server.Calendar;

public static class CalendarExtensions
{
    private static string? ReadTextProperty(ICalendarComponent component, string propertyName)
    {
        var prop = component.FindFirstProperty<TextMultilanguageProperty>(propertyName);
        if (prop is not null && prop.Value is not null)
        {
            return prop.Value.Value;
        }
        return null;
    }


    private static string? ReadStringProperty(ICalendarComponent component, string propertyName)
    {
        var prop = component.FindFirstProperty<IProperty>(propertyName);
        if (prop is not null && prop.Raw is not null)
        {
            return prop.Raw.Value;
        }
        return null;
    }

    private static int? ReadIntegerProperty(ICalendarComponent component, string propertyName)
    {
        var prop = component.FindFirstProperty<IProperty>(propertyName);
        if (prop is not null && prop.Raw is not null)
        {
            if (int.TryParse(prop.Raw.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
            {
                return num;
            }
        }
        return null;
    }

    private static Instant? ReadDateTimeProperty(ICalendarComponent component, string propertyName)
    {
        var prop = component.FindFirstProperty<DateTimeProperty>(propertyName);
        if (prop is not null && prop.Raw is not null)
        {
            return prop.Value?.ToInstant();
        }
        return null;
    }


    public static void ReadCommonProperties(this RecurringComponent? component, CollectionObject target)
    {
        if (component is null || target.CalendarItem is null)
        {
            return;
        }
        CollectCalendarItem(component, target.CalendarItem);
        switch (target.CalendarItem.Class)
        {
            case "CONFIDENTIAL":
                target.IsConfidential = true;
                break;
            case "PRIVATE":
                target.IsPrivate = true;
                break;
            case "PUBLIC":
                target.IsPublic = true;
                break;
            case null:
                break;
            default:
                Log.Warning("Event class {eventClass} unknown, ignoring", target.CalendarItem.Class);
                break;
        }
    }

    public static CollectionObject? CreateCollectionObject(this VCalendarUnique vCalendarUnique, DavResource resource, string? serializedCalendar = null)
    {
        resource.Object ??= new();
        var target = resource.Object;
        target.OwnerId = resource.Owner.UserId;
        target.ActualUserId = resource.CurrentUser.UserId;
        target.Uri = resource.Uri.Path!;
        target.CalendarItem ??= new();
        if (target.CollectionId == 0 && resource.Parent is not null)
        {
            target.CollectionId = resource.Parent.Id;
        }
        target.CalendarItem ??= new();
        if (!string.IsNullOrEmpty(vCalendarUnique.Uid))
        {
            target.Uid = vCalendarUnique.Uid;
        }
        else
        {
            target.Uid = resource.Uri?.ItemName?.Replace(".ics", "") ?? $"{Guid.NewGuid()}";
            Log.Warning("Autocorrecting malformed VObject {davName} with missing UID, new UID {uid}", resource.DavName, target.Uid);
        }

        if (vCalendarUnique.Reference is not null)
        {
            target.VObjectType = vCalendarUnique.Reference.Name;
            vCalendarUnique.Reference.ReadCommonProperties(target);
            // if (target.CalendarItem.IsOrganizer is not null)
            // {

            // }
        }
        else if (vCalendarUnique.Occurrences.Count != 0)
        {
            var component = vCalendarUnique.Occurrences.First().Value;
            target.VObjectType = component.Name;
            component.ReadCommonProperties(target);
        }
        else if (vCalendarUnique.Component is not null)
        {
            target.VObjectType = vCalendarUnique.Component.Name;
            // TODO: Other components (currently known on the top level: VAVAILABILITY, VPOLL)
            if (vCalendarUnique.Component is VAvailability availability)
            {
                CollectCalendarItem(availability, target.CalendarItem);
            }
        }

        serializedCalendar ??= vCalendarUnique.Calendar.Serialize();
        target.RawData = serializedCalendar;
        target.Etag = serializedCalendar.PrettyMD5Hash();
        if (string.IsNullOrEmpty(target.ScheduleTag))
        {
            target.ScheduleTag = target.Etag;
        }
        return target;
    }

    // [Obsolete("Use call with VCalendarUnique")]
    // TODO: [HIGH] Use call with VCalendarUnique
    public static CollectionObject? CreateCollectionObject(this VCalendar vCalendar, DavResource resource, string? serializedCalendar = null)
    {
        var vCalendarUnique = new VCalendarUnique(vCalendar);
        return vCalendarUnique.CreateCollectionObject(resource, serializedCalendar);
    }

    private static void CollectCalendarItem(VAvailability component, ObjectCalendar ci)
    {
        ci.Dtstamp = component.DateStamp;
        ci.Created = component.Created;
        ci.LastModified = component.LastModified;
        ci.Dtstart = component.DateStart?.ToInstant();
        ci.Dtend = component.DateEnd?.ToInstant();
        ci.Timezone = component.DateStart?.Zone?.Id;
        ci.Summary = ReadTextProperty(component, PropertyName.Summary);
        ci.Status = ReadStringProperty(component, PropertyName.Status);
        ci.Description = ReadTextProperty(component, PropertyName.Description);
        ci.Location = ReadStringProperty(component, PropertyName.Location);
        ci.Class = ReadStringProperty(component, PropertyName.Class);
        ci.Url = ReadStringProperty(component, PropertyName.Url);
        ci.Sequence = ReadIntegerProperty(component, PropertyName.Sequence) ?? 0;
        ci.Priority = ReadIntegerProperty(component, PropertyName.Priority);
        ci.Transp = ReadStringProperty(component, PropertyName.TimeTransparency);
    }

    private static void CollectCalendarItem(RecurringComponent component, ObjectCalendar ci)
    {
        ci.Dtstamp = component.DateStamp;
        ci.Created = component.Created;
        ci.LastModified = component.LastModified;
        ci.Dtstart = component.DateStart?.ToInstant();
        ci.Timezone = component.DateStart?.Zone?.Id;
        ci.Due = ReadDateTimeProperty(component, PropertyName.Due);
        ci.Completed = ReadDateTimeProperty(component, PropertyName.Completed);
        ci.Summary = ReadTextProperty(component, PropertyName.Summary);
        ci.Status = ReadStringProperty(component, PropertyName.Status);
        ci.Description = ReadTextProperty(component, PropertyName.Description);
        ci.Location = ReadStringProperty(component, PropertyName.Location);
        ci.Class = ReadStringProperty(component, PropertyName.Class);
        ci.Url = ReadStringProperty(component, PropertyName.Url);
        ci.Sequence = ReadIntegerProperty(component, PropertyName.Sequence) ?? 0;
        ci.Priority = ReadIntegerProperty(component, PropertyName.Priority);
        ci.Rrule = ReadStringProperty(component, PropertyName.RecurrenceRule);
        ci.Transp = ReadStringProperty(component, PropertyName.TimeTransparency);

        var range = component.Parent!.Children.GetOccurrencesRange();
        if (range is not null)
        {
            ci.FirstInstanceStart = range.Value.HasStart ? range.Value.Start : Instant.MinValue;
            ci.Dtstart ??= ci.FirstInstanceStart;
            ci.LastInstanceEnd = range.Value.HasEnd ? range.Value.End : Instant.MaxValue;   // TODO: it's last instance start (not end), okay?
        }
        var interval = component.GetInterval(DateTimeZone.Utc);
        if (interval.HasStart)
        {
            ci.Dtstart ??= interval.Start;
        }
        if (interval.HasEnd)
        {
            ci.Dtend = interval.End;
        }

        // scheduling related information
        var attendees = component.Attendees.Value;
        if (attendees is not null && attendees.Count > 0)
        {
            var existingAttendees = ci.Attendees ?? [];
            ci.Attendees ??= [];
            var now = SystemClock.Instance.GetCurrentInstant();
            foreach (var attendee in attendees)
            {
                var existing = existingAttendees.FirstOrDefault(x => x.EMail?.Equals(attendee.Value, StringComparison.InvariantCultureIgnoreCase) == true);
                if (existing is not null)
                {
                    existing.CommonName = attendee.CommonName;
                    existing.ParticipationStatus = attendee.ParticipationStatus.Token;
                    existing.Rsvp = attendee.Rsvp;
                    existing.Modified = now;
                }
                else
                {
                    ci.Attendees.Add(new ObjectCalendarAttendee
                    {
                        CommonName = attendee.CommonName,
                        Role = attendee.Role.Token,
                        ParticipationStatus = attendee.ParticipationStatus.Token,
                        Rsvp = attendee.Rsvp,
                        EMail = attendee.Value,
                        Language = attendee.Language,
                        AttendeeType = attendee.CalendarUserType,
                        ScheduleAgent = attendee.ScheduleAgent.Token,
                    });
                }
            }
        }
        else
        {
            ci.Attendees.Clear();
        }
        ci.IsScheduling = attendees is not null && attendees.Count > 0 && component.Organizer is not null;
    }

    public static CollectionObject UpdateWith(this CollectionObject collectionObject, VCalendar vCalendar, bool isSignificantChange)
    {
        var serializedCalendar = vCalendar.Serialize();
        collectionObject.RawData = serializedCalendar;
        collectionObject.Etag = serializedCalendar.PrettyMD5Hash();
        if (isSignificantChange)
        {
            collectionObject.ScheduleTag = collectionObject.Etag;
        }
        return collectionObject;
    }

    public static CollectionObject UpdateWith(this CollectionObject collectionObject, VCalendarUnique vCalendar, bool isSignificantChange) => collectionObject.UpdateWith(vCalendar.Calendar, isSignificantChange);
}
