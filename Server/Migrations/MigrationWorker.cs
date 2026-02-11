using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Calendare.Server.Migrations;

public class MigrationWorker : BackgroundService
{
    private readonly IServiceProvider ServiceProvider;

    public MigrationWorker(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var migrationRepository = scope.ServiceProvider.GetRequiredService<IMigrationRepository>();
            await migrationRepository.Migrate(stoppingToken);
        }
    }
}
