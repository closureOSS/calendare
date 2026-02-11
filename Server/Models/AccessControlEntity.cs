using System.Collections.Immutable;
using Calendare.Data.Models;

namespace Calendare.Server.Models;

public class AccessControlEntity
{
    public Principal? Grantee { get; set; }
    public Collection? Grantor { get; set; }
    public GrantType? GrantType { get; set; }
    public PrivilegeMask Privileges { get; set; }
    public bool IsIndirect { get; set; }
    public bool IsInherited { get; set; }

    public ImmutableList<PrivilegeItem> Grants
    {
        get
        {
            return [.. PrivilegesDefinitions.LoadList(Privileges)];
        }
    }
}
