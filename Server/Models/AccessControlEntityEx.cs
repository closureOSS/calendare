using System.Xml.Linq;

namespace Calendare.Server.Models;

public class AccessControlEntityEx : AccessControlEntity
{
    public XName? Name { get; set; }
}
