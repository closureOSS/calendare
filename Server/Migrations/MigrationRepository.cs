using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Server.Options;
using Calendare.Server.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;

namespace Calendare.Server.Migrations;

partial class MigrationRepository : IMigrationRepository
{
    private readonly CalendareContext Context;
    private readonly BootstrapOptions BootstrapOptions;
    private readonly StaticDataRepository StaticData;

    public MigrationRepository(IOptions<BootstrapOptions> options, CalendareContext context, StaticDataRepository staticData)
    {
        Context = context;
        BootstrapOptions = options.Value;
        StaticData = staticData;
    }

    public async Task Migrate(CancellationToken ct)
    {
        Log.Warning("Data migration in progress ...");
        await Migrate(nameof(Initial_Migration), Initial_Migration, ct);
        await Migrate(nameof(StaticDataUpdate01_Migration), StaticDataUpdate01_Migration, ct);
        Log.Warning("Data migration done");
    }

    private async Task<bool> IsOpen(string migrationId, CancellationToken ct)
    {
        var hit = await Context.__DataMigrationHistory.Where(x => x.Id == migrationId).FirstOrDefaultAsync(ct);
        if (hit is not null)
        {
            return hit.CompletedOn is null;
        }
        Context.__DataMigrationHistory.Add(new() { Id = migrationId });
        await Context.SaveChangesAsync(ct);
        return true;
    }

    private async Task Completed(string migrationId, CancellationToken ct)
    {
        var hit = await Context.__DataMigrationHistory.Where(x => x.Id == migrationId).FirstOrDefaultAsync(ct);
        if (hit is not null)
        {
            hit.CompletedOn = SystemClock.Instance.GetCurrentInstant();
            await Context.SaveChangesAsync(ct);
        }
        else
        {
            Log.Fatal("Migration {migrationId} is unregistred?", migrationId);
            throw new Exception($"Migration {migrationId} is unregistred?");
        }
    }

    private async Task<bool> Migrate(string migrationId, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        if (await IsOpen(migrationId, ct))
        {
            Log.Information("Migration {migrationId} starting ...", migrationId);
            using var transaction = await Context.Database.BeginTransactionAsync(ct);
            try
            {
                await action(ct);
                await Completed(migrationId, ct);
                await transaction.CommitAsync(ct);
                Log.Information("Migration {migrationId} completed", migrationId);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Migration {migrationId} failed", migrationId);
                throw;
            }
        }
        return true;
    }

    public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken ct)
    {
        var migrations = await Context.__DataMigrationHistory.OrderBy(c => c.CreatedOn).Select(m => m.Id).ToListAsync(ct);
        return migrations;
    }
}
