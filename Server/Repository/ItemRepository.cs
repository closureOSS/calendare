using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Utils;
using Calendare.Server.Webpush;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;

namespace Calendare.Server.Repository;

public partial class ItemRepository
{
    private readonly CalendareContext Db;
    private readonly InternalQueue<SyncMsg> Queue;


    public ItemRepository(InternalQueue<SyncMsg> queue, CalendareContext calendareContext)
    {
        Queue = queue;
        Db = calendareContext;
    }

    // DEBUG INTERFACE
    public async Task<CollectionObject?> ListCollectionObjectsByIdAsync(int id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var collectionObjects = await Db.CollectionObject
            .AsNoTracking()
            .Include(c => c.Collection)
            .Include(c => c.CalendarItem)
            .Include(c => c.AddressItem)
            .Where(ci => ci.Id == id)
            .FirstOrDefaultAsync(ct);
        return collectionObjects;
    }

    // DEBUG INTERFACE
    public async Task<List<CollectionObject>> ListCollectionObjectsByUidAsync(string uid, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var collectionObjects = await Db.CollectionObject
            .AsNoTracking()
            .Include(c => c.Collection)
            .Include(c => c.CalendarItem)
            .Include(c => c.AddressItem)
            .Where(ci => ci.Uid == uid)
            .OrderBy(ci => ci.Id)
            .ToListAsync(ct);
        return collectionObjects;
    }


    public async Task<List<CollectionObject>> ListCollectionObjectsAsync(Collection collection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // TODO: permissions ???
        var collectionObjects = await Db.CollectionObject
            // .AsNoTracking()
            .Include(c => c.CalendarItem)
            .Include(c => c.AddressItem)
            .Where(ci => ci.CollectionId == collection.Id && !(ci.CalendarItem == null && ci.AddressItem == null) && ci.Deleted == null)
            .OrderBy(x => x.Uri)
            .ToListAsync(ct);
        return collectionObjects;
    }

    public async Task<List<CollectionObject>> ListCalendarObjectsAsync(CalendarObjectQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // TODO: permissions ???
        var collectionObjects = Db.CollectionObject
            .Include(c => c.CalendarItem)
            .Include(c => c.Collection)
            .Where(ci => ci.CalendarItem != null && ci.Deleted == null)
            ;
        if (query.CollectionIds is not null)
        {
            collectionObjects = collectionObjects.Where(ci => query.CollectionIds.Contains(ci.CollectionId));
        }
        if (query.OwnerId is not null)
        {
            collectionObjects = collectionObjects.Where(ci => query.OwnerId == ci.OwnerId);
        }
        if (query.ExcludePrivate)
        {
            collectionObjects = collectionObjects.Where(c => c.IsPrivate == false);
        }
        if (query.VObjectTypes is not null && query.VObjectTypes.Count > 0)
        {
            collectionObjects = collectionObjects.Where(c => query.VObjectTypes.Contains(c.VObjectType));
        }
        if (query.ExcludeTransparent)
        {
            collectionObjects = collectionObjects.Where(c => c.CalendarItem!.Transp == null || c.CalendarItem!.Transp != "TRANSPARENT");    // TODO: Replace const string
        }

        if (query.Period is not null)
        {
            collectionObjects = collectionObjects.Where(c =>
                       ((c.CalendarItem!.Dtstart >= query.Period.Value.Start || c.CalendarItem!.Dtend >= query.Period.Value.Start)
                    && (c.CalendarItem!.Dtstart < query.Period.Value.End || c.CalendarItem!.Dtend < query.Period.Value.End))
                    || (c.CalendarItem!.FirstInstanceStart != null && c.CalendarItem!.LastInstanceEnd != null
                    && (c.CalendarItem!.FirstInstanceStart >= query.Period.Value.Start || c.CalendarItem!.LastInstanceEnd >= query.Period.Value.Start)
                    && (c.CalendarItem!.FirstInstanceStart < query.Period.Value.End || c.CalendarItem!.LastInstanceEnd < query.Period.Value.End))
            );
        }
        if (!query.IsTracking)
        {
            collectionObjects = collectionObjects.AsNoTracking();
        }
        return await collectionObjects.OrderBy(x => x.Uri).ToListAsync(ct);
    }

    public async Task<List<SyncJournal>> ListCollectionObjectsAsync(int collectionId, Guid rangeStartId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (rangeStartId == Guid.Empty)
        {
            return await Db.CollectionObject
                .Include(c => c.CalendarItem)
                .Include(c => c.AddressItem)
                .Where(ci => ci.CollectionId == collectionId && !(ci.CalendarItem == null && ci.AddressItem == null) && ci.Deleted == null)
                .OrderBy(x => x.Uri)
                .Select(x => new SyncJournal
                {
                    CollectionObject = x,
                    Uri = x.Uri,
                    IsDeleted = false,
                    CollectionId = x.CollectionId,
                })
                .ToListAsync(ct);
        }
        // TODO: permissions ???
        var amendedItems = await Db.SyncJournal
            // .AsNoTracking()
            .Include(j => j.CollectionObject).ThenInclude(j => j!.CalendarItem)
            .Include(j => j.CollectionObject).ThenInclude(j => j!.AddressItem)
            .Where(x => x.CollectionId == collectionId && x.Id > rangeStartId && (x.IsDeleted == true || x.CollectionObjectId != null))
            .OrderBy(x => x.CollectionObject!.Uri)
            .ToListAsync(ct);

        return amendedItems;
    }

    public async Task LoadCalendarObject(CollectionObject? collectionObject, CancellationToken ct)
    {
        if (collectionObject is not null && collectionObject.CalendarItem is not null)
        {
            await Db.Entry(collectionObject.CalendarItem).Collection(ci => ci.Attendees).LoadAsync(ct);
        }
    }

    public async Task<List<CollectionObject>> ListByUriAsync(string[] uris, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // TODO: permissions ???

        var calendarItems = await Db.CollectionObject
            .Include(c => c.CalendarItem)
            .Include(c => c.AddressItem)
            .Where(ci => uris.Contains(ci.Uri) && ci.Deleted == null)
            .ToListAsync(ct);
        return calendarItems;
    }

    public async Task<CollectionObject?> CreateAsync(CollectionObject data, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Db.CollectionObject.Add(data);
        await TrackSyncChanges(data, false, ct);
        await Db.SaveChangesAsync(ct);
        return data;
    }

    public async Task<CollectionObject?> UpdateAsync(CollectionObject data, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var transaction = Db.Database.BeginTransaction();
        if (data.Id != 0)
        {
            var oldsync = await Db.SyncJournal.Where(c => c.CollectionId == data.CollectionId && c.CollectionObjectId == data.Id).FirstOrDefaultAsync(ct);
            if (oldsync is not null)
            {
                oldsync.CollectionObjectId = null;
            }
            data.Modified = SystemClock.Instance.GetCurrentInstant();
        }
        await TrackSyncChanges(data, false, ct);

        await Db.SaveChangesAsync(ct);
        transaction.Commit();
        return data;
    }

    public async Task<SchedulingRequest> AmendAsync(SchedulingRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var transaction = Db.Database.BeginTransaction();
        var syncJournal = new List<SyncJournal>();
        switch (request.OpCode)
        {
            case Handlers.DbOperationCode.Delete:
                syncJournal.Add(await DeleteWithSyncJournalAsync(request.Origin, ct));
                break;
            default:
                syncJournal.Add(await AmendWithSyncJournalAsync(request.Origin, ct));
                break;
        }
        foreach (var so in request.SchedulingObjects)
        {
            syncJournal.Add(await AmendWithSyncJournalAsync(so, ct));
        }
        foreach (var tc in request.TrashcanObjects)
        {
            syncJournal.Add(await DeleteWithSyncJournalAsync(tc, ct));
        }
        foreach (var external in request.ExternalObjects)
        {
            Db.CalendarMessage.Add(new SchedulingMessage
            {
                Body = external.Body,
                ReceiverEmail = external.EmailTo,
                SenderEmail = external.EmailFrom,
                Uid = external.Uid,
                Created = SystemClock.Instance.GetCurrentInstant(),
            });
        }
        await TrackSyncChanges(syncJournal, ct);

        await Db.SaveChangesAsync(ct);
        transaction.Commit();
        return request;
    }

    private async Task<SyncJournal> AmendWithSyncJournalAsync(CollectionObject data, CancellationToken ct)
    {
        if (data.Id != 0)
        {
            var oldsync = await Db.SyncJournal.Where(c => c.CollectionId == data.CollectionId && c.CollectionObjectId == data.Id).FirstOrDefaultAsync(ct);
            if (oldsync is not null)
            {
                oldsync.CollectionObjectId = null;
            }
            data.Modified = SystemClock.Instance.GetCurrentInstant();
        }
        else
        {
            Db.Add(data);
        }
        return new SyncJournal
        {
            CollectionId = data.CollectionId,
            CollectionObjectId = data.Id,
            Uri = data.Uri,
            IsDeleted = false
        };
    }

    private async Task<SyncJournal> DeleteWithSyncJournalAsync(CollectionObject data, CancellationToken ct)
    {
        if (data.Id != 0)
        {
            var oldsync = await Db.SyncJournal.Where(c => c.CollectionId == data.CollectionId && c.CollectionObjectId == data.Id).FirstOrDefaultAsync(ct);
            if (oldsync is not null)
            {
                oldsync.CollectionObjectId = null;
            }
            Db.CollectionObject.Remove(data);
        }
        return new SyncJournal
        {
            CollectionId = data.CollectionId,
            CollectionObjectId = data.Id,
            Uri = data.Uri,
            IsDeleted = true
        };
    }


    public async Task<int> CreateAsync(List<CollectionObject> dataList, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Db.AddRange(dataList);
        await TrackSyncChanges([.. dataList.Select(data => new SyncJournal
        {
            CollectionId = data.CollectionId,
            CollectionObjectId = data.Id,
            Uri = data.Uri,
            IsDeleted = false
        })], ct);

        return await Db.SaveChangesAsync(ct);
    }

    public async Task<int> MoveAsync(CollectionObject source, Collection target, string destination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var transaction = Db.Database.BeginTransaction();
        Db.CollectionObject.Remove(source);
        await TrackSyncChanges(source, true, ct);
        await Db.SaveChangesAsync(ct);
        source.Id = 0;
        source.Collection = target;
        source.CollectionId = target.Id;
        source.Uri = destination;
        Db.CollectionObject.Add(source);
        await TrackSyncChanges(source, false, ct);
        var result = await Db.SaveChangesAsync(ct);
        transaction.Commit();
        return result;
    }

    private async Task TrackSyncChanges(CollectionObject data, bool isDelete, CancellationToken ct)
    {
        await TrackSyncChanges([new SyncJournal
        {
            CollectionId = data.CollectionId,
            CollectionObjectId = isDelete ? null : data.Id,
            Uri = data.Uri,
            IsDeleted = isDelete
        }], ct);
    }

    private async Task TrackSyncChanges(List<SyncJournal> changes, CancellationToken ct)
    {
        var collectionId = changes.FirstOrDefault()?.CollectionId;
        if (collectionId is not null)
        {
            Db.SyncJournal.AddRange(changes);
            foreach (var sc in changes)
            {
                await Queue.Push(new SyncMsg(sc.CollectionId, sc.CollectionObjectId));
            }
        }
    }

    public async Task<CollectionObject?> DeleteAsync(string uri, CancellationToken ct)
    {
        var existing = await Db.CollectionObject.FirstOrDefaultAsync(co => co.Uri == uri && co.Deleted == null, ct);
        if (existing is not null)
        {
            await TrackSyncChanges(existing, true, ct);
            Db.CollectionObject.Remove(existing);

            await Db.SaveChangesAsync(ct);
        }
        return existing;
    }

    public async Task<Guid?> VerifySyncToken(int collectionId, Guid tokenId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (tokenId == Guid.Empty)
        {
            var collection = await Db.Collection.AsNoTracking().FirstOrDefaultAsync(c => c.Id == collectionId, ct);
            if (collection is not null)
            {
                return Guid.Empty;
            }
        }
        else
        {
            var journal = await Db.SyncJournal
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == tokenId && c.CollectionId == collectionId && c.Issued != null, ct);
            if (journal is not null)
            {
                return journal.Id;
            }
        }
        return null;
    }

    public async Task<SyncToken> GetCurrentSyncToken(int collectionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var journal = await Db.SyncJournal
            .Where(c => c.CollectionId == collectionId)
            .OrderByDescending(c => c.Created).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct)
            ;
        if (journal is null)
        {
            // TODO: we need to create an sentinel ... to optimize journal
            return new SyncToken { CollectionId = collectionId, Id = Guid.Empty, Created = SystemClock.Instance.GetCurrentInstant(), };
        }
        if (journal.Issued is null)
        {
            journal.Issued = SystemClock.Instance.GetCurrentInstant();
            await Db.SaveChangesAsync(ct);
        }
        return new SyncToken { CollectionId = journal.CollectionId, Id = journal.Id, Created = journal.Created };
    }

    public async Task<SyncToken?> GetLatestSyncToken(string collectionUri, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var collection = await Db.Collection.FirstOrDefaultAsync(c => c.Uri == collectionUri, ct);
        if (collection is not null)
        {
            var journal = await Db.SyncJournal
                .AsNoTracking()
                .Where(c => c.CollectionId == collection.Id && c.Issued != null)
                .OrderByDescending(c => c.Created).ThenByDescending(c => c.Id)
                .FirstOrDefaultAsync(ct)
                ;
            if (journal is null)
            {
                return new SyncToken { CollectionId = collection.Id, Id = Guid.Empty, Created = SystemClock.Instance.GetCurrentInstant(), };
            }
            return new SyncToken { CollectionId = journal.CollectionId, Id = journal.Id, Created = journal.Created };
        }
        Log.Error("Collection doesn't exist: {uri}", collectionUri);
        return null;
    }

}
