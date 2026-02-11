using Calendare.Data.Models;

namespace Calendare.Server.Api;

public class PermissionResponse
{
    public string Username { get; set; } = default!;
    public string Uri { get; set; } = default!;
    public CollectionType CollectionType { get; set; } = CollectionType.Collection;
    public CollectionSubType CollectionSubType { get; set; } = CollectionSubType.Default;
    public PrincipalType? PrincipalType { get; set; }

    public PrivilegeMask Permissions { get; set; }
    public PrivilegeMask GlobalPermitSelf { get; set; }
    public PrivilegeMask AuthorizedProhibit { get; set; }
    public PrivilegeMask OwnerProhibit { get; set; }
    public bool? IsRoot { get; set; }
    public bool? IsAdmin { get; set; }
}
