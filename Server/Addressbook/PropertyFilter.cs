using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Calendare.Server.Constants;
using FolkerKinzel.VCards;
using FolkerKinzel.VCards.Enums;

namespace Calendare.Server.Addressbook;

public class PropertyFilter
{
    public required string Name { get; init; }
    public Prop? VCardProperty { get; init; }
    public bool IsNotDefined { get; init; }
    public bool LogicalAnd { get; init; }
    public List<Func<string, bool>> TextMatches { get; init; } = [];
    public List<ParamFilter> ParamFilters { get; init; } = [];


    // https://datatracker.ietf.org/doc/html/rfc6352#section-10.5.1
    // - text-match
    // - param-filter
    // - is-not-defined
    public static List<PropertyFilter> Parse(XElement xml)
    {
        var xmlPropFilters = xml.Elements(XmlNs.Carddav + "prop-filter");
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
            var xmlTest = xmlPropFilter.Attribute("test");
            var logicalAnd = xmlTest is not null && "allof".Equals(xmlTest.Value, System.StringComparison.InvariantCultureIgnoreCase);
            var xmlIsNotDefined = xmlPropFilter.Element(XmlNs.Carddav + "is-not-defined");
            // TODO: How to handle custom properties (VCardProperty is null)
            var pf = new PropertyFilter
            {
                Name = propName.Value.ToUpperInvariant(),
                IsNotDefined = xmlIsNotDefined is not null,
                LogicalAnd = logicalAnd,
                VCardProperty = VCardTags.Lookup(propName.Value.ToUpperInvariant()),
                TextMatches = ParseTextMatches(xmlPropFilter),
                ParamFilters = ParamFilter.Parse(xmlPropFilter.Elements(XmlNs.Carddav + "param-filter"))
            };
            if (pf.IsValid())
            {
                result.Add(pf);
            }
        }
        return result;
    }

    public static List<Func<string, bool>> ParseTextMatches(XElement xml)
    {
        var xmlTextMatches = xml.Elements(XmlNs.Carddav + "text-match");
        var result = new List<Func<string, bool>>();
        foreach (var xmlTextMatch in xmlTextMatches ?? [])
        {
            var matchType = xmlTextMatch.Attribute("match-type")?.Value ?? "contains";
            var negateCondition = "yes".Equals(xmlTextMatch.Attribute("negate-condition")?.Value ?? "no", System.StringComparison.InvariantCultureIgnoreCase);
            var collation = xmlTextMatch.Attribute("collation")?.Value ?? "i;unicode-casemap";
            var textMatch = new TextMatch { Collation = collation, MatchType = matchType.ToLowerInvariant(), NegateCondition = negateCondition, Value = xmlTextMatch.Value, };
            result.Add(textMatch.Compile());
        }
        return result;
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name) && VCardProperty is not null;
    }
}
