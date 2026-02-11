using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Data.Utils;
using Calendare.Server.Api.Models;
using Calendare.Server.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;

namespace Calendare.Server.Repository;

/// <summary>
/// Amend grants (privileges)
/// </summary>
public partial class UserRepository
{
    public async Task AmendRelationshipAsync(Collection grantor, List<PrivilegeGroupRequest> pvgs, CancellationToken ct)
    {
        var relationships = await Db.GrantRelation
             .Include(c => c.GrantType)
             .Where(c => c.GrantorId == grantor.Id)
             .ToListAsync(ct);
        var usernames = pvgs.SelectMany(x => x.Principals ?? [], (g, p) => p.Username).ToList();
        if (usernames is null || usernames.Count == 0)
        {
            return; // nothing to do (no implicit deletes)
        }
        var principals = await Db.Collection
            .Include(c => c.Owner)
            .Where(c => usernames.Contains(c.Owner.Username) && c.ParentId == null)
            .Select(p => new { Id = p.Id, OwnerId = p.OwnerId, Username = p.Owner.Username, Uri = p.Uri, GrantTypeId = 0, })
            .ToListAsync(ct);
        var candidateGrants = new List<GrantRelation>();
        foreach (var pvg in pvgs)
        {
            var grantType = StaticData.RelationshipTypeList.Values.FirstOrDefault(x => string.Equals(x.Confers, pvg.Code, System.StringComparison.Ordinal));
            if (grantType is null)
            {
                continue; // TODO: throw or abort all??
            }
            foreach (var priv in pvg.Principals ?? [])
            {
                var principal = principals.FirstOrDefault(p => string.Equals(p.Username, priv.Username, System.StringComparison.Ordinal));
                if (principal is null)
                {
                    continue; // TODO: throw or abort all
                }
                var candidate = new GrantRelation
                {
                    GrantorId = grantor.Id,
                    GrantTypeId = grantType.Id,
                    Privileges = grantType.Privileges,
                    GranteeId = principal.Id,
                    IsIndirect = false,
                };
                // if (candidate.GranteeId == candidate.GrantorId)
                // {
                //     continue;   // skip self relations
                // }
                if (priv.DoRemove)
                {
                    var existing = relationships.FirstOrDefault(r => r.GranteeId == candidate.GranteeId && r.GrantorId == candidate.GrantorId);
                    if (existing is not null)
                    {
                        Db.GrantRelation.Remove(existing);
                    }
                }
                else
                {
                    var existing = relationships.FirstOrDefault(r => r.GranteeId == candidate.GranteeId && r.GrantorId == candidate.GrantorId);
                    if (existing is not null)
                    {
                        if (existing.GrantTypeId != candidate.GrantTypeId)
                        {
                            existing.Modified = SystemClock.Instance.GetCurrentInstant();
                            existing.GrantTypeId = candidate.GrantTypeId;
                            existing.Privileges = candidate.Privileges;
                        }
                        if (existing.IsIndirect != candidate.IsIndirect)
                        {
                            existing.IsIndirect = false;
                        }
                        // else -> nothing to do
                    }
                    else
                    {
                        Db.GrantRelation.Add(candidate);
                    }
                }
            }
        }
        await Db.SaveChangesAsync(ct);
        // TODO: now recalculate all privileges ...
        await RebuildPrivilegesAsync(grantor.Id, ct);
        foreach (var principal in principals)
        {
            await RebuildPrivilegesAsync(principal.Id, ct);
        }
    }

    public async Task AmendRelationshipAsync(Collection grantor, List<AccessControlEntity> aces, CancellationToken ct)
    {
        var relationships = await Db.GrantRelation
            .Include(c => c.Grantee)
            .Include(c => c.GrantType)
            .Where(c => c.GrantorId == grantor.Id)
            .ToListAsync(ct);
        foreach (var ace in aces)
        {
            if (ace.Grantee is not null)
            {
                var acePrivilegeMask = ace.Privileges;
                var relTypeId = StaticData.FindCommonRelationship(acePrivilegeMask).Id;
                var existing = relationships.FirstOrDefault(x => x.GranteeId == ace.Grantee.Id);
                if (existing is not null)
                {
                    if (acePrivilegeMask == PrivilegeMask.None)
                    {
                        Db.GrantRelation.Remove(existing);
                    }
                    else if (acePrivilegeMask != existing.Privileges)
                    {
                        existing.GrantTypeId = relTypeId;
                        existing.Privileges = acePrivilegeMask;
                    }
                }
                else
                {
                    var relationship = new GrantRelation
                    {
                        GrantorId = grantor.Id,
                        GranteeId = ace.Grantee.Id,
                        GrantTypeId = relTypeId,
                        Privileges = acePrivilegeMask,
                    };
                    Db.GrantRelation.Add(relationship);
                }
            }
            else
            {
                // an "null" principal defines the default privileges
                grantor.GlobalPermitSelf = ace.Privileges;
                continue;
            }
        }
        await Db.SaveChangesAsync(ct);
        // TODO: now recalculate all privileges ...
        foreach (var member in aces.Where(ace => ace.Grantee is not null).Select(ace => ace.Grantee))
        {
            if (member is not null)
            {
                await RebuildPrivilegesAsync(member, ct);
            }
        }
    }

    public async Task<bool?> DeleteRelationshipAsync(Collection grantor, Principal granteePrincipal, CancellationToken ct)
    {
        var relationship = await Db.GrantRelation.FirstOrDefaultAsync(c => c.GrantorId == grantor.Id && c.GranteeId == granteePrincipal.Id, ct);
        if (relationship is null)
        {
            return false;
        }
        Db.GrantRelation.Remove(relationship);
        await Db.SaveChangesAsync(ct);
        await RebuildPrivilegesAsync(granteePrincipal, ct);
        return true;
    }

    /// <summary>
    /// Get privileges granted from grantor to others
    /// </summary>
    /// <param name="grantorCollectionId">Grantor Collection Id</param>
    /// <param name="transitive">Include transitive (in-direct) privileges</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<AccessControlEntity>> GetPrivilegesGrantedToAsync(IEnumerable<int> grantorCollectionId, bool transitive, CancellationToken ct)
    {
        // TODO: Implement inheritance query -> from collection to parent(s) -> combine for result
        // TODO: Add inherited flag to ACE
        var relationships = await Db.GrantRelation
          .Include(c => c.Grantee).ThenInclude(c => c.Owner)
          .Include(c => c.GrantType)
          .Where(c => grantorCollectionId.Contains(c.GrantorId) && (transitive == true || c.IsIndirect == transitive))
          .ToListAsync(ct);
        var result = new List<AccessControlEntity>();
        foreach (var rel in relationships.OrderBy(c => c.Grantee.DisplayName, System.StringComparer.Ordinal).ThenBy(c => grantorCollectionId.ToImmutableList().IndexOf(c.GrantorId)))
        {
            Log.Information($"{rel.GrantorId}:{grantorCollectionId.ToImmutableList().IndexOf(rel.GrantorId)} -> {rel.GranteeId} {rel.Grantee.Uri}");
            var exists = result.FirstOrDefault(r => r.Grantee?.Id == rel.GranteeId);
            if (exists is null)
            {
                var ace = new AccessControlEntity
                {
                    Grantee = rel.Grantee.ToPrincipal(),
                    Privileges = rel.Privileges,
                    GrantType = rel.GrantType,
                    IsIndirect = rel.IsIndirect,
                    IsInherited = grantorCollectionId.ToImmutableList().IndexOf(rel.GrantorId) != 0,
                };
                result.Add(ace);
            }
            else
            {
                // TODO: Merge rights ..
                exists.Privileges = exists.Privileges.ToBitArray().Or(rel.Privileges.ToBitArray()).FromBitArray();
                exists.GrantType = StaticData.FindCommonRelationship(exists.Privileges);
            }
        }
        return result;
    }

    public async Task<List<AccessControlEntity>> GetPrivilegesGrantedToAsync(DavResource resource, bool transitive, CancellationToken ct)
    {
        var grantorCollectionId = await UnnestHierarchy(resource, ct);
        return await GetPrivilegesGrantedToAsync(grantorCollectionId, transitive, ct);
    }

    public async Task<List<AccessControlEntity>> GetPrivilegesGrantedByAsync(int granteeId, bool transitive, CancellationToken ct)
    {
        var relationships = await Db.GrantRelation
          .Include(c => c.Grantor).ThenInclude(c => c.Owner)
          .Include(c => c.GrantType)
          .Where(c => c.GranteeId == granteeId && (transitive == true || c.IsIndirect == transitive))
          .OrderBy(c => c.Grantor.DisplayName)
          .AsNoTracking()
          .ToListAsync(ct);
        var result = new List<AccessControlEntity>();
        foreach (var rel in relationships)
        {
            var ace = new AccessControlEntity
            {
                Grantor = rel.Grantor,
                Privileges = rel.Privileges,
                GrantType = rel.GrantType,
                IsIndirect = rel.IsIndirect,
            };
            result.Add(ace);
        }
        return result;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<ImmutableList<int>> UnnestHierarchy(DavResource resource, CancellationToken ct)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        // bottom up
        List<int> collectionIds = [];
        if (resource.Current is not null)
        {
            collectionIds.Add(resource.Current.Id);
        }
        collectionIds.Add(resource.Owner.Id);
        // TODO: Resolve more nesting levels
        return [.. collectionIds];
    }

    public async Task<GrantType> GetRelationshipTypeAsync(string searchTerm, CancellationToken ct)
    {
        var rt = await Db.GrantType.Where(x => x.Confers == searchTerm || x.Name == searchTerm).FirstOrDefaultAsync(ct) ?? await Db.GrantType.Where(x => x.Id == 1).FirstOrDefaultAsync(ct);
        return rt!;
    }



}
