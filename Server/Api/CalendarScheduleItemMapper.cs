using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class CalendarScheduleItemMapper
{
    private static partial CalendarScheduleItem Map(CollectionObject source);
    private static partial void MapInto(ObjectCalendar source, CalendarScheduleItem target);

    public static CalendarScheduleItem ToView(this SyncJournal source)
    {
        if (source.CollectionObject is null || source.CollectionObject.CalendarItem is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        var target = Map(source.CollectionObject);
        MapInto(source.CollectionObject.CalendarItem, target);
        return target;
    }

    public static CalendarScheduleItem ToView(this CollectionObject source)
    {
        if (source is null || source.CalendarItem is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        var target = Map(source);
        MapInto(source.CalendarItem, target);
        return target;
    }

    public static List<CalendarScheduleItem> ToView(this IEnumerable<SyncJournal> source)
    {
        return [.. source.Select(x => x.ToView())];
    }

    public static List<CalendarScheduleItem> ToView(this IEnumerable<CollectionObject> source)
    {
        return [.. source.Select(x => x.ToView())];
    }
}
