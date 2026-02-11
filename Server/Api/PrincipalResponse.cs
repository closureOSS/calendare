using Calendare.Data.Models;
using NodaTime;

namespace Calendare.Server.Api;

public class PrincipalResponse
{
    public string Username { get; set; } = default!;
    public string Uri { get; set; } = default!;
    public PrincipalType? PrincipalType { get; set; }
    public string? Email { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? DisplayName { get; set; }
    public string? Timezone { get; set; }
    public string? Description { get; set; } = "";
    public string? DateFormatType { get; set; }
    public string? Locale { get; set; }
    public string? Color { get; set; }
    public int? OrderBy { get; set; }
    public bool IsRoot { get; set; }
    public bool? HasGroups { get; set; }
    public bool? HasScheduling { get; set; }
    public PrivilegeMask Permissions { get; set; }
    public PrivilegeMask GlobalPermitSelf { get; set; }
    public PrivilegeMask AuthorizedProhibit { get; set; }
    public PrivilegeMask OwnerProhibit { get; set; }
    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}

public class PrincipalIntermediateResponse : PrincipalResponse
{
    public GrantRelation? Granted { get; set; }
    public PrivilegeMask GlobalPermit { get; set; }

    public bool IsOwner { get; set; }
}
