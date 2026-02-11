using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Data.Models;

namespace Calendare.Server.Models;

public class PrivilegeItem
{
    public required XName Id { get; init; }
    public PrivilegeMask Privileges { get; init; }
    public bool IsAbstract { get; init; }
    public List<PrivilegeItem>? Items { get; init; }
    public string? Description { get; set; }
}
