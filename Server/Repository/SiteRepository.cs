using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Repository;

public class SiteRepository
{
    private readonly CalendareContext Db;

    public SiteRepository(CalendareContext calendareContext)
    {
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

    public async Task AddTrxJournal(TrxJournal trxJournal)
    {
        Db.TrxJournal.Add(trxJournal);
        await Db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task<int> DeleteTrxJournal(CancellationToken ct)
    {
        var cnt = await Db.TrxJournal.ExecuteDeleteAsync(ct);
        return cnt;
    }

}
