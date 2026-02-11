using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Server.Constants;
using FolkerKinzel.VCards;
using FolkerKinzel.VCards.Enums;

namespace Calendare.Server.Addressbook;

public class ParamFilter
{
    public required string Name { get; init; }
    public Prop? VCardProperty { get; init; }
    public bool IsNotDefined { get; init; }
    public bool LogicalAnd { get; init; }
    public List<Func<string, bool>> TextMatches { get; init; } = [];


    // https://datatracker.ietf.org/doc/html/rfc6352#section-10.5.2
    // - text-match
    // - is-not-defined
    public static List<ParamFilter> Parse(IEnumerable<XElement> xmlParamFilters)
    {
        var result = new List<ParamFilter>();
        foreach (var xmlParamFilter in xmlParamFilters)
        {
            var propName = xmlParamFilter.Attribute("name");
            if (propName is null)
            {
                // TODO: Verify, currently silently ignoring if not name attribute exists
                continue;
            }
            var xmlTest = xmlParamFilter.Attribute("test");
            var logicalAnd = xmlTest is not null && "allof".Equals(xmlTest.Value, System.StringComparison.InvariantCultureIgnoreCase);
            var xmlIsNotDefined = xmlParamFilter.Element(XmlNs.Carddav + "is-not-defined");
            // TODO: How to handle custom properties (VCardProperty is null)
            var pf = new ParamFilter
            {
                Name = propName.Value.ToUpperInvariant(),
                IsNotDefined = xmlIsNotDefined is not null,
                LogicalAnd = logicalAnd,
                VCardProperty = VCardTags.Lookup(propName.Value.ToUpperInvariant()),
                TextMatches = PropertyFilter.ParseTextMatches(xmlParamFilter)
            };
            if (pf.IsValid())
            {
                result.Add(pf);
            }
        }
        return result;
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name);// && VCardProperty is not null;
    }
}
