using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace Calendare.Data.Utils;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null) return result;

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry is not { State: EntityState.Deleted, Entity: IHistorize delete }) continue;
            entry.State = EntityState.Modified;
            delete.DeletedOn = SystemClock.Instance.GetCurrentInstant();
        }
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ValueTask<InterceptionResult<int>>(SavingChanges(eventData, result));
    }
}
