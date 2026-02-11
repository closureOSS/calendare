using Calendare.Data.Models;

namespace Calendare.Server.Api;

public class PermissionRequest
{
    public required string Uri { get; set; }
    public PrivilegeMask? GlobalPermitSelf { get; set; }
    public PrivilegeMask? AuthorizedProhibit { get; set; }
    public PrivilegeMask? OwnerProhibit { get; set; }
}
