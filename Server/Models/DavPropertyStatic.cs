using System.Xml.Linq;

namespace Calendare.Server.Models;

public class DavPropertyStatic
{
    public required XName Name { get; init; }
    public XElement? Value { get; set; }
    public bool ToDelete { get; set; }
    // Result information
    public PropertyUpdateResult IsSuccess { get; set; } = PropertyUpdateResult.BadRequest;
    public string StatusCode { get; set; } = "424 Failed Dependency";
}
