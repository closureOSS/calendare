using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Calendare.Server.Repository;

public class PushSubscriptionRepository
{
    private readonly CalendareContext Db;

    public PushSubscriptionRepository(CalendareContext calendareContext)
    {
        Db = calendareContext;
    }

    public async Task<PushSubscription?> GetByDestinationUri(int userId, int collectionId, string pushUri, CancellationToken ct)
    {
        var query = Db.PushSubscription.AsQueryable();
        query = query.Where(ps => ps.UserId == userId && ps.ResourceId == collectionId);
        query = query.Include(ps => ps.User);
        query = query.Include(ps => ps.Resource);
        var result = await query.FirstOrDefaultAsync(ps => ps.PushDestinationUri == pushUri, ct);
        return result;
    }

    public async Task<PushSubscription> Amend(PushSubscription subscription, CancellationToken ct)
    {
        if (subscription.Id == 0)
        {
            Db.PushSubscription.Add(subscription);
        }
        await Db.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<PushSubscription?> GetBySubscriptionId(int userId, string subscriptionId, CancellationToken ct)
    {
        var query = Db.PushSubscription.AsQueryable();
        query = query.Where(ps => ps.UserId == userId);
        query = query.Include(ps => ps.User);
        query = query.Include(ps => ps.Resource);
        var result = await query.FirstOrDefaultAsync(ps => ps.SubscriptionId == subscriptionId, ct);
        return result;
    }

    public async Task<List<PushSubscription>> ListByCollectionId(int collectionId, CancellationToken ct)
    {
        var query = Db.PushSubscription.AsQueryable();
        query = query.Where(ps => ps.ResourceId == collectionId);
        query = query.Include(ps => ps.User);
        query = query.Include(ps => ps.Resource);
        query = query.AsNoTracking();
        var result = await query.ToListAsync(ct);
        return result;
    }

    public async Task<PushSubscription?> Delete(int userId, string subscriptionId, CancellationToken ct)
    {
        var subscription = await GetBySubscriptionId(userId, subscriptionId, ct);
        if (subscription is null)
        {
            return null;
        }
        Db.PushSubscription.Remove(subscription);
        await Db.SaveChangesAsync(ct);
        return subscription;
    }

    private async Task<PushSubscription?> GetForUpdate(PushSubscription subscription, CancellationToken ct)
    {
        return await Db.PushSubscription.FirstOrDefaultAsync(ps => ps.Id == subscription.Id, ct);
    }

    public async Task<PushSubscription?> MarkFailure(PushSubscription subscription, CancellationToken ct)
    {
        var data = await GetForUpdate(subscription, ct);
        if (data is not null)
        {
            data.LastNotification ??= SystemClock.Instance.GetCurrentInstant();
            data.FailCounter++;
            await Db.SaveChangesAsync(ct);
        }
        return data;
    }


    public async Task<PushSubscription?> MarkSuccess(PushSubscription subscription, CancellationToken ct)
    {
        var data = await GetForUpdate(subscription, ct);
        if (data is not null)
        {
            data.LastNotification = SystemClock.Instance.GetCurrentInstant();
            data.FailCounter = 0;
            await Db.SaveChangesAsync(ct);
        }
        return data;
    }

}
