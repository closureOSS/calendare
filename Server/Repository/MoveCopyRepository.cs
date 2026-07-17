using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Storage;
using Calendare.Server.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Calendare.Server.Repository;

public class MoveCopyRepository(CalendareContext Db, ItemRepository ItemRepository)
{
    public async Task<int> MoveAsync(CollectionObject source, Collection target, string uri, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var transaction = await Db.Database.BeginTransactionAsync(ct);
        Db.CollectionObject.Remove(source);
        await ItemRepository.TrackSyncChanges(source, isDelete: true, ct);
        await Db.SaveChangesAsync(ct);
        var newUri = new CaldavUri(uri);
        source.Id = 0;
        source.Collection = target;
        source.CollectionId = target.Id;
        source.Segment = newUri.TrailingSegment!;
        source.Uri = newUri.Path!;
        Db.CollectionObject.Add(source);
        await ItemRepository.TrackSyncChanges(source, isDelete: false, ct);
        var result = await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<CollectionObject?> PrepareMoveAsync(CollectionObject source, CollectionObject? overwrite, IDavStorage storage, CancellationToken ct)
    {
        if (source?.BlobItem is null)
        {
            return null;
        }
        if (overwrite?.BlobItem is not null)
        {
            storage.Add(new StorageRequest
            {
                Operation = StorageOperation.Delete,
                ObjectBlobId = overwrite.BlobItem.Id,
                Location = overwrite.BlobItem.Location,
                ContentLength = overwrite.BlobItem.ContentLength,
                ContentType = overwrite.BlobItem.ContentType,
            });
        }
        return source;
    }

    public async Task CommitMoveAsync(CollectionObject source, Collection target, string uri, CollectionObject? existing, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var transaction = await Db.Database.BeginTransactionAsync(ct);
        if (existing is not null)
        {
            var current = await Db.CollectionObject.FirstOrDefaultAsync(co => co.Id == existing.Id && co.Deleted == null, ct);
            if (current is not null)
            {
                Db.CollectionObject.Remove(current);
                await Db.SaveChangesAsync(ct);
            }
        }
        if (source.CollectionId != target.Id)
        {
            source.CollectionId = target.Id;
        }
        var newUri = new CaldavUri(uri);
        source.Segment = newUri.TrailingSegment!;
        source.Uri = newUri.Path!;
        // source.BlobItem?.LastAccess = SystemClock.Instance.GetCurrentInstant();
        source.Modified = SystemClock.Instance.GetCurrentInstant();
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<long> ComputeQuotaUsedAsync(int ownerId, CancellationToken ct)
    {
        var collectionTree = await ReadCollectionTreeLightAsync(ownerId, null, ct);
        var collectionIds = collectionTree.Select(colt => colt.Id).ToList();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var quota = await Db.Collection
            .Where(cco => collectionIds.Contains(cco.Id))
            .SelectMany(c => c.Objects.Where(o => o.Deleted == null))
            .Where(cco => cco.BlobItem != null)
            .SumAsync(cco => cco.BlobItem.ContentLength, ct);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        return quota ?? 0;
    }

    public async Task<IList<Collection>> RetrieveCollectionTreeFullAsync(Collection? source, CancellationToken ct)
    {
        if (source is null)
        {
            return [];
        }
        var collectionTree = await ReadCollectionTreeLightAsync(source.OwnerId, source.Id, ct);
        var collectionIds = collectionTree.Select(colt => colt.Id).ToList();
        var objs = await Db.Collection
                    .Where(cco => collectionIds.Contains(cco.Id))
                    .Include(cco => cco.Objects.Where(o => o.Deleted == null))
                    .ThenInclude(cco => cco.BlobItem)
                    .ToListAsync(ct);
        return objs;
    }

    public async Task<IList<Collection>> PrepareCopyToAsync(Collection? source, int parentId, CaldavUri targetUri, Collection? existing, IDavStorage storage, CancellationToken ct)
    {
        if (source is null)
        {
            return [];
        }
        await TrackBlobDeletionAsync(existing, storage, ct);

        var collections = await RetrieveCollectionTreeFullAsync(source, ct);
        if (collections.Count == 0) return [];
        bool isFirstRow = true;
        foreach (var collection in collections)
        {
            Db.Entry(collection).State = EntityState.Detached;
            collection.Id = 0;
            collection.PermanentId = Guid.NewGuid();
            if (isFirstRow)
            {
                collection.ParentId = parentId;
                collection.Parent = null;
                collection.Uri = targetUri.Path!;
                collection.Segment = targetUri.TrailingSegment!;
                // TODO: handle owner
                isFirstRow = false;
            }
            else
            {
                // TODO: handle parent
                // TODO: handle owner
                var collectionUri = new CaldavUri(UriUtils.ToFolderPath([.. UriUtils.ToSegments(collection.Parent?.Uri!), UriUtils.EncodeSlash(collection.Segment)]));
                collection.Uri = collectionUri.Path!;
                collection.Segment = collectionUri.TrailingSegment!;
            }
            foreach (var obj in collection.Objects)
            {
                Db.Entry(obj).State = EntityState.Detached;
                obj.Id = 0;
                obj.CollectionId = 0;
                obj.Uid = Guid.NewGuid().ToString();
                // TODO: handle owner
                // TODO: handle actual user
                var objUri = new CaldavUri(UriUtils.ToPath([.. UriUtils.ToSegments(collection.Uri), UriUtils.EncodeSlash(obj.Segment)]));
                obj.Uri = objUri.Path!;
                // obj.Segment = objUri.TrailingSegment!;   // segment doesn't change for collection copy
                if (obj.BlobItem is not null)
                {
                    var refBlobId = obj.BlobItem.Id;
                    Db.Entry(obj.BlobItem).State = EntityState.Detached;
                    obj.BlobItem.Id = 0;
                    obj.BlobItem.CollectionObjectId = 0;
                    var sr = new StorageRequest
                    {
                        Operation = StorageOperation.Copy,
                        ObjectBlobId = refBlobId,
                        Location = obj.BlobItem.Location,
                        TargetLocation = await storage.GetFilename(obj.Uri, ct),
                        ContentLength = obj.BlobItem.ContentLength,
                        ContentType = obj.BlobItem.ContentType,
                    };
                    obj.BlobItem.Location = sr.TargetLocation;
                    storage.Add(sr);
                }
            }
            // TODO: Handle Properties, Groups, Members, Grants?
        }
        return collections;
    }

    public async Task<CollectionObject?> PrepareCopyToAsync(CollectionObject source, DavResource target, CollectionObject? overwrite, IDavStorage storage, CancellationToken ct)
    {
        if (source?.BlobItem is null)
        {
            return null;
        }
        Db.Entry(source).State = EntityState.Detached;
        source.Id = 0;
        source.Collection = target.Parent!;
        source.Uid = Guid.NewGuid().ToString();
        source.Uri = target.Uri.Path!;
        source.Segment = target.Uri.TrailingSegment!;
        source.OwnerId = target.Owner.UserId;
        source.ActualUserId = target.CurrentUser.UserId;
        if (source.BlobItem is not null)
        {
            var refBlobId = source.BlobItem.Id;
            Db.Entry(source.BlobItem).State = EntityState.Detached;
            source.BlobItem.Id = 0;
            source.BlobItem.CollectionObjectId = 0;
            // source.BlobItem.CollectionObject = source;
            var sr = new StorageRequest
            {
                Operation = StorageOperation.Copy,
                ObjectBlobId = refBlobId,
                Location = source.BlobItem.Location,
                TargetLocation = await storage.GetFilename(source.Uri, ct),
                ContentLength = source.BlobItem.ContentLength,
                ContentType = source.BlobItem.ContentType,
            };
            source.BlobItem.Location = sr.TargetLocation;
            storage.Add(sr);
        }
        if (overwrite?.BlobItem is not null)
        {
            storage.Add(new StorageRequest
            {
                Operation = StorageOperation.Delete,
                ObjectBlobId = overwrite.BlobItem.Id,
                Location = overwrite.BlobItem.Location,
                ContentLength = overwrite.BlobItem.ContentLength,
                ContentType = overwrite.BlobItem.ContentType,
            });
        }
        return source;
    }

    public async Task CommitCopyToAsync(CollectionObject source, CollectionObject? overwrite, CancellationToken ct)
    {
        using var transaction = await Db.Database.BeginTransactionAsync(ct);
        if (overwrite is not null)
        {
            var existing = await Db.CollectionObject.SingleOrDefaultAsync(c => c.Id == overwrite.Id, ct);
            if (existing is not null)
            {
                Db.Remove(existing);
                await Db.SaveChangesAsync(ct);
            }
        }
        Db.CollectionObject.Add(source);
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CommitCopyToAsync(IList<Collection> collections, Collection? overwrite, CancellationToken ct)
    {
        using var transaction = await Db.Database.BeginTransactionAsync(ct);
        if (overwrite is not null)
        {
            var existing = await Db.Collection.SingleOrDefaultAsync(c => c.Id == overwrite.Id, ct);
            if (existing is not null)
            {
                Db.Remove(existing);
                await Db.SaveChangesAsync(ct);
            }
        }
        foreach (var collection in collections)
        {
            Db.Collection.Add(collection);
        }
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<IList<Collection>> ReadCollectionTreeLightAsync(int ownerId, int? collectionId, CancellationToken ct)
    {
        var tree = await Db.Collection
            .Where(c => c.OwnerId == ownerId && c.CollectionType == CollectionType.Collection && c.CollectionSubType == CollectionSubType.Default)
            .Select(c => new Collection
            {
                Id = c.Id,
                ParentId = c.ParentId,
                OwnerId = c.OwnerId,
                Uri = c.Uri,
                Segment = c.Segment,
                ParentContainerUri = c.ParentContainerUri,
            })
            .ToListAsync(ct);
        WeakTreeSort(tree, collectionId);
        return tree.AsReadOnly();
    }

    public async Task TrackBlobDeletionAsync(Collection? existing, IDavStorage storage, CancellationToken ct)
    {
        if (existing is null) return;
        var removeCollections = await RetrieveCollectionTreeFullAsync(existing, ct);
        if (removeCollections.Count > 0)
        {
            storage.AddRange(removeCollections
                .SelectMany(col => col.Objects)
                .Where(o => o.BlobItem != null)
                .Select(o => new StorageRequest
                {
                    Operation = StorageOperation.Delete,
                    ObjectBlobId = o.BlobItem?.Id,
                    Location = o.BlobItem?.Location,
                    ContentType = o.BlobItem?.ContentType,
                    ContentLength = o.BlobItem?.ContentLength,
                })
            );
        }
    }

    public async Task<Collection?> PrepareMoveAsync(Collection source, string uri, Collection targetParent, Collection? existing, IDavStorage storage, CancellationToken ct)
    {
        await TrackBlobDeletionAsync(existing, storage, ct);
        if (source.OwnerId != targetParent.OwnerId)
        {
            source.OwnerId = targetParent.OwnerId;
        }
        if (source.ParentId != targetParent.Id)
        {
            source.ParentId = targetParent.Id;
        }
        // Pure rename, source and target are siblings
        var newUri = new CaldavUri(uri);
        source.Segment = newUri.TrailingSegment!;
        source.Uri = newUri.Path!;
        source.ParentContainerUri = newUri.ParentCollectionPath!;
        source.Modified = SystemClock.Instance.GetCurrentInstant();
        var collectionTree = await RebuildUriOfCollectionTree(source, ct);

        // Change all collectionobjects. Uri needs to be changed
        var collectionIds = collectionTree.Select(colt => colt.Id).ToList();
        var objs = await Db.CollectionObject
                .Where(cco => collectionIds.Contains(cco.CollectionId) && cco.Deleted == null)
                .Include(co => co.BlobItem)
                .ToListAsync(ct);
        foreach (var item in objs)
        {
            var col = collectionTree.FirstOrDefault(ctt => ctt.Id == item.CollectionId);
            if (col is null) continue; // TODO: this would be inconsistent, throw
            var targetLocation = UriUtils.ToPath([.. UriUtils.ToSegments(col.Uri), UriUtils.EncodeSlash(item.Segment)]);
            if (item.BlobItem is not null)
            {
                storage.Add(new StorageRequest
                {
                    Operation = StorageOperation.Move,
                    ObjectBlobId = item.BlobItem.Id,
                    Location = item.BlobItem.Location,
                    TargetLocation = targetLocation,
                    ContentLength = item.BlobItem.ContentLength,
                    ContentType = item.BlobItem.ContentType,
                });
            }
            item.Uri = targetLocation;
        }
        return source;
    }

    public async Task CommitMoveAsync(Collection source, Collection? existing, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // if (source.ParentId is null || targetParent is null)
        // {
        //     return;
        // }
        using var transaction = await Db.Database.BeginTransactionAsync(ct);

        if (existing is not null)
        {
            var current = await Db.Collection.FirstOrDefaultAsync(c => c.Id == existing.Id, ct);
            if (current is not null)
            {
                Db.Collection.Remove(current);
                await Db.SaveChangesAsync(ct);
            }
        }
        // if (source.OwnerId != targetParent.OwnerId)
        // {
        //     source.OwnerId = targetParent.OwnerId;
        // }
        // if (source.ParentId != targetParent.Id)
        // {
        //     source.ParentId = targetParent.Id;
        // }
        // // Pure rename, source and target are siblings
        // var newUri = new CaldavUri(uri);
        // source.Segment = newUri.TrailingSegment!;
        // source.Uri = newUri.Path!;
        // source.ParentContainerUri = newUri.ParentCollectionPath!;
        // source.Modified = SystemClock.Instance.GetCurrentInstant();
        // var collectionTree = await RebuildUriOfCollectionTree(source, ct);

        // // Change all collectionobjects. Uri needs to be changed
        // var collectionIds = collectionTree.Select(colt => colt.Id).ToList();
        // var objs = await Db.CollectionObject
        //         .Where(cco => collectionIds.Contains(cco.CollectionId) && cco.Deleted == null)
        //         .ToListAsync(ct);
        // foreach (var item in objs)
        // {
        //     var col = collectionTree.FirstOrDefault(ctt => ctt.Id == item.CollectionId);
        //     if (col is null) continue; // TODO: this would be inconsistent, throw
        //     item.Uri = UriUtils.ToPath([.. UriUtils.ToSegments(col.Uri), UriUtils.EncodeSlash(item.Segment)]);
        // }

        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }


    private async Task<IList<Collection>> RebuildUriOfCollectionTree(Collection source, CancellationToken ct)
    {
        var tree = await ReadCollectionTreeLightAsync(source.OwnerId, source.Id, ct);
        foreach (var co in tree)
        {
            if (co.Id == source.Id) continue; // skip parent
            var parent = co.ParentId == source.Id ? source : tree.FirstOrDefault(tc => tc.Id == co.ParentId);
            if (parent is null) continue; // TODO: this would be inconsistent, throw
            co.ParentContainerUri = parent.Uri;
            co.Uri = UriUtils.ToFolderPath([.. UriUtils.ToSegments(co.ParentContainerUri), UriUtils.EncodeSlash(co.Segment)]);
            Db.Collection.Attach(co);
            Db.Entry(co).Property(p => p.Uri).IsModified = true;
            Db.Entry(co).Property(p => p.ParentContainerUri).IsModified = true;
        }
        return tree.AsReadOnly();
    }

    private static void WeakTreeSort(List<Collection> collections, int? parentId)
    {
        var inTreeMap = new HashSet<int>(collections.Count)
        {
            parentId ?? 0,
        };
        var indexMap = new Dictionary<int, int>(collections.Count);
        for (int i = 0; i < collections.Count; i++)
        {
            indexMap[collections[i].Id] = i;
        }

        for (int i = 0; i < collections.Count; i++)
        {
            var current = collections[i];
            if (inTreeMap.Contains(current.ParentId ?? -1))
            {
                inTreeMap.Add(current.Id);
            }

            // If it has a parent and that parent is present in the list
            if (current.ParentId.HasValue && indexMap.TryGetValue(current.ParentId.Value, out int parentIndex))
            {
                // If the child is positioned BEFORE its parent, we must move it
                if (i < parentIndex)
                {
                    // Remove child from its current position
                    collections.RemoveAt(i);

                    // The parent's index has now shifted down by 1 because we removed an item before it
                    parentIndex--;

                    // Insert the child right after its parent
                    collections.Insert(parentIndex + 1, current);

                    // Re-index the affected range in our map
                    for (int j = i; j <= parentIndex + 1; j++)
                    {
                        indexMap[collections[j].Id] = j;
                    }

                    // Evaluate the item that shifted into the current index 'i'
                    i--;
                }
            }
        }
        if (parentId is not null) collections.RemoveAll(c => !inTreeMap.Contains(c.Id));
    }
}
