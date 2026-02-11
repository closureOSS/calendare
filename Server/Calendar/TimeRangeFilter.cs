using System.Xml.Linq;
using Calendare.Server.Constants;
using NodaTime;
using NodaTime.Text;

namespace Calendare.Server.Calendar;

public class TimeRangeFilter
{
    public Instant Start { get; set; } = Instant.MinValue;
    public Instant End { get; set; } = Instant.MaxValue;

    // see https://datatracker.ietf.org/doc/html/rfc4791#section-9.9
    //  <!ELEMENT time-range EMPTY>
    //  <!ATTLIST time-range start CDATA #IMPLIED
    //                       end   CDATA #IMPLIED>
    //  start value: an iCalendar "date with UTC time"
    //  end value: an iCalendar "date with UTC time"
    public static TimeRangeFilter? Parse(XElement xml)
    {
        var xmlTimeRange = xml.Element(XmlNs.Caldav + "time-range");
        if (xmlTimeRange is null || !xmlTimeRange.HasAttributes)
        {
            // TODO: throw if no start and end attribute exists
            return null;
        }
        var startAttr = xmlTimeRange.Attribute("start");
        var endAttr = xmlTimeRange.Attribute("end");
        if (startAttr is null && endAttr is null)
        {
            // TODO: throw if no start and end attribute exists
        }
        var start = Parse(startAttr?.Value);
        var end = Parse(endAttr?.Value);
        var result = new TimeRangeFilter();
        if (start?.Success == true)
        {
            result.Start = start.Value.ToInstant();
        }
        if (end?.Success == true)
        {
            result.End = end.Value.ToInstant();
        }
        return result;
    }

    public bool IsValid()
    {
        return Start <= End;
    }

    public bool IsUnresticted()
    {
        return Start == Instant.MinValue && End == Instant.MaxValue;
    }

    public Interval ToInterval()
    {
        if (End == Instant.MaxValue && Start == Instant.MinValue)
        {
            return new Interval();
        }
        if (End == Instant.MaxValue)
        {
            return new Interval(Start, null);
        }
        if (Start == Instant.MinValue)
        {
            return new Interval(null, End);
        }
        return new Interval(Start, End);
    }

    private static ParseResult<OffsetDateTime> Parse(string? time)
    {
        // 20060104T000000Z
        var pattern = OffsetDateTimePattern.CreateWithInvariantCulture("yyyyMMddTHHmmsso<G>");
        // if (time.Length == 5)
        // {
        // pattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");
        // }
        return pattern.Parse(time ?? "");
    }
}
