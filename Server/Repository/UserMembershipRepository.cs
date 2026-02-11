using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models.DavProperties;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Repository;

/// <summary>
/// Amend members and memberships
/// </summary>
public partial class UserRepository
{

    /// <summary>
    /// Memberships of a principal
    ///
    /// It returns collections where the principal is a member of and also members which are in groups owned by the principal
    /// </summary>
    /// <param name="principalId"></param>
    /// <param name="ct"></param>
    /// <returns>Memberships</returns>
    public async Task<List<MembershipQueryResult>> GetPrincipalMembershipsAsync(int principalId, MembershipDirection direction, CancellationToken ct)
    {
        var queryMembers = Db.CollectionGroup
            .Include(m => m.Member).ThenInclude(m => m.Owner)
            .Where(c => c.Group.OwnerId == principalId)
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.GroupId,
                GroupUri = m.Group.Uri,
                MemberId = m.MemberId,
                MemberUri = m.Member.Uri,
                Username = m.Member.Owner.Username,
                Displayname = m.Member.DisplayName,
                PrincipalType = m.Member.PrincipalType!.Label,
                CollectionSubType = m.Member.CollectionSubType,
                IsMember = false,
            });
        var queryMemberships = Db.CollectionGroup
            .Include(m => m.Group).ThenInclude(m => m.Owner)
            .Where(c => c.Member.OwnerId == principalId)
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.MemberId,
                GroupUri = m.Member.Uri,
                MemberUri = m.Group.Uri,
                MemberId = m.GroupId,
                Username = m.Group.Owner.Username,
                Displayname = m.Group.DisplayName,
                PrincipalType = m.Group.PrincipalType!.Label,
                CollectionSubType = m.Group.CollectionSubType,
                IsMember = true,
            });
        var query = direction switch
        {
            MembershipDirection.Members => queryMembers,
            MembershipDirection.Memberships => queryMemberships,
            MembershipDirection.Both => queryMembers.Concat(queryMemberships),
            _ => throw new System.NotSupportedException(),
        };
        return await query.Concat(QueryPrincipalGroupsAsync(principalId)).ToListAsync(ct);
    }

    private IQueryable<MembershipQueryResult> QueryPrincipalGroupsAsync(int principalId)
    {
        return Db.Collection
            .Include(u => u.Owner)
            .Include(u => u.PrincipalType)
            .Where(c => c.CollectionType == CollectionType.Principal && c.OwnerId == principalId
                    && c.PrincipalType != null && c.PrincipalType.Label == PrincipalTypeCode.Group)
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.Id,
                GroupUri = m.Uri,
                MemberUri = "",
                MemberId = 0,
                Username = m.Owner.Username,
                Displayname = m.DisplayName,
                PrincipalType = m.PrincipalType!.Label,
                CollectionSubType = m.CollectionSubType,
                IsMember = false,
            })
            ;
    }

    /// <summary>
    /// Retrieves principals which could be added as members to a group owned by the given principal.
    ///
    /// The request excludes the principal itself (no self references) and the internal admin.
    /// </summary>
    /// <param name="principalId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<MembershipQueryResult>> GetPrincipalForMembershipsAsync(int principalId, CancellationToken ct)
    {
        var usrQuery = Db.Collection
            .Include(u => u.Owner)
            .Include(u => u.PrincipalType)
            .Where(c => c.CollectionType == CollectionType.Principal && c.ParentId == null
                && new[] { StockPrincipal.Admin, principalId }.Contains(c.OwnerId) == false)
            .Select(m => new MembershipQueryResult
            {
                GroupId = 0,
                GroupUri = "",
                MemberUri = m.Uri,
                MemberId = m.Id,
                Username = m.Owner.Username,
                Displayname = m.DisplayName,
                PrincipalType = m.PrincipalType!.Label,
                CollectionSubType = m.CollectionSubType,
                IsMember = false,
            })
            ;
        return await usrQuery.OrderBy(c => c.Displayname).ToListAsync(ct);
    }


    /// <summary>
    /// Members of a group (or list of groups)
    /// </summary>
    /// <param name="groupIds">Collection Id's of groups</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<MembershipQueryResult>> GetGroupMembersDirectAsync(int[] groupIds, CancellationToken ct)
    {
        var query = Db.CollectionGroup
            .Include(m => m.Member).ThenInclude(m => m.Owner)
            .Where(c => groupIds.Contains(c.GroupId))
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.GroupId,
                GroupUri = m.Group.Uri,
                MemberId = m.MemberId,
                MemberUri = m.Member.Uri,
                Username = m.Member.Owner.Username,
                Displayname = m.Member.DisplayName,
                PrincipalType = m.Member.PrincipalType!.Label,
                CollectionSubType = m.Member.CollectionSubType,
                IsMember = true,
            })
            .OrderBy(z => z.MemberUri)
            ;
        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Retrives members of a group for a principal identified by uri.
    ///
    /// Hint: Principal has members request
    /// </summary>
    /// <param name="uri">Principal Uri</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Collection?> GetMembersAsync(string uri, CancellationToken ct)
    {
        return await Db.Collection
            .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => c.Uri == uri && c.CollectionType == CollectionType.Principal)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Collection?> GetMembersAsync(int principalId, CollectionSubType subType, CancellationToken ct)
    {
        return await Db.Collection
            .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => c.OwnerId == principalId && c.CollectionType == CollectionType.Principal && c.CollectionSubType == subType)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Collection?> GetMembersAsync(int groupId, CancellationToken ct)
    {
        return await Db.Collection
           .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
           .Include(c => c.PrincipalType)
           .Include(c => c.Owner)
           .Where(c => c.Id == groupId)
           .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Retrieves groups (memberships) for a principal identified by uri
    ///
    /// Hint: Principal is member of group request
    /// </summary>
    /// <param name="uri">Principal Uri</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<MembershipQueryResult>?> GetMembershipAsync(string uri, CancellationToken ct)
    {
        return await Db.CollectionGroup
            .Include(cg => cg.Member)
            .Include(cg => cg.Group)
            .Where(cg => cg.Member.Uri == uri)
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.GroupId,
                GroupUri = m.Group.Uri,
                MemberId = m.MemberId,
                MemberUri = m.Member.Uri,
                Username = m.Member.Owner.Username,
                Displayname = m.Member.DisplayName,
                PrincipalType = m.Member.PrincipalType!.Label,
                CollectionSubType = m.Member.CollectionSubType,
                IsMember = true,
            })
            .ToListAsync(ct);

        // return await Db.Collection
        //     .Include(c => c.Groups).ThenInclude(m => m.PrincipalType)
        //     .Where(c => c.Uri == uri && c.CollectionType == CollectionType.Principal)
        //     .ToListAsync(ct);
    }

    public async Task<List<MembershipQueryResult>> GetPrincipalMembershipsAsync(int principalId, IEnumerable<CollectionSubType> subTypes, CancellationToken ct)
    {
        return await Db.CollectionGroup
            .Include(cg => cg.Member)
            .Include(cg => cg.Group)
            .Where(cg => cg.MemberId == principalId && subTypes.Contains(cg.Group.CollectionSubType))
            .Select(m => new MembershipQueryResult
            {
                GroupId = m.GroupId,
                GroupUri = m.Group.Uri,
                MemberId = m.MemberId,
                MemberUri = m.Member.Uri,
                Username = m.Group.Owner.Username,
                Displayname = m.Group.DisplayName,
                PrincipalType = m.Group.PrincipalType!.Label,
                CollectionSubType = m.Group.CollectionSubType,
                IsMember = true,
            })
            .ToListAsync(ct);
    }

    public async Task<bool> AddGroupMemberAsync(Collection group, Collection member, CancellationToken ct)
    {
        if (group.Members.Any(c => c.Id == member.Id))
        {
            return false;    // nothing to do, is already a member
        }
        group.Members.Add(member);
        await Db.SaveChangesAsync(ct);
        await RebuildPrivilegesAsync(member, ct);
        return true;
    }

    public async Task<bool> RemoveGroupMemberAsync(Collection group, Collection member, CancellationToken ct)
    {
        if (!group.Members.Any(c => c.Id == member.Id))
        {
            return false;    // nothing to do, not a member
        }
        var groupGrants = await Db.GrantRelation
           .Where(x => x.GranteeId == group.Id && x.IsIndirect == false)
           .Join(Db.GrantRelation, p => p.GrantorId, i => i.GrantorId, (p, i) => i)
           .Where(i => i.GranteeId == member.Id && i.IsIndirect == true)
           .ExecuteDeleteAsync(ct);
        group.Members.Remove(member);
        await Db.SaveChangesAsync(ct);
        await RebuildPrivilegesAsync(member, ct);
        return true;
    }


    public async Task AmendGroupMembersAsync(MembershipRequest request, CancellationToken ct)
    {
        var memberUriList = request.Groups.SelectMany(x => x.Members.Select(m => m.Uri)).Distinct(System.StringComparer.Ordinal);
        if (memberUriList is null || !memberUriList.Any())
        {
            return;
        }
        var collectionGroupRefs = await GetGroupRefIdAsync(request.Groups.Select(x => x.Uri), ct);
        var memberRefs = await GetGroupRefIdAsync(memberUriList, ct);
        var groupIdList = collectionGroupRefs.Select(x => x.Id);
        var currentMembers = await Db.CollectionGroup.Where(cg => groupIdList.Contains(cg.GroupId)).ToListAsync(ct);
        foreach (var grp in request.Groups)
        {
            var groupRef = collectionGroupRefs.First(gr => string.Equals(gr.Uri, grp.Uri, System.StringComparison.Ordinal));
            foreach (var member in grp.Members)
            {
                var memberRef = memberRefs.First(m => string.Equals(m.Uri, member.Uri, System.StringComparison.Ordinal));
                var existing = currentMembers.FirstOrDefault(cm => cm.GroupId == groupRef.Id && memberRef.Id == cm.MemberId);
                if (member.MembershipType != MembershipPrivilegeType.Unassigned)
                {
                    if (existing is null)
                    {
                        Db.CollectionGroup.Add(new CollectionGroup { GroupId = groupRef.Id, MemberId = memberRef.Id });
                    }   // else -> nothing to do as already exists
                }
                else
                {
                    if (existing is not null)
                    {
                        Db.CollectionGroup.Remove(existing);
                    }  // else -> nothing to delete
                }
            }
        }
        await Db.SaveChangesAsync(ct);
        foreach (var member in memberRefs)
        {
            await RebuildPrivilegesAsync(member.Id, ct);
        }
    }

    public async Task AmendGroupMembersAsync(Collection group, List<Models.Principal> members, CancellationToken ct)
    {
        var memberIds = members.Select(m => m.Id).ToList();
        // TODO: Check something like await Db.Entry(group).Collection(g => g.Members).LoadAsync(ct); to avoid re-loading the group
        var currentGroup = await GetMembersAsync(group.Id, ct);
        if (currentGroup is null)
        {
            return;
        }
        var proxyGroup = await GetOtherProxyGroupAsync(group, ct);

        var toRemove = currentGroup.Members.Where(m => !memberIds.Contains(m.Id)).ToList();
        toRemove.ForEach(d => currentGroup.Members.Remove(d));
        if (proxyGroup is not null)
        {
            var toRemoveProxy = proxyGroup.Members.Where(m => memberIds.Contains(m.Id)).ToList();
            toRemoveProxy.ForEach(d => proxyGroup.Members.Remove(d));
        }

        foreach (var memberId in memberIds)
        {
            var existing = currentGroup.Members.FirstOrDefault(cm => cm.Id == memberId);
            if (existing is not null)
            {
                continue; // already a member
            }
            var memberCollection = await Db.Collection.Where(c => c.Id == memberId && c.CollectionType == CollectionType.Principal).FirstOrDefaultAsync(ct);
            if (memberCollection is not null)
            {
                currentGroup.Members.Add(memberCollection);
            }
        }
        await Db.SaveChangesAsync(ct);
        foreach (var member in members)
        {
            await RebuildPrivilegesAsync(member, ct);
        }
    }


    private async Task<List<CollectionRefId>> GetGroupRefIdAsync(IEnumerable<string> uri, CancellationToken ct)
    {
        return await Db.Collection
            // .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => uri.Any(u => u == c.Uri) && c.CollectionType == CollectionType.Principal)
            .Select(c => new CollectionRefId { Uri = c.Uri, Id = c.Id })
            .ToListAsync(ct);
    }

    private async Task<Collection?> GetOtherProxyGroupAsync(Collection group, CancellationToken ct)
    {
        string? proxyUri;
        if (group.IsProxyRead())
        {
            proxyUri = $"{group.ParentContainerUri}{CollectionUris.CalendarProxyWrite}/";
        }
        else if (group.IsProxyWrite())
        {
            proxyUri = $"{group.ParentContainerUri}{CollectionUris.CalendarProxyRead}/";
        }
        else
        {
            return null;
        }
        return await GetMembersAsync(proxyUri, ct);
    }
}
