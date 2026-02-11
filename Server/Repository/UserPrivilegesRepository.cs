#undef DEBUG_PRIVILEGE_CALCULATION

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Data.Utils;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;
namespace Calendare.Server.Repository;

/// <summary>
/// Logic to build the privileges network. Generates direct and indirect grants.
/// </summary>
public partial class UserRepository
{
    public async Task RebuildPrivilegesAsync(Principal principal, CancellationToken ct)
    {
        if (principal.UserId != StockPrincipal.Admin)
        {
            await RebuildPrivilegesAsync(principal.Id, ct);
        }
        else
        {
            var principals = await Db.Collection.Where(c => c.CollectionType == CollectionType.Principal).ToListAsync(ct);
            foreach (var p in principals)
            {
                await RebuildPrivilegesAsync(p.Id, ct);
            }
        }
    }

    private async Task RebuildPrivilegesAsync(Collection principalCollection, CancellationToken ct) => await RebuildPrivilegesAsync(principalCollection.Id, ct);

    private async Task RebuildPrivilegesAsync(int principalCollectionId, CancellationToken ct)
    {
        List<GrantRelation> memberGrants = [];
        var groups = await ResolveGroupsAsync(principalCollectionId, ct);
        var grantsToGroups = await ResolveGranteeAsync(groups, ct);
        memberGrants.AddRange(grantsToGroups.Select(grant => BuildGrantRelationTo(grant, principalCollectionId)));
        var grantorGroups = await ResolveGranteeAsync(grantsToGroups.Select(x => x.GrantorId), ct);
        StringBuilder sb = new();
        foreach (var grant in grantorGroups)
        {
            var parent = memberGrants.FirstOrDefault(gg => gg.GrantorId == grant.GranteeId);
            if (parent is null)
            {
                // TODO: What is this condition about?
                Log.Error("Missing parent of {grantor}->{grantee} during rebuilding privileges for {principal}", grant.GrantorId, grant.GranteeId, principalCollectionId);
#if DEBUG_PRIVILEGE_CALCULATION
                sb.AppendLine($"Missing parent for -> {grant.Grantor?.Uri ?? $"{grant.GrantorId}"}");
#endif
            }
            else
            {
                var inheritedPrivileges = grant.Privileges.ToBitArray().And(parent.Privileges.ToBitArray()).FromBitArray();
                var existing = memberGrants.FirstOrDefault(gg => gg.GrantorId == grant.GrantorId);
                if (existing is null)
                {
                    grant.Privileges = inheritedPrivileges;
                    grant.GrantTypeId = StaticData.FindCommonRelationship(grant.Privileges).Id;
#if DEBUG_PRIVILEGE_CALCULATION
                    sb.AppendLine($"Parent {parent.Grantor?.Uri ?? $"{parent.GrantorId}"} for -> {grant.Grantor?.Uri ?? $"{grant.GrantorId}"}, {grant.GrantTypeId}");
#endif
                    if (grant.GrantorId != principalCollectionId)
                    {
                        memberGrants.Add(BuildGrantRelationTo(grant, principalCollectionId));
                    }
                }
                else
                {
                    existing.Privileges = existing.Privileges.ToBitArray().Or(inheritedPrivileges.ToBitArray()).FromBitArray();
                    existing.GrantTypeId = StaticData.FindCommonRelationship(existing.Privileges).Id;
#if DEBUG_PRIVILEGE_CALCULATION
                    sb.AppendLine($"Parent {parent.Grantor?.Uri ?? $"{parent.GrantorId}"} for -> {grant.Grantor?.Uri ?? $"{grant.GrantorId}"}, {grant.GrantTypeId} with existing {existing.GrantTypeId}");
#endif
                }
            }
        }
#if DEBUG_PRIVILEGE_CALCULATION
        Dump(sb, memberGrants);
        Log.Warning(sb.ToString());
#endif
        await GrantRelationMergeAsync(principalCollectionId, memberGrants, ct);
    }

#if DEBUG_PRIVILEGE_CALCULATION
    private static StringBuilder Dump(StringBuilder sb, List<GrantRelation> grants)
    {
        sb.AppendLine();
        foreach (var grant in grants)
        {
            sb.AppendLine($"Grant to {grant.Grantor?.Uri ?? $"{grant.GrantorId}"}, {grant.GrantTypeId}");
        }
        return sb;
    }
#endif

    private static GrantRelation BuildGrantRelationTo(GrantRelation grant, int principalCollectionId)
    {
        return new GrantRelation
        {
            GranteeId = principalCollectionId,
            Grantee = grant.Grantee,  // TODO: Remove, for debugging
            GrantorId = grant.GrantorId,
            Grantor = grant.Grantor,
            GrantTypeId = grant.GrantTypeId,
            Privileges = grant.Privileges,
            IsIndirect = true,
        };
    }

    private async Task<List<int>> ResolveGroupsAsync(int principalCollectionId, CancellationToken ct)
    {
        // TODO: [Low] Improve unnest groups, this is hard-core reading the whole membership list and unnesting in memory
        //             but potentially the fastest way
        var gms = await Db.CollectionGroup.Include(cg => cg.Group).Select(cg => new { cg.GroupId, GroupParentId = cg.Group.ParentId, cg.MemberId }).ToListAsync(ct);
        var queue = new Queue<int>();
        var result = new HashSet<int>();
        foreach (var ms in gms.Where(cg => cg.MemberId == principalCollectionId))
        {
            queue.Enqueue(ms.GroupParentId ?? ms.GroupId);
            result.Add(ms.GroupId);
        }
        while (queue.Count > 0)
        {
            int groupId = queue.Dequeue();
            foreach (var ms in gms.Where(cg => cg.MemberId == groupId))
            {
                if (result.Add(ms.GroupId))
                {
                    queue.Enqueue(ms.GroupParentId ?? ms.GroupId);
                }
            }
        }
        return [.. result];
    }

    private async Task<List<GrantRelation>> ResolveGranteeAsync(IEnumerable<int> granteeIds, CancellationToken ct)
    {
        return await Db.GrantRelation
            // .Include(gr => gr.Grantor)
            .Where(gr => granteeIds.Contains(gr.GranteeId))
            .AsNoTracking()
            .ToListAsync(ct);
        // ToTraceString();
    }

    private async Task GrantRelationMergeAsync(int principalCollectionId, List<GrantRelation> grantRelations, CancellationToken ct)
    {
        var existingGrants = await Db.GrantRelation.Where(r => principalCollectionId == r.GranteeId).ToListAsync(ct);
        foreach (var grant in grantRelations.DistinctBy(gr => new { gr.GranteeId, gr.GrantorId }))
        {
            var existing = existingGrants.FirstOrDefault(gg => gg.GrantorId == grant.GrantorId && gg.GranteeId == grant.GranteeId);
            if (existing is null)
            {
                Db.GrantRelation.Add(grant);
            }
            else
            {
                if (existing.IsIndirect)
                {
                    if (!existing.Privileges.Equals(grant.Privileges) || existing.GrantTypeId != grant.GrantTypeId)
                    {
                        existing.Privileges = grant.Privileges;
                        existing.GrantTypeId = grant.GrantTypeId;
                        existing.Modified = SystemClock.Instance.GetCurrentInstant();
                    }
                }
            }
        }
        Db.GrantRelation.RemoveRange(existingGrants.Where(oldGrant => oldGrant.IsIndirect == true && !grantRelations.Any(gr => gr.GranteeId == oldGrant.GranteeId && gr.GrantorId == oldGrant.GrantorId)));
        await Db.SaveChangesAsync(ct);
    }
}
