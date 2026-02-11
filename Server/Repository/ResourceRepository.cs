using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Calendare.Server.Repository;

public class ResourceRepository
{
    private readonly CalendareContext Db;
    private readonly string PathBase;
    private readonly PrincipalRepository PrincipalRepository;

    public ResourceRepository(CalendareContext calendareContext, PrincipalRepository principalRepository, DavEnvironmentRepository env)
    {
        Db = calendareContext;
        PathBase = env.PathBase;
        PrincipalRepository = principalRepository;
    }

    public async Task<List<DavResource>> ListPrincipalsAsResourceAsync(PrincipalListQuery query, CancellationToken ct)
    {
        var result = new List<DavResource>();
        var sql = PrincipalRepository.QueryPrincipalsAsync(query);
        await foreach (var principal in sql.OrderBy(c => c.Uri).Select(c => c.ToPrincipal()).AsAsyncEnumerable())
        {
            var uri = new CaldavUri(principal.Uri);
            var resource = new DavResource(uri)
            {
                CurrentUser = query.CurrentUser,
                Owner = principal,
                DavName = uri.Path!,
                PathBase = PathBase,
                DavEtag = principal.Etag,
                Exists = true,
                ParentResourceType = DavResourceType.Root,
                ResourceType = DavResourceType.Principal,
                Privileges = principal.GlobalPermit,
            };
            result.Add(resource);
        }
        return result;
    }

    public async Task<List<DavResource>> ListPrincipalsAsResourceAsync(DavResource parent, bool onlySelf, CancellationToken ct)
    {
        if (parent is null)
        {
            return [];
        }
        var result = new List<DavResource>();
        var directList = await Db.Collection
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => c.CollectionType == Calendare.Data.Models.CollectionType.Principal && parent.CurrentUser.Id == c.Id)
            .Select(c => c.ToPrincipal())
            .ToListAsync(ct);
        foreach (var child in directList)
        {
            var uri = new CaldavUri(child.Uri);
            var resource = new DavResource(uri)
            {
                CurrentUser = parent.CurrentUser,
                Owner = child,
                DavName = uri.Path!,
                PathBase = PathBase,
                DavEtag = child.Etag,
                Exists = true,
                Parent = parent.Current,
                ParentResourceType = CollectionType(parent.Current),
                ResourceType = DavResourceType.Principal,
                Privileges = child.Id == parent.CurrentUser.Id ? PrivilegeMask.All : parent.Privileges,
            };
            result.Add(resource);
            if (onlySelf)
            {
                break;
            }
            var permissions = await Db.GrantRelation
                .Include(c => c.Grantor).ThenInclude(c => c.PrincipalType)
                .Include(c => c.Grantor).ThenInclude(c => c.Owner)
                .Where(c => c.GranteeId == parent.Owner.Id)
                .OrderBy(c => c.Grantor.Uri)
                .ToListAsync(ct);
            if (permissions is not null && permissions.Count > 0)
            {
                foreach (var grant in permissions)
                {
                    var uriGrant = new CaldavUri(grant.Grantor.Uri);
                    var resourceGrant = new DavResource(uriGrant)
                    {
                        CurrentUser = parent.CurrentUser,
                        Owner = grant.Grantor.ToPrincipal(),
                        DavName = uriGrant.Path!,
                        PathBase = PathBase,
                        DavEtag = grant.Grantor.Etag,
                        Exists = true,
                        Parent = parent.Current,
                        ParentResourceType = CollectionType(parent.Current),
                        ResourceType = DavResourceType.Principal,
                        Privileges = grant.Privileges,
                    };
                    result.Add(resourceGrant);
                }
            }
        }
        return result;
    }

    public async Task<List<DavResource>> ListChildrenAsResourcesAsync(DavResource parent, CancellationToken ct)
    {
        if (parent is null)
        {
            return [];
        }
        var result = new List<DavResource>();
        var children = await Db.Collection
            .Include(c => c.Properties)
            .Include(c => c.Owner)
            .Where(c => c.OwnerId == parent.Owner.UserId && c.Uri != parent.DavName /*&& c.CollectionType != Calendare.Data.Models.CollectionType.Principal*/)
            .OrderBy(c => c.Uri)
            .ToListAsync(ct);
        foreach (var child in children)
        {
            var uri = new CaldavUri(child.Uri);
            var resource = new DavResource(uri)
            {
                CurrentUser = parent.CurrentUser,
                Owner = parent.Owner,
                DavName = child.Uri,
                PathBase = PathBase,
                DavEtag = child.Etag,
                Exists = true,
                Parent = parent.Current,
                Current = child,
                ParentResourceType = CollectionType(parent.Current),
                ResourceType = child.CollectionType.ToResourceType(),
                Privileges = parent.Privileges,   // TODO: Inherit is not okay, should have unmodified privileges ???
            };
            result.Add(resource);
        }
        return result;
    }

    public async Task<List<DavResource>> ListChildObjectsAsResourcessync(DavResource parent, CancellationToken ct)
    {
        if (parent is null || parent.Current is null)
        {
            return [];
        }
        var result = new List<DavResource>();
        var children = await Db.CollectionObject
            .Include(c => c.CalendarItem).Include(c => c.AddressItem)
            .Where(c => c.CollectionId == parent.Current.Id)
            .OrderBy(c => c.Uri)
            .ToListAsync(ct);
        foreach (var child in children)
        {
            var uri = new CaldavUri(child.Uri);
            var resource = new DavResource(uri)
            {
                CurrentUser = parent.CurrentUser,
                Owner = parent.Owner,
                DavName = child.Uri,
                PathBase = PathBase,
                DavEtag = child.Etag,
                Parent = parent.Current,
                Object = child,
                ParentResourceType = CollectionType(parent.Current),
                ResourceType = CollectionType(parent.Current) switch
                {
                    DavResourceType.Calendar => DavResourceType.CalendarItem,
                    DavResourceType.Addressbook => DavResourceType.AddressbookItem,
                    _ => throw new Exception(),
                }
            };
            result.Add(resource);

        }
        return result;
    }


    public async Task<DavResource> GetResourceAsync(CaldavUri uri, HttpContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await PrincipalRepository.GetCurrentUserPrincipalAsync(context.User.Identity, ct) ?? throw new InvalidOperationException("User is required");
        var owner = uri.Username is not null && string.Equals(user.Username, uri.Username, StringComparison.Ordinal) ? user : null;
        owner = uri.Username is not null && owner is null ? await PrincipalRepository.GetPrincipalAsync(
            new PrincipalQuery
            {
                CurrentUser = new(),
                Username = uri.Username,
                IsTracking = false,
            }, ct) : owner;
        var resource = new DavResource(uri)
        {
            CurrentUser = user,
            Owner = owner ?? user,
            PathBase = PathBase,
        };

        if (owner is null)
        {
            if (!string.IsNullOrEmpty(uri.Username))
            {
                Log.Error("Resource detection/Invalid/malformed URI {uri}", context.Request.GetEncodedUrl());
                resource.ParentResourceType = resource.ResourceType = DavResourceType.Unknown;
            }
            else
            {
                resource.ResourceType = DavResourceType.Root;
                resource.Exists = true;
            }
            resource.Privileges = await GetPrivilegesAsync(resource, ct);
            return resource;
        }

        if (uri.IsPrincipal())
        {
            resource.ResourceType = DavResourceType.Principal;
            resource.ParentResourceType = DavResourceType.Root;
            resource.Exists = true;
            resource.DavEtag = resource.Owner.Etag;
            resource.Privileges = await GetPrivilegesAsync(resource, ct);
        }
        else if (uri.IsDirectory == true)
        {
            resource.Current = await Db.Collection.Include(c => c.Owner).Include(c => c.PrincipalType).FirstOrDefaultAsync(z => z.Uri == uri.Path, ct);
            if (resource.Current is not null)
            {
                resource.Exists = true;
                resource.ResourceType = CollectionType(resource.Current);
                resource.DavName = resource.Current.Uri;
                resource.DavEtag = resource.Current.Etag;
                resource.Privileges = await GetPrivilegesAsync(resource, ct);
                resource.Privileges |= resource.Current.GlobalPermitSelf;
                resource.ParentResourceType = resource.Uri?.Collection?.Count == 1 ? DavResourceType.Principal : DavResourceType.Container;
            }
            else
            {
                if (uri.Collection?.Count > 1)
                {
                    var parent = await Db.Collection.Include(c => c.Owner).Include(c => c.PrincipalType).FirstOrDefaultAsync(z => z.Uri == uri.ParentCollectionPath, ct);
                    if (parent is null)
                    {
                        // Testsuite rfc5689/3112
                        Log.Error("Resource detection/Parent collection missing {uri}", resource.Uri.Path);
                        resource.ParentResourceType = DavResourceType.Unknown;
                        resource.Privileges = await GetPrivilegesAsync(resource, ct);
                        return resource;
                    }
                    resource.ParentResourceType = CollectionType(parent);
                    resource.ResourceType = DavResourceType.Container;
                    resource.Parent = parent;
                    resource.Privileges = await GetPrivilegesAsync(resource, ct);
                }
                else
                {
                    // create directly below user/principal
                    resource.ResourceType = DavResourceType.Container;  // we don't yet know the collection type
                    resource.ParentResourceType = DavResourceType.Principal;
                    resource.Privileges = await GetPrivilegesAsync(resource, ct);
                }
            }
        }
        else
        {
            resource.Parent = await Db.Collection.FirstOrDefaultAsync(z => z.Uri == uri.ParentCollectionPath, ct);
            if (resource.Parent is not null)
            {
                resource.ParentResourceType = CollectionType(resource.Parent);
                resource.Privileges = await GetPrivilegesAsync(resource, ct);

                switch (resource.ParentResourceType)
                {
                    case DavResourceType.Calendar:
                        resource.ResourceType = DavResourceType.CalendarItem;
                        resource.Object = await Db.CollectionObject.Include(c => c.CalendarItem).FirstOrDefaultAsync(z => z.Uri == uri.Path && z.Deleted == null, ct);
                        if (resource.Object is not null)
                        {
                            if (!(resource.Owner.Id == resource.CurrentUser.Id || resource.CurrentUser.Id == resource.Object.ActualUserId))
                            {
                                if (resource.Object.IsPublic)
                                {
                                    // RFC5545 => Public is DEFAULT, but not an enforcement statement
                                    // if (resource.Privileges.HasFlag(PrivilegeMask.ReadFreeBusy))
                                    // {
                                    //     resource.Privileges |= PrivilegeMask.Read;
                                    // }
                                }
                                if (resource.Object.IsPrivate)
                                {
                                    resource.Privileges = PrivilegeMask.None;
                                }
                            }
                            resource.Exists = true;
                        }
                        break;

                    case DavResourceType.Addressbook:
                        resource.ResourceType = DavResourceType.AddressbookItem;
                        resource.Object = await Db.CollectionObject.Include(c => c.AddressItem).FirstOrDefaultAsync(z => z.Uri == uri.Path && z.Deleted == null, ct);
                        if (resource.Object is not null)
                        {
                            resource.Exists = true;
                        }
                        break;

                    case DavResourceType.Container:
                        if (resource.Parent is not null)
                        {
                            switch (resource.Parent.CollectionSubType)
                            {
                                case CollectionSubType.SchedulingInbox:
                                    {
                                        resource.ResourceType = DavResourceType.CalendarItem;
                                        resource.Object = await Db.CollectionObject.Include(c => c.CalendarItem).FirstOrDefaultAsync(z => z.Uri == uri.Path && z.Deleted == null, ct);
                                        resource.Exists = resource.Object is not null;

                                    }
                                    break;

                                case CollectionSubType.WebPushSubscription:
                                    {
                                        resource.ResourceType = DavResourceType.WebSubscriptionItem;
                                        resource.Object = new CollectionObject
                                        {
                                            Uri = uri.Path ?? "/",
                                            Uid = uri.ItemName ?? "",
                                            Collection = resource.Parent,
                                        };
                                        resource.Exists = true;
                                    }
                                    break;

                                default:
                                    {
                                        resource.Current = await Db.Collection.FirstOrDefaultAsync(z => z.Uri == uri.Path, ct);
                                        if (resource.Current is not null)
                                        {
                                            resource.Exists = true;
                                        }
                                        // Testsuite ???
                                        Log.Error("TODO: Add DavResourceType ");
                                        throw new NotSupportedException($"Resource type undefined in parent {resource.Parent.CollectionSubType:o}");
                                    }
                            }
                        }
                        break;

                    default:
                        // Testsuite ???
                        Log.Error("TODO: Handle parent {parent}", resource.ParentResourceType.ToString("o"));
                        throw new NotSupportedException($"Resource type undefined {resource.ParentResourceType:o}");
                }
                if (resource.Object is not null)
                {
                    resource.DavName = resource.Object.Uri;
                    resource.DavEtag = resource.Object.Etag;
                    resource.ScheduleTag = resource.Object.ScheduleTag;
                }
                // AmendPrivilege(resource);
            }
            else
            {
                // Testsuite regression-suite/0924-MOVE-a, regression-suite/0925, regression-suite/2500-is..resources-1
                Log.Error("Resource detection/Parent collection missing {uri} {parentUri}", resource.Uri.Path, uri.ParentCollectionPath);
                resource.ParentResourceType = DavResourceType.Unknown;
                resource.Privileges = await GetPrivilegesAsync(resource, ct);
            }
        }
        return resource;
    }


    public async Task<DavResource> GetAsync(HttpContext context, CancellationToken ct)
    {
        var uri = new CaldavUri(context.Request.Path, PathBase);
        return await GetResourceAsync(uri, context, ct);
    }



    public async Task<DavResource?> GetByUidAsync(HttpContext context, string? uid, int ownerId, CollectionType collectionType, CollectionSubType collectionSubType, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(uid))
        {
            return null;
        }
        var query = Db.CollectionObject
                    .Include(c => c.Collection)
                    .Include(c => c.CalendarItem)
                    .Include(c => c.AddressItem)
                    .Where(c => c.Uid == uid
                        && c.Collection.CollectionType == collectionType
                        && c.Collection.CollectionSubType == collectionSubType
                        && c.OwnerId == ownerId
                    )
                    .Select(c => c.Uri);
        var uris = await query.AsNoTracking().ToListAsync(ct);
        if (uris is null || uris.Count != 1)
        {
            return null;
        }
        var uri = new CaldavUri(uris.First(), PathBase);
        return await GetResourceAsync(uri, context, ct);
    }

    private async Task<PrivilegeMask> GetPrivilegesAsync(DavResource resource, CancellationToken ct)
    {
        if (!(resource.ParentResourceType == DavResourceType.Calendar || resource.ParentResourceType == DavResourceType.Addressbook || resource.ParentResourceType == DavResourceType.Container))
        {
            if (resource.ResourceType == DavResourceType.Unknown || resource.ResourceType == DavResourceType.Root)
            {
                return PrivilegeMask.Read | PrivilegeMask.ReadAcl | PrivilegeMask.ReadCurrentUserPrivilegeSet;
            }
        }

        if (resource.CurrentUser.UserId == StockPrincipal.Admin)
        {
            return PrivilegeMask.All;
        }

        if (resource.Owner.Id == resource.CurrentUser.Id)
        {
            return PrivilegeMask.All & (resource.Current?.OwnerMask ?? resource.Owner.OwnerMask);
        }

        var grantorIds = new HashSet<int>
        {
            resource.Owner.Id,
        };
        if (resource.Parent is not null) { grantorIds.Add(resource.Parent.Id); }
        if (resource.Current is not null) { grantorIds.Add(resource.Current.Id); }
        var permissions = await Db.GrantRelation.Where(x => x.GranteeId == resource.CurrentUser.Id && grantorIds.Contains(x.GrantorId)).AsNoTracking().ToListAsync(ct);
        var globalPermits = resource.Current?.GlobalPermit ?? resource.Owner.GlobalPermit;
        if (permissions is not null && permissions.Count > 0)
        {
            var permission = GetGrantRelation(permissions, resource.Current);
            permission ??= GetGrantRelation(permissions, resource.Parent);
            permission ??= permissions.First();
            return (globalPermits | permission.Privileges) & (resource.Current?.AuthorizedMask ?? resource.Owner.AuthorizedMask);
        }
        return globalPermits;
    }

    private static GrantRelation? GetGrantRelation(List<GrantRelation> permissions, Collection? collection)
    {
        if (collection is null)
        {
            return null;
        }
        return permissions.FirstOrDefault(g => g.GrantorId == collection.Id);
    }

    private static DavResourceType CollectionType(Collection? c)
    {
        if (c is null)
        {
            return DavResourceType.Container;
        }
        return c.CollectionType switch
        {
            Calendare.Data.Models.CollectionType.Addressbook => DavResourceType.Addressbook,
            Calendare.Data.Models.CollectionType.Calendar => DavResourceType.Calendar,
            Calendare.Data.Models.CollectionType.Principal => DavResourceType.Principal,
            _ => DavResourceType.Container
        };
    }
}
