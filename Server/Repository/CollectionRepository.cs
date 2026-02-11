using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Api;
using Calendare.Server.Constants;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;

namespace Calendare.Server.Repository;

public class CollectionRepository
{
    private readonly CalendareContext Db;

    public CollectionRepository(CalendareContext calendareContext)
    {
        Db = calendareContext;
    }

    public async Task<Collection?> GetAsync(int id, CancellationToken ct)
    {
        // TODO: permissions ???

        var collection = await Db.Collection
        .Include(c => c.PrincipalType)
        .Include(c => c.Properties)
        .Include(c => c.Owner)
        .Include(c => c.Children).ThenInclude(h => h.PrincipalType)
        .Include(c => c.Groups).ThenInclude(g => g.PrincipalType)
        .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
        // .Include(c => c.Owner)
        .Where(c => c.Id == id)
        .AsSplitQuery()
        .FirstOrDefaultAsync(ct);
        return collection;
    }

    public async Task<Collection?> GetAsync(string path, CancellationToken ct)
    {
        // TODO: permissions ???

        var collection = await Db.Collection
        .Include(c => c.Properties)
        .Include(c => c.PrincipalType)
        .Include(c => c.Owner)
        .Where(c => c.Uri == path)
        .FirstOrDefaultAsync(ct);
        return collection;
    }

    public async Task<List<Collection>> ListByOwnerUserIdAsync(int ownerId, CancellationToken ct)
    {
        var result = await Db.Collection
            .Include(c => c.PrincipalType)
            .Where(c => c.OwnerId == ownerId)
            .OrderBy(c => c.Uri)
            .ToListAsync(ct);
        return result;
    }

    /// <summary>
    /// Query collections owned by a principal
    ///
    /// </summary>
    /// <param name="query"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<Collection>> ListByOwnerAsync(CollectionReadQuery query, CancellationToken ct)
    {
        var result = CollectionQuery(query);
        return await result.ToListAsync(ct);
    }

    private IQueryable<Collection> CollectionQuery(CollectionReadQuery query)
    {
        var sql = Db.Collection
            .Include(c => c.Owner)
            .Include(c => c.PrincipalType)
            .Where(c => c.Owner.Username == query.OwnerUsername)
            ;
        if (query.CollectionSubTypes is not null && query.CollectionSubTypes.Count != 0)
        {
            sql = sql.Where(c => query.CollectionSubTypes.Contains(c.CollectionSubType));
        }
        if (query.CollectionTypes is not null && query.CollectionTypes.Count != 0)
        {
            sql = sql.Where(c => query.CollectionTypes.Contains(c.CollectionType));
        }
        else if (!query.IncludeAllCollectionTypes)
        {
            sql = sql.Where(c =>
                    c.CollectionType == CollectionType.Calendar
                || c.CollectionType == CollectionType.Addressbook
                || c.CollectionType == CollectionType.Collection)
            ;
        }
        if (!query.IsTracking)
        {
            sql = sql.AsNoTracking();
        }
        sql = sql.OrderBy(c => c.OrderBy).ThenBy(c => c.Uri);
        return sql;
    }

    public async Task<List<GrantRelation>> ListPrivilegesAsync(int granteeId, CancellationToken ct)
    {
        var result = await Db.GrantRelation.Where(x => x.GranteeId == granteeId).AsNoTracking().ToListAsync(ct);
        return result;
    }

    public async Task<Collection?> CreateAsync(Collection collection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Db.Collection.Add(collection);
        CalculatePermissions(collection, await Db.Collection.FirstOrDefaultAsync(c => c.Id == collection.ParentId, ct));

        await Db.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<Collection?> UpdateAsync(Collection collection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Db.Collection.Add(collection);
        collection.Modified = SystemClock.Instance.GetCurrentInstant();
        // TODO: add properties

        await Db.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<Collection?> DeleteAsync(int id, CancellationToken ct)
    {
        var collection = await Db.Collection.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null)
        {
            return null;
        }
        Db.Remove(collection);
        await Db.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<bool> StoreAsync(Collection collection, CollectionAmendRequest request, CancellationToken ct)
    {
        if (!Amend(collection, request))
        {
            return false;
        }
        await Db.SaveChangesAsync(ct);
        return true;
    }

    private static bool Amend(Collection collection, CollectionAmendRequest request)
    {
        if (request.DisplayName is not null && !string.Equals(request.DisplayName, collection.DisplayName, System.StringComparison.Ordinal))
        {
            collection.DisplayName = request.DisplayName.Trim();
        }
        if (request.Description is not null && !string.Equals(request.Description, collection.Description, System.StringComparison.Ordinal))
        {
            collection.Description = request.Description;
        }
        if (request.Color is not null && !string.Equals(request.Color, collection.Color, System.StringComparison.Ordinal))
        {
            collection.Color = request.Color;
        }
        if (request.ExcludeFreeBusy is not null)
        {
            if (request.ExcludeFreeBusy.Value == true && !string.Equals(collection.ScheduleTransparency, ScheduleTransparency.Transparent, System.StringComparison.Ordinal))
            {
                collection.ScheduleTransparency = ScheduleTransparency.Transparent;
            }
            if (request.ExcludeFreeBusy.Value == false && collection.ScheduleTransparency is not null && !string.Equals(collection.ScheduleTransparency, ScheduleTransparency.Opaque, System.StringComparison.Ordinal))
            {
                collection.ScheduleTransparency = ScheduleTransparency.Opaque;
            }
        }
        if (request.Timezone is not null)
        {
            if (TimezoneParser.TryReadTimezone(request.Timezone ?? "", out var timeZone))
            {
                collection.Timezone = timeZone!.Id;
            }
            else
            {
                return false;
            }
        }
        // TODO: uri
        return true;
    }

    public static void CalculatePermissions(Collection collection, Collection? parent)
    {
        collection.GlobalPermit = collection.GlobalPermitSelf;
        collection.AuthorizedMask = collection.AuthorizedProhibit;
        collection.OwnerMask = collection.OwnerProhibit;
        if (parent is not null)
        {
            collection.GlobalPermit |= parent.GlobalPermit;
            collection.AuthorizedMask |= ~parent.AuthorizedMask;
            collection.OwnerMask |= ~parent.OwnerMask;
        }
        collection.AuthorizedMask = ~collection.AuthorizedMask;
        collection.OwnerMask = ~collection.OwnerMask;
    }

    public async Task AmendPermission(Collection collection, PermissionRequest request, CancellationToken ct)
    {
        if (request.GlobalPermitSelf is not null)
        {
            collection.GlobalPermitSelf = request.GlobalPermitSelf.Value;
        }
        if (request.AuthorizedProhibit is not null)
        {
            collection.AuthorizedProhibit = request.AuthorizedProhibit.Value;
        }
        if (request.OwnerProhibit is not null)
        {
            collection.OwnerProhibit = request.OwnerProhibit.Value;
        }
        await Db.SaveChangesAsync(ct);
        // TODO: Recalc all masks ...
        await RecalcPermissions(collection.OwnerId, ct);
        await Db.SaveChangesAsync(ct);
    }

    private async Task RecalcPermissions(int ownerId, CancellationToken ct)
    {
        var tree = await Db.Collection.Where(c => c.OwnerId == ownerId).OrderBy(c => c.Uri).ToListAsync(ct);
        var top = tree.FirstOrDefault(c => c.ParentId is null);
        if (top is null)
        {
            Log.Error("Collection tree without owner principal?", ownerId);
            return;
        }
        CalculatePermissions(top, null);
        // top.GlobalPermit = top.GlobalPermitSelf;
        // top.AuthorizedMask = top.AuthorizedProhibit;
        // top.OwnerMask = top.OwnerProhibit;
        CascadePermissions(tree, top);
    }

    private static void CascadePermissions(List<Collection> tree, Collection parent)
    {
        foreach (var collection in tree.Where(c => c.ParentId == parent.Id))
        {
            CalculatePermissions(collection, parent);
            // collection.GlobalPermit = parent.GlobalPermit | collection.GlobalPermitSelf;
            // collection.AuthorizedMask = parent.AuthorizedMask | collection.AuthorizedProhibit;
            // collection.OwnerMask = parent.OwnerMask | collection.OwnerProhibit;
            CascadePermissions(tree, collection);
        }
    }

    #region Collection Property Management

    public async Task<Collection> LoadProperties(Collection collection, CancellationToken ct)
    {
        if (collection.Properties is null || collection.Properties.Count == 0)
        {
            await Db.Entry(collection).Collection(ci => ci.Properties).LoadAsync(ct);
        }
        return collection;
    }

    public async Task<CollectionProperty?> GetProperty(Collection collection, string key, CancellationToken ct)
    {
        await LoadProperties(collection, ct);
        var collectionProperty = collection.Properties.FirstOrDefault(p => string.Equals(p.Name, key, System.StringComparison.Ordinal));
        return collectionProperty;
    }

    public async Task<CollectionProperty> AmendProperty(Collection collection, string key, string value, int userId, CancellationToken ct)
    {
        var collectionProperty = await GetProperty(collection, key, ct);
        var isNew = collectionProperty is null;
        collectionProperty ??= new CollectionProperty { CollectionId = collection.Id, Name = key };
        collectionProperty.Value = value;
        collectionProperty.ModifiedById = userId;
        if (isNew) collection.Properties.Add(collectionProperty);
        return collectionProperty;
    }

    public async Task<CollectionProperty?> DeleteProperty(Collection collection, string key, CancellationToken ct)
    {
        var collectionProperty = await GetProperty(collection, key, ct);
        if (collectionProperty is not null)
        {
            collection.Properties.Remove(collectionProperty);
        }
        return collectionProperty;
    }
    #endregion
}
