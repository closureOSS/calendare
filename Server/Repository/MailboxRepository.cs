using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Repository;

public partial class MailboxRepository
{
    private readonly CalendareContext Db;

    public MailboxRepository(CalendareContext calendareContext)
    {
        Db = calendareContext;
    }

    // DEBUG INTERFACE
    public async Task<SchedulingMessage?> GetMailboxItemByIdAsync(int id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var mailboxItems = await Db.CalendarMessage
            .AsNoTracking()
            .Where(ci => ci.Id == id)
            .FirstOrDefaultAsync(ct);
        return mailboxItems;
    }

    public async Task<List<SchedulingMessage>> ListMailboxItems(MailboxQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await SearchMailboxQuery(query).ToListAsync(ct);
    }

    private IQueryable<SchedulingMessage> SearchMailboxQuery(MailboxQuery query)
    {
        var sql = Db.CalendarMessage.AsQueryable();
        if (!string.IsNullOrEmpty(query.Uid))
        {
            sql = sql.Where(ci => ci.Uid == query.Uid);
        }
        if (!string.IsNullOrEmpty(query.SenderEmail))
        {
            sql = sql.Where(ci => ci.SenderEmail == query.SenderEmail);
        }
        if (!query.IncludeProcessed)
        {
            sql = sql.Where(ci => ci.Processed == null);
        }
        if (!query.IsTracking)
        {
            sql = sql.AsNoTracking();
        }
        sql = sql.OrderBy(ci => ci.Id);
        return sql;
    }


}
