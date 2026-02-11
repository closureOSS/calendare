using System.Collections.Generic;
using Calendare.Data.Models;
using Calendare.VSyntaxReader.Components;
using Serilog;

namespace Calendare.Server.Calendar;

public static class CalendarBuilderExtensions
{
    public static List<ICalendarComponent> LoadCalendars(this ICalendarBuilder calendarBuilder, IEnumerable<CollectionObject> collectionObjects)
    {
        List<ICalendarComponent> components = [];
        foreach (var co in collectionObjects)
        {
            var parseResult = calendarBuilder.Parser.TryParse(co.RawData, out var vCalendar, $"{co.Id}");
            if (parseResult && vCalendar is not null)
            {
                components.AddRange(vCalendar.Children);
            }
            else
            {
                Log.Error("Failed to parse {id} {errMsg}", co.Id, parseResult.ErrorMessage);
            }
        }
        return components;
    }

#if DEBUG
    public static string SerializeForDebug(this ICalendarBuilder calendarBuilder, IEnumerable<ICalendarComponent> calendarComponents)
    {
        var vCalendarTest = calendarBuilder.CreateCalendar();
        foreach (var cc in calendarComponents)
        {
            if (cc is not VTimezone)
            {
                vCalendarTest.AddChild(cc);
            }
        }
        return vCalendarTest.Serialize();
    }
#endif
}
