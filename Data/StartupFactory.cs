using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Calendare.Data;

public class StartupFactory : IDesignTimeDbContextFactory<CalendareContext>
{
    public StartupFactory() { }

    public CalendareContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<StartupFactory>()
            // .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();
        var optionsBuilder = new DbContextOptionsBuilder<CalendareContext>();
        optionsBuilder.ConfigureCalendareNpgsql(configuration.GetSection("Postgresql"));
        return new CalendareContext(optionsBuilder.Options);
    }
}
