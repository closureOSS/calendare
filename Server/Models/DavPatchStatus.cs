using System.Collections.Generic;
using System.Xml.Linq;

namespace Calendare.Server.Models;

public class DavPatchStatus
{
    public List<DavPropertyStatic> Properties { get; init; } = [];
    public bool Failure { get; set; }
    public string? ResponseDescription { get; set; }
    public XName? Error { get; set; }
}
