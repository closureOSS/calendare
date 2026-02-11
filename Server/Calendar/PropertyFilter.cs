using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Calendare.Server.Constants;

namespace Calendare.Server.Calendar;

public class PropertyFilter
{
    public required string Name { get; init; }
    public bool IsNotDefined { get; init; }
    public List<Func<string, bool>> TextMatches { get; init; } = [];
    public List<ParamFilter> ParamFilters { get; init; } = [];

    // https://datatracker.ietf.org/doc/html/rfc4791#section-9.7.2
    // <!ELEMENT prop-filter (is-not-defined |
    //                        ((time-range | text-match)?,
    //                         param-filter*))>
    // <!ATTLIST prop-filter name CDATA #REQUIRED>
    // name value: a calendar property name (e.g., ATTENDEE)
    public static List<PropertyFilter> Parse(XElement xml)
    {
        var xmlPropFilters = xml.Elements(XmlNs.Caldav + "prop-filter");
        if (xmlPropFilters is null || !xmlPropFilters.Any())
        {
            return [];
        }
        var result = new List<PropertyFilter>();
        foreach (var xmlPropFilter in xmlPropFilters)
        {
            var propName = xmlPropFilter.Attribute("name");
            if (propName is null)
            {
                // TODO: Verify, currently silently ignoring if not name attribute exists
                continue;
            }
            var xmlIsNotDefined = xmlPropFilter.Element(XmlNs.Caldav + "is-not-defined");
            var pf = new PropertyFilter
            {
                Name = propName.Value,
                IsNotDefined = xmlIsNotDefined is not null,
                ParamFilters = ParamFilter.Parse(xmlPropFilter.Elements(XmlNs.Caldav + "param-filter")),
                TextMatches = ParseTextMatches(xmlPropFilter),
            };
            if (pf.IsValid())
            {
                result.Add(pf);
            }
        }
        return result;
    }

    //https://datatracker.ietf.org/doc/html/rfc4791#section-9.7.5
    public static List<Func<string, bool>> ParseTextMatches(XElement xml)
    {
        var xmlTextMatches = xml.Elements(XmlNs.Caldav + "text-match");
        var result = new List<Func<string, bool>>();
        foreach (var xmlTextMatch in xmlTextMatches ?? [])
        {
            var negateCondition = "yes".Equals(xmlTextMatch.Attribute("negate-condition")?.Value ?? "no", System.StringComparison.InvariantCultureIgnoreCase);
            var collation = xmlTextMatch.Attribute("collation")?.Value ?? "i;unicode-casemap";
            var textMatch = new TextMatch { Collation = collation, NegateCondition = negateCondition, Value = xmlTextMatch.Value, };
            result.Add(textMatch.Compile());
        }
        return result;
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name);
    }
}
