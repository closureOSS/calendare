using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Server.Constants;

namespace Calendare.Server.Addressbook;



public class AddressbookFilter
{
    /// <summary>
    /// True -> AllOf - all subfilters must match (AND)
    /// False -> AnyOf - one or more subfilter must match (OR)
    /// </summary>
    public bool LogicalAnd { get; set; }
    public List<PropertyFilter> PropFilters { get; } = [];

    public static AddressbookFilter? Parse(XElement? xml)
    {
        if (xml is null)
        {
            return null;
        }
        // https://datatracker.ietf.org/doc/html/rfc6352#section-10.5
        var xmlFilter = xml.Element(XmlNs.Carddav + "filter");
        if (xmlFilter is null || xmlFilter.IsEmpty)
        {
            return null; // no filter
        }
        var xmlTest = xmlFilter.Attribute("test");
        var filter = new AddressbookFilter
        {
            LogicalAnd = xmlTest is not null && "allof".Equals(xmlTest.Value, System.StringComparison.InvariantCultureIgnoreCase)
        };
        filter.PropFilters.AddRange(PropertyFilter.Parse(xmlFilter));
        return filter;
    }
}
