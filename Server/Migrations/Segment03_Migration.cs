using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Migrations;

partial class MigrationRepository
{
    private async Task Segment03_Migration(CancellationToken ct)
    {
        await AddSegmentCollectionAsync(ct);
        await AddSegmentCollectionObjectAsync(ct);

        await Context.SaveChangesAsync(ct);
    }


    private async Task AddSegmentCollectionObjectAsync(CancellationToken ct)
    {
        var dbList = await Context.CollectionObject
            .Where(c => c.Segment == "")
            .OrderBy(c => c.Id)
            .Select(c => new CollectionObject { Id = c.Id, Uri = c.Uri, Segment = c.Segment })
            .ToListAsync(ct);
        foreach (var co in dbList)
        {
            var uri = new CaldavUri(co.Uri);
            co.Segment = uri.TrailingSegment ?? "";
            Context.CollectionObject.Attach(co);
            Context.Entry(co).Property(p => p.Segment).IsModified = true;
        }
    }

    private async Task AddSegmentCollectionAsync(CancellationToken ct)
    {
        var dbList = await Context.Collection
            .Where(c => c.Segment == "")
            .OrderBy(c => c.Id)
            .Select(c => new Collection { Id = c.Id, Uri = c.Uri, Segment = c.Segment })
            .ToListAsync(ct);
        foreach (var co in dbList)
        {
            var uri = new CaldavUri(co.Uri);
            co.Segment = uri.TrailingSegment ?? uri.Username ?? "";
            Context.Collection.Attach(co);
            Context.Entry(co).Property(p => p.Segment).IsModified = true;
        }
    }
}
