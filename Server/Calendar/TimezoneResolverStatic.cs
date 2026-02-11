using System;
using System.Linq;
using Calendare.Server.Options;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;

namespace Calendare.Server.Calendar;

public static partial class TimezoneResolvers
{
    public static TimezoneResolverFn? Static(IServiceProvider provider)
    {
        var timezoneAliases = provider.GetService<IOptions<CalendareOptions>>()?.Value.TimezoneAliases;
        if (timezoneAliases is null || timezoneAliases.Count == 0)
        {
            return null;
        }

        return (tzId) =>
        {
            var alias = timezoneAliases.FirstOrDefault(x => x.Alias.Equals(tzId, StringComparison.InvariantCultureIgnoreCase));
            if (alias is not null)
            {
                if (TimezoneParser.TryReadTimezone(alias.TzId, out var timeZone))
                {
                    return timeZone;
                }
                Log.Error("Timezone Alias [{tzId}] with invalid Id [{alias}]", tzId, alias.TzId);
                throw new ArgumentOutOfRangeException($"Invalid Timezone Id {alias.TzId}");
            }
            Log.Warning("Timezone Id [{tzId}] not recognized, set to UTC", tzId);
            return DateTimeZone.Utc;
        };
    }
}
