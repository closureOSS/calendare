using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

//, RequiredMappingStrategy = RequiredMappingStrategy.None
[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true)]
public static partial class CalendarMessageMapper
{
    [MapperIgnoreSource(nameof(SchedulingMessage.Id))]
    private static partial MailboxItem Map(SchedulingMessage source);

    public static MailboxItem ToView(this SchedulingMessage source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        var target = Map(source);
        return target;
    }

    public static List<MailboxItem> ToView(this IEnumerable<SchedulingMessage> source)
    {
        return [.. source.Select(x => x.ToView())];
    }
}
