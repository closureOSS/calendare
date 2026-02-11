using System.Xml.Linq;

namespace Calendare.Server.Models;

public record class DavPropertyRef
{
    public required XName Name { get; init; }
    public bool IsExpensive { get; init; }
    public XElement? Element { get; set; }
}
