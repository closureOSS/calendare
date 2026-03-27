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
        var root = await Context.Collection.Where(c => c.OwnerId == StockPrincipal.Admin && c.ParentId == null).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Root admin missing");
        // var adminRole = new Usr
        // {
        //     IsActive = true,
        //     Username = userName,
        //     Email = $"{userName}@internal",
        //     EmailOk = now,
        //     DateFormatType = BootstrapOptions.DateFormatType ?? UserDefaults.DateFormatType,
        //     Locale = BootstrapOptions.Locale ?? UserDefaults.Locale,
        // };
        var groupCollection = new Collection
        {
            OwnerId = root.OwnerId,
            CollectionType = CollectionType.Principal,
            PrincipalType = PrincipalTypeGroup,
            ParentId = root.Id,
            ParentContainerUri = $"/{BootstrapOptions.Username ?? "admin"}/",
            Uri = $"/{BootstrapOptions.Username ?? "admin"}/{userName}/",
            DisplayName = displayName,
            AuthorizedProhibit = PrivilegeMask.All,
            AuthorizedMask = PrivilegeMask.All,
            OwnerProhibit = PrivilegeMask.All,
            OwnerMask = PrivilegeMask.All,
            GlobalPermitSelf = PrivilegeMask.None,
            GlobalPermit = PrivilegeMask.None,
        };
        Context.Collection.Add(groupCollection);
        // adminRole.Collections.Add(groupCollection);
        // Context.Usr.Add(adminRole);
        var relationship = new GrantRelation
        {
            GrantorId = root.Id,
            Grantee = groupCollection,
            GrantTypeId = 1,
            Privileges = privilege,
        };
        Context.GrantRelation.Add(relationship);
        await Context.SaveChangesAsync(ct);
    }
}
