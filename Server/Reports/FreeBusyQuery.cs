using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Calendar;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Serilog;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc4791#section-7.10
/// </summary>
public class FreeBusyQueryReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var ct = httpContext.RequestAborted;
        if (!(resource.ResourceType == DavResourceType.Principal || resource.ResourceType == DavResourceType.Calendar) || !resource.Exists)
        {
            Log.Error("Current resource {uri}/{resourcetype} not supported", resource.DavName, resource.ResourceType);
            return new(HttpStatusCode.BadRequest);
        }

        var timeRangeFilter = TimeRangeFilter.Parse(xmlRequestDoc.Root!);
        if (timeRangeFilter is null || !timeRangeFilter.IsValid() || timeRangeFilter.IsUnresticted())
        {
            return new(HttpStatusCode.BadRequest, "All valid freebusy requests MUST contain a time-range filter.");
        }
        var calendarBuilder = httpContext.RequestServices.GetRequiredService<ICalendarBuilder>();

        var itemRepository = httpContext.RequestServices.GetRequiredService<ItemRepository>();
        var evalRange = SafeEvalRange(timeRangeFilter);
        var query = new CalendarObjectQuery
        {
            CurrentUser = resource.CurrentUser,
            OwnerId = resource.Owner.UserId,
            Period = evalRange,
            ExcludePrivate = resource.Owner.UserId != resource.CurrentUser.UserId,
            ExcludeTransparent = true,
            VObjectTypes = [ComponentName.VEvent, ComponentName.VAvailability],
        };
        if (resource.ResourceType == DavResourceType.Calendar && resource.Current is not null)
        {
            query.CollectionIds = [resource.Current.Id];
        }
        var calendarObjects = await itemRepository.ListCalendarObjectsAsync(query, httpContext.RequestAborted);

        var calendarComponents = calendarBuilder.LoadCalendars(calendarObjects);
        var availabilities = await httpContext.LoadAvailabilityProperty(resource.Owner.UserId, ct);
        if (availabilities is not null)
        {
            calendarComponents.AddRange(availabilities);
        }
#if DEBUG
        var debugFreeBusyCalendar = calendarBuilder.SerializeForDebug(calendarComponents);
#endif
        var timezone = GetTimezoneForFloatingDates(resource.Owner, calendarObjects);
        var freeBusyEntries = calendarComponents.GetFreeBusyEntries(evalRange, calendarTimeZone: timezone);
        var vCalendar = calendarBuilder.CreateCalendar();
        var freebusyComponent = vCalendar.CreateChild<VFreebusy>()!;
        freebusyComponent.DateStamp = default;
        // freebusyComponent.Uid = Guid.NewGuid().ToString(); // TODO: From request or empty
        freebusyComponent.DateStart = evalRange.Start;
        freebusyComponent.DateEnd = evalRange.End;
        freebusyComponent.SetFreeBusyEntries(freeBusyEntries);

        return new(vCalendar.Serialize(), $"{MimeContentTypes.VCalendar}; {MimeContentTypes.Utf8}");
    }

    public static Interval SafeEvalRange(TimeRangeFilter timeRangeFilter, int safeDays = 365)
    {
        var evalRange = timeRangeFilter.ToInterval();
        if (!evalRange.HasEnd && !evalRange.HasStart)
        {
            // open range - limit to -1y +2y from now
            var now = SystemClock.Instance.GetCurrentInstant();
            return new Interval(now.Plus(Duration.FromDays(-safeDays)), now.Plus(Duration.FromDays(safeDays)));
        }
        else if (!evalRange.HasEnd)
        {
            // open ended
            return new Interval(evalRange.Start, evalRange.Start.Plus(Duration.FromDays(safeDays)));
        }
        else if (!evalRange.HasStart)
        {
            // no start
            return new Interval(evalRange.End.Plus(Duration.FromDays(-safeDays)), evalRange.End);
        }
        return evalRange;
    }

    public static DateTimeZone GetTimezoneForFloatingDates(Principal principal, List<CollectionObject>? calendarObjects = null)
    {
        var calenderComponentsTimezones = calendarObjects?.Where(c => c.Collection is not null && !string.IsNullOrEmpty(c.Collection.Timezone)).Select(c => c.Collection.Timezone).Distinct(StringComparer.Ordinal).FirstOrDefault();
        if (!TimezoneParser.TryReadTimezone(calenderComponentsTimezones ?? principal.Timezone ?? "UTC", out var timezone))
        {
        }
        return timezone ?? DateTimeZone.Utc;
    }
}
