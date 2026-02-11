using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Server.Constants;

namespace Calendare.Server.Calendar;

public class ParamFilter
{
    public required string Name { get; init; }
    public bool IsNotDefined { get; init; }
    public List<Func<string, bool>> TextMatches { get; init; } = [];


    // https://datatracker.ietf.org/doc/html/rfc4791#section-9.7.3
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
            var xmlIsNotDefined = xmlParamFilter.Element(XmlNs.Carddav + "is-not-defined");
            var pf = new ParamFilter
            {
                Name = propName.Value.ToUpperInvariant(),
                IsNotDefined = xmlIsNotDefined is not null,
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
