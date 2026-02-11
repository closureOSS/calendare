using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Reports;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Operations;
using NodaTime;
using Serilog;

namespace Calendare.Server.Calendar;

public class FilterEvaluator
{
    private ComponentFilter? Filter;
    private readonly ICalendarBuilder CalendarBuilder;

    public FilterEvaluator(ICalendarBuilder calendarBuilder)
    {
        CalendarBuilder = calendarBuilder;
    }

    public void Compile(CalendarFilter? filter)
    {
        if (filter is null || filter.ComponentFilter is null)
        {
            return;
        }
        Filter = filter.ComponentFilter;
    }

    public void Compile(TimeRangeFilter? filter, string[]? componentNames = null)
    {
        if (filter is null)
        {
            return;
        }
        Filter = new ComponentFilter
        {
            TimeRangeFilter = filter,
            // ComponentTypes = [ComponentName.Event],
        };
        if (componentNames is not null)
        {
            Filter.ComponentTypes.AddRange(componentNames);
        }
    }

    public bool Matches(CollectionObject co)
    {
        if (Filter is null)
        {
            return true;
        }
        if (Filter.ComponentTypes.Count > 0)
        {
            if (!Filter.ComponentTypes.Contains(co.VObjectType, StringComparer.Ordinal))
            {
                return false;
            }
        }
        if (co.CalendarItem is not null)
        {
            if (Filter.TimeRangeFilter is not null)
            {
                if (Filter.TimeRangeFilter.Start != Instant.MinValue)
                {
                    if (co.CalendarItem.Rrule is null)
                    {
                        // TODO: For VTODO Dtdue
                        var isInRange = co.CalendarItem.Dtend is null || co.CalendarItem.Dtend >= Filter.TimeRangeFilter.Start;
                        if (isInRange == false)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        var isInRange = co.CalendarItem.LastInstanceEnd >= Filter.TimeRangeFilter.Start;
                        if (isInRange == false)
                        {
                            return false;
                        }
                        var parseResult = CalendarBuilder.Parser.TryParse(co.RawData, out var calendar, $"{co.Id}");
                        if (!parseResult || calendar is null)
                        {
                            return false;   // TODO: Log or throw ???
                        }
                        var occurrences = calendar.GetOccurrences(FreeBusyQueryReport.SafeEvalRange(Filter.TimeRangeFilter, 183 /* ~1/2 year */));
                        if (occurrences.Count == 0)
                        {
                            return false;
                        }
                    }
                }
                if (Filter.TimeRangeFilter.End != Instant.MaxValue)
                {
                    if (co.CalendarItem.Rrule is null)
                    {
                        var isInRange = co.CalendarItem.Dtstart is null || co.CalendarItem.Dtstart <= Filter.TimeRangeFilter.End;
                        if (isInRange == false)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        var isInRange = co.CalendarItem.FirstInstanceStart is null || co.CalendarItem.FirstInstanceStart <= Filter.TimeRangeFilter.End;
                        if (isInRange == false)
                        {
                            return false;
                        }
                        var parseResult = CalendarBuilder.Parser.TryParse(co.RawData, out var calendar, $"{co.Id}");
                        if (!parseResult || calendar is null)
                        {
                            return false;   // TODO: Log or throw ???
                        }
                        var occurrences = calendar.GetOccurrences(FreeBusyQueryReport.SafeEvalRange(Filter.TimeRangeFilter, 183 /* ~1/2 year */));
                        if (occurrences.Count == 0)
                        {
                            return false;
                        }
                    }
                }
            }
            if (Filter.PropertyFilters is not null && Filter.PropertyFilters.Count > 0)
            {
                var parseResult = CalendarBuilder.Parser.TryParse(co.RawData, out var calendar, $"{co.Id}");
                if (parseResult && calendar is not null)
                {
                    bool anyMatch = false;
                    foreach (var propFilter in Filter.PropertyFilters)
                    {
                        var matchPropFilter = MatchPropFilter(calendar, propFilter, Filter.ComponentTypes.FirstOrDefault());
                        if (matchPropFilter == true && anyMatch == false)
                        {
                            anyMatch = true;
                        }
                        if (matchPropFilter == false && /*Filter.LogicalAnd == */true)
                        {
                            return false;
                        }
                    }
                    return anyMatch;
                }
                else
                {
                    Log.Error("Failed to parse {id} {errMsg}", co.Id, parseResult.ErrorMessage);
                    return false;
                }
            }
        }
        return true;
    }

    private static bool MatchPropFilter(VCalendar vCalendar, PropertyFilter propFilter, string? componentType)
    {
        if (componentType is null)
        {
            return false;
        }
        var hasGlobalMatch = false;
        // TODO: Why only the first component (of a group); should other occurrences have no impact?
        var components = vCalendar.Children.FirstOrDefault(x => x.Name.Equals(componentType, StringComparison.InvariantCultureIgnoreCase));
        var propertyMatches = components?.Properties.Where(p => p.Name.Equals(propFilter.Name, StringComparison.InvariantCultureIgnoreCase));
        // TODO: Doesn't work with properties with Cardinality Many
        if (propertyMatches is not null && propertyMatches.Any())
        {
            var propertyCount = propertyMatches.Count();
            if (propFilter.IsNotDefined == true)
            {
                return false;
            }
            if (propFilter.TextMatches.Count == 0 && propFilter.ParamFilters.Count == 0)
            {
                return true;
            }
            foreach (var kvp in propertyMatches)
            {
                if (kvp.Raw is null)
                {
                    continue;
                }
                var testValue = kvp.Raw.Value ?? string.Empty;
                var isMatchingProperty = MatchesProperty(testValue, propFilter.TextMatches);
                if (!isMatchingProperty)
                {
                    continue;
                }
                hasGlobalMatch = true;
                foreach (var paramMatch in propFilter.ParamFilters ?? [])
                {
                    hasGlobalMatch = false;
                    var paramTestValue = kvp.Raw?.Parameters.FirstOrDefault(p => p.Name.Equals(paramMatch.Name, StringComparison.InvariantCultureIgnoreCase))?.Value;
                    if (paramTestValue is not null)
                    {
                        var isMatchingParameter = MatchesProperty(paramTestValue, paramMatch.TextMatches);
                        if (isMatchingParameter)
                        {
                            return true;
                        }
                    }
                    // else
                    // {
                    //     continue;   // TODO: is there a non defined parameter clause?
                    // }
                }
            }

        }
        else
        {
            if (propFilter.IsNotDefined == true)
            {
                return true;
            }
        }
        return hasGlobalMatch;
    }

    private static bool MatchesProperty(string testValue, List<Func<string, bool>>? textMatcher)
    {
        bool anyMatch = false;
        foreach (var textMatch in textMatcher ?? [])
        {
            var isMatching = textMatch(testValue);
            if (isMatching == true && anyMatch == false)
            {
                anyMatch = true;
            }
            if (isMatching == false)
            {
                return false;
            }
        }
        return anyMatch;
    }
}
