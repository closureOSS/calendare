using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.VSyntaxReader.Components;

namespace Calendare.Server.Calendar;

public class ComponentFilter
{
    public List<string> ComponentTypes { get; set; } = [];
    public bool IsNotDefined { get; set; }
    public TimeRangeFilter? TimeRangeFilter { get; set; }
    public List<PropertyFilter> PropertyFilters { get; set; } = [];

    // https://datatracker.ietf.org/doc/html/rfc4791#section-9.7.1
    // <!ELEMENT comp-filter (is-not-defined | (time-range?,
    //                         prop-filter*, comp-filter*))>
    //  <!ATTLIST comp-filter name CDATA #REQUIRED>
    //  name value: a calendar object or calendar component
    //              type (e.g., VEVENT)
    public static ComponentFilter? Parse(XElement xml, ComponentFilter? compFilter = null)
    {
        var xmlCompFilter = xml.Element(XmlNs.Caldav + "comp-filter");
        if (xmlCompFilter is null)
        {
            return null; // no component filter
        }
        compFilter ??= new ComponentFilter();
        compFilter.IsNotDefined = xmlCompFilter.Element(XmlNs.Caldav + "is-not-defined") != null;
        var componentType = xmlCompFilter.Attribute("name");
        if (componentType is null)
        {
            // TODO: throw as name is required
            return null;
        }
        if (string.Equals(componentType.Value, ComponentName.VCalendar, System.StringComparison.Ordinal))
        {
            compFilter.ComponentTypes.AddRange([ComponentName.VTodo, ComponentName.VEvent, ComponentName.VJournal, ComponentName.VAvailability, ComponentName.VPoll]);
        }
        else
        {
            compFilter.ComponentTypes.Clear();
            compFilter.ComponentTypes.Add(componentType.Value.ToUpperInvariant());
        }
        var xmlNestedCompFilter = xmlCompFilter.Element(XmlNs.Caldav + "comp-filter");
        if (xmlNestedCompFilter is not null)
        {
            return Parse(xmlCompFilter, compFilter);
        }
        compFilter.TimeRangeFilter = TimeRangeFilter.Parse(xmlCompFilter);
        compFilter.PropertyFilters = PropertyFilter.Parse(xmlCompFilter);
        return compFilter;
    }

    public bool IsValid()
    {
        return ComponentTypes.Count != 0;
    }
}
