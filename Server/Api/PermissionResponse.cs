using Calendare.Data.Models;

namespace Calendare.Server.Api;

public class PermissionResponse
{
    public string Username { get; set; } = default!;
    public string Uri { get; set; } = default!;
    public CollectionType CollectionType { get; set; } = CollectionType.Collection;
    public CollectionSubType CollectionSubType { get; set; } = CollectionSubType.Default;
    public PrincipalType? PrincipalType { get; set; }

    /// <summary>Users current privileges on object</summary>
    public PrivilegeMask Permissions { get; set; }

    public PrivilegeMask GlobalPermitSelf { get; set; }

    public PrivilegeMask AuthorizedProhibit { get; set; }

    public PrivilegeMask OwnerProhibit { get; set; }

    /// <summary>
    /// Current administrative privileges
    ///
    /// This value is not set in bulk queries
    /// </summary>
    public PrivilegeMask Administration { get; set; }

    /// <summary>Identifies the root administrator (id 1)</summary>
    public bool? IsRoot { get; set; }
}
