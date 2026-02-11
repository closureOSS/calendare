using System;
using System.Linq;
using System.Threading.Tasks;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Models;
using Calendare.VSyntaxReader.Operations;
using Calendare.VSyntaxReader.Properties;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Text;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository ObjectCalendarProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Object?.CalendarItem?.Summary))
                {
                    prop.Value = resource.Object?.CalendarItem?.Summary ?? "";
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.5
            Name = XmlNs.Dav + "getcontenttype",
            TypeRestrictions = [DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = $"{MimeContentTypes.VCalendar}; component=vevent"; // TODO: vevent must be variable ???
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.3
            Name = XmlNs.Dav + "getcontentlanguage",
            TypeRestrictions = [DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-9.3 and
            // https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.10 for avoiding conflicts when updating scheduling object resources
            Name = XmlNs.Caldav + "schedule-tag",
            TypeRestrictions = [DavResourceType.CalendarItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.ScheduleTag)) prop.Value = $"\"{resource.ScheduleTag}\"";
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4791#section-9.6
            // TODO: Investigate support for COMP->PROP and ALLCOMP->ALLPROP (we currently return always all components and properties)
            Name = XmlNs.Caldav + "calendar-data",
            TypeRestrictions = [DavResourceType.CalendarItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null && resource.Object.RawData is not null)
                {
                    var expandMode = qry?.Element(XmlNs.Caldav + "expand");
                    if (expandMode is null)
                    {
                        prop.Value = resource.Object.RawData;
                    }
                    else
                    {
                        // TODO: Refactor attribute parsing for expand timerange
                        var startAttr = expandMode.Attribute("start");
                        var endAttr = expandMode.Attribute("end");
                        var start = Parse(startAttr?.Value);
                        var end = Parse(endAttr?.Value);
                        if (startAttr is null && endAttr is null)
                        {
                            // TODO: expand to infinity or what??
                            prop.Value = resource.Object.RawData;
                        }
                        else
                        {
                            var calendarBuilder = ctx.RequestServices.GetRequiredService<ICalendarBuilder>();

                            // https://www.rfc-editor.org/rfc/rfc4791#section-9.6.5
                            var parseResult = calendarBuilder.Parser.TryParse(resource.Object.RawData, out var calendar, $"{resource.Owner.Id}");
                            if (!parseResult || calendar is null)
                            {
                                return Task.FromResult(PropertyUpdateResult.BadRequest);
                            }
                            var hasReccurring = calendar.Children.OfType<RecurringComponent>().Any();
                            if (!hasReccurring)
                            {
                                prop.Value = resource.Object.RawData;
                                return Task.FromResult(PropertyUpdateResult.Success);
                            }
                            var expandedCalendar = calendarBuilder.CreateCalendar();
                            var occurrences = calendar.GetOccurrences(new Interval(start.Value.ToInstant(), end.Value.ToInstant()));
                            foreach (var occurrenceItem in occurrences)
                            {
                                if (occurrenceItem.IsReccurring == false) // this is the "real" occurrence - no repeating at all
                                {
                                    expandedCalendar.AddChild(occurrenceItem.Source);
                                    continue;
                                }
                                var occurrence = occurrenceItem.Source.CopyTo<RecurringComponent>(expandedCalendar) ?? throw new InvalidOperationException(nameof(occurrenceItem));
                                if (occurrence.RecurrenceId is null)
                                {
                                    occurrence.RemoveProperties([PropertyName.RecurrenceRule, PropertyName.RecurrenceDate, PropertyName.RecurrenceExceptionDate, PropertyName.RecurrenceExceptionRule]);
                                    if (occurrenceItem.Source.DateStart?.IsDateOnly == true)
                                    {
                                        occurrence.RecurrenceId = new CaldavDateTime(occurrenceItem.Interval.Start.InZone(occurrenceItem.Source.DateStart?.Zone ?? DateTimeZone.Utc).LocalDateTime.Date);
                                    }
                                    else
                                    {
                                        occurrence.RecurrenceId = new CaldavDateTime(occurrenceItem.Interval.Start.InUtc());
                                    }
                                    occurrence.DateStart = new CaldavDateTime(occurrenceItem.Interval.Start.InZone(occurrenceItem.Source.DateStart?.Zone ?? DateTimeZone.Utc), occurrenceItem.Source.DateStart?.IsDateOnly ?? false);
                                    if (occurrence is VEvent vEvent)
                                    {
                                        if (vEvent.DateEnd is not null)
                                        {
                                            vEvent.DateEnd = new CaldavDateTime(occurrenceItem.Interval.End.InZone(vEvent.DateEnd.Zone ?? DateTimeZone.Utc), occurrenceItem.Source.DateStart?.IsDateOnly ?? false);
                                        }
                                        else if (vEvent.Duration is not null)
                                        {
                                            var durationInSeconds = Period.FromSeconds(Convert.ToInt64(occurrenceItem.Interval.Duration.TotalSeconds));
                                            var durationNormalized = durationInSeconds.Normalize();
                                            if (durationNormalized != vEvent.Duration)
                                            {
                                                vEvent.Duration = durationNormalized;
                                            }
                                        }
                                    }
                                }
                            }
                            var serializedCalendar = expandedCalendar.Serialize();
                            prop.Value = serializedCalendar;
                        }
                    }
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-report-set",
            TypeRestrictions = [DavResourceType.CalendarItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.AddSupportedReports(CommonReports);
                prop.AddSupportedReports([
                    XmlNs.Caldav + "calendar-query",
                    XmlNs.Caldav + "calendar-multiget",
                    XmlNs.Caldav + "free-busy-query"
                ]);
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        return repo;
    }

    private static ParseResult<OffsetDateTime> Parse(string? time)
    {
        // 20060104T000000Z
        var pattern = OffsetDateTimePattern.CreateWithInvariantCulture("yyyyMMddTHHmmsso<G>");
        // if (time.Length == 5)
        // {
        // pattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");
        // }
        return pattern.Parse(time ?? "");
    }
}
