using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Calendare.Server.Migrations;

partial class MigrationRepository
{
    private async Task AdminGroups02_Migration(CancellationToken ct)
    {
        await CreateAdminRoles("role-sysops", "System Operator Role", PrivilegeMask.AdminSysOps, ct);
        await CreateAdminRoles("role-manager", "Group Administrator Role", PrivilegeMask.AdminManager, ct);
    }

    private async Task CreateAdminRoles(string userName, string displayName, PrivilegeMask privilege, CancellationToken ct)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        var principalTypes = await Context.PrincipalType.ToListAsync(ct);
        var PrincipalTypeGroup = principalTypes.Find(x => string.Equals(x.Label, PrincipalTypeCode.Group, StringComparison.Ordinal)) ?? throw new InvalidOperationException("Valid principal type required");
        var adminRole = new Usr
        {
            IsActive = true,
            Username = userName,
            Email = $"{userName}@internal",
            EmailOk = now,
            DateFormatType = BootstrapOptions.DateFormatType ?? UserDefaults.DateFormatType,
            Locale = BootstrapOptions.Locale ?? UserDefaults.Locale,
        };
        var groupCollection = new Collection
        {
            Owner = adminRole,
            CollectionType = CollectionType.Principal,
            PrincipalType = PrincipalTypeGroup,
            ParentContainerUri = "/",
            Uri = $"/{adminRole.Username}/",
            DisplayName = displayName,
            AuthorizedProhibit = PrivilegeMask.All,
            AuthorizedMask = PrivilegeMask.All,
            OwnerProhibit = PrivilegeMask.All,
            OwnerMask = PrivilegeMask.All,
            GlobalPermitSelf = PrivilegeMask.None,
            GlobalPermit = PrivilegeMask.None,
        };
        adminRole.Collections.Add(groupCollection);
        Context.Usr.Add(adminRole);
        var relationship = new GrantRelation
        {
            GrantorId = StockPrincipal.Admin,
            Grantee = groupCollection,
            GrantTypeId = 1,
            Privileges = privilege,
        };
        Context.GrantRelation.Add(relationship);
        await Context.SaveChangesAsync(ct);
    }
}
