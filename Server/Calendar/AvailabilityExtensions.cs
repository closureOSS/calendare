using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Calendar;

public static class AvailabilityExtensions
{
    public const string PROPERTY_calendar_availability = "calendar-availability";

    /// <summary>
    /// MacOS calendar application stores a "default" availability as
    /// property of the users inbox calendar (this is an observation)
    /// </summary>
    /// <returns></returns>
    public static async Task<List<VAvailability>?> LoadAvailabilityProperty(this HttpContext httpContext, int userId, CancellationToken ct)
    {
        var userRepository = httpContext.RequestServices.GetRequiredService<UserRepository>();
        var collections = await userRepository.GetCollectionsByType(userId, CollectionSubType.SchedulingInbox, ct);
        if (collections is null || collections.Count != 1)
        {
            return null;
        }
        var inboxCollection = collections.First();
        var collectionRepository = httpContext.RequestServices.GetRequiredService<CollectionRepository>();

        var prop = await collectionRepository.GetProperty(inboxCollection, PROPERTY_calendar_availability, ct);
        if (prop is null || string.IsNullOrEmpty(prop.Value))
        {
            return null;
        }
        var calendarBuilder = httpContext.RequestServices.GetRequiredService<ICalendarBuilder>();
        var parseResult = calendarBuilder.Parser.TryParse(prop.Value, out var vCalendar, $"{userId}/inbox/");
        if (parseResult && vCalendar is not null)
        {
            return [.. vCalendar.Children.OfType<VAvailability>()];
        }
        return null;
    }
}
