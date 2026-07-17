using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Calendare.Server.Repository;

public class SiteRepository
{
    private readonly CalendareContext Db;
    private readonly IDavStorage? Storage;

    public SiteRepository(CalendareContext calendareContext, IDavStorage? storage = null)
    {
        Storage = storage;
        Db = calendareContext;
    }


    /// <summary>
    /// Deletes whole site (all users, all collections, all calender and addressbook data)
    ///
    /// WARNING: Use for test runs, never in production
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<int> DeleteAllAsync(bool resetInstallation, CancellationToken ct)
    {
        await Db.SyncJournal.ExecuteDeleteAsync(ct);
        await Db.CalendarMessage.ExecuteDeleteAsync(ct);
        if (!resetInstallation)
        {
            return await Db.Usr.Where(u => u.Id != 1).ExecuteDeleteAsync(ct);
        }
        var cnt = await Db.Usr.ExecuteDeleteAsync(ct);
        await Db.TrxJournal.ExecuteDeleteAsync(ct);
        await Db.UsrCredentialType.ExecuteDeleteAsync(ct);
        await Db.PrincipalType.ExecuteDeleteAsync(ct);
        await Db.GrantType.ExecuteDeleteAsync(ct);
        await Db.__DataMigrationHistory.ExecuteDeleteAsync(ct);
        return cnt;
    }

    public async Task AddTrxJournalAsync(TrxJournal trxJournal)
    {
        Db.TrxJournal.Add(trxJournal);
        await Db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task<int> DeleteTrxJournalAsync(CancellationToken ct)
    {
        var cnt = await Db.TrxJournal.ExecuteDeleteAsync(ct);
        return cnt;
    }

    public async Task<int> GarbageCollectionAsync(CancellationToken ct)
    {
        if (Storage is null)
        {
            Log.Information("WebDAV storage not configured; no garbage collection performed");
            return 0;
        }
        // TODO: [LONGTERM] This doesn't work for a large installation, we need to break up the
        //       request.
        var locationKeys = await Db.ObjectBlob.Select(ob => ob.Location).ToHashSetAsync(ct);
        await Storage.Cleanup(locationKeys, ct);
        return 0;
    }
}
