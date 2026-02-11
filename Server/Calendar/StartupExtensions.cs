using System;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Calendar;

public static class StartupExtensions
{
    public delegate TimezoneResolverFn? CalendarBuilderOptions(IServiceProvider provider);

    public static IServiceCollection AddVSyntaxReaderExtended(this IServiceCollection services, CalendarBuilderOptions? options = null)
    {
        services.AddSingleton<ICalendarBuilder, CalendarBuilder>((provider) =>
        {
            return options is not null ? new CalendarBuilder(options(provider)) : new CalendarBuilder();
        });
        // services.AddTransient<ICalendarParser, CalendarParser>();
        return services;
    }
}
