using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Calendare.Server.Migrations;

public interface IMigrationRepository
{
    public Task Migrate(CancellationToken ct);
    public Task<List<string>> GetAppliedMigrationsAsync(CancellationToken ct);
}
