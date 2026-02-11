using System.Xml.Linq;
using Calendare.Server.Constants;

namespace Calendare.Server.Calendar;


public class CalendarFilter
{
    public ComponentFilter? ComponentFilter { get; set; }

    public static CalendarFilter? Parse(XElement? xml)
    {
        if (xml is null)
        {
            return null;
        }
        // https://datatracker.ietf.org/doc/html/rfc4791#section-9.7
        var xmlFilter = xml.Element(XmlNs.Caldav + "filter");
        if (xmlFilter is null || xmlFilter.IsEmpty)
        {
            return null; // no filter
        }
        var xmlTest = xmlFilter.Attribute("test");
        var filter = new CalendarFilter
        {
            ComponentFilter = ComponentFilter.Parse(xmlFilter),
        };
        return filter;
    }
}
