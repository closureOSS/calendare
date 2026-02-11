using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Server.Constants;
using Calendare.VSyntaxReader.Components;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Repository;

public class SiteStatisticsRepository
{
    private readonly CalendareContext Db;

    public SiteStatisticsRepository(CalendareContext calendareContext)
    {
        Db = calendareContext;
    }

    // TODO: Re-implement a more condensed report without private information
    public async Task<List<StatisticsCollectionResult>> ComputeCollectionStatistics(CancellationToken ct)
    {
        var sql = Db.Collection
            .Include(c => c.PrincipalType)
            .Where(c => c.OwnerId != StockPrincipal.Admin)
            .OrderBy(c => c.Owner.Username).ThenBy(c => c.CollectionType).ThenBy(c => c.CollectionSubType).ThenBy(c => c.Uri)
            ;
        var sql2 = sql.Select(c => new StatisticsCollectionResult
        {
            Username = c.Owner.Username,
            DisplayName = c.DisplayName,
            Uri = c.Uri,
            CollectionType = c.CollectionType,
            CollectionSubType = c.CollectionSubType,
            PrincipalTypeName = c.PrincipalType!.Name,
            VEventCount = c.Objects.Count(o => o.VObjectType == ComponentName.VEvent),
            VJournalCount = c.Objects.Count(o => o.VObjectType == ComponentName.VJournal),
            VAvailabilityCount = c.Objects.Count(o => o.VObjectType == ComponentName.VAvailability),
            VTodoCount = c.Objects.Count(o => o.VObjectType == ComponentName.VTodo),
            VPollCount = c.Objects.Count(o => o.VObjectType == ComponentName.VPoll),
            VCardCount = c.Objects.Count(o => o.VObjectType == "VCARD"),
            PropertyCount = c.Properties.Count(),
        });
        return await sql2.ToListAsync(ct);
    }
}
