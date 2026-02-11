using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Calendare.Server.Api.Models;
using Calendare.Server.Options;
using Calendare.Server.Utils.HttpUserAgent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Calendare.Server.Repository;

public class DavEnvironmentRepository
{
    public readonly string PathBase;
    private readonly List<ClientFeatureSet> Features;
    public bool IsTestMode { get; set; }

    public DavEnvironmentRepository(IOptions<CalendareOptions> options)
    {
        PathBase = options.Value.PathBase;
        Features = options.Value.Features;
        IsTestMode = options.Value.IsTestMode;
    }

    public bool HasFeatures(CalendareFeatures feature, HttpContext? httpContext) => HasFeatures(feature, Detect(httpContext));

    public bool HasFeatures(CalendareFeatures feature, CalendarClientType calendarClientType)
    {
        bool? decision = null;
        var defaultClient = Features.FirstOrDefault(c => c.ClientType == CalendarClientType.Default);
        if (defaultClient is not null)
        {
            if (defaultClient.Enable.Contains(feature))
            {
                decision = true;
            }
            if (defaultClient.Disable.Contains(feature))
            {
                decision = false;
            }
        }
        var actualClient = Features.FirstOrDefault(c => c.ClientType == calendarClientType);
        if (actualClient is not null)
        {
            if (actualClient.Disable.Contains(feature))
            {
                return false;
            }
            if (actualClient.Enable.Contains(feature))
            {
                return true;
            }
        }
        return decision ?? false;
    }

    public ImmutableArray<CalendarClientType> GetFeatureSets()
    {
        return [.. Features.Select(x => x.ClientType).Distinct()];
    }

    public ImmutableArray<CalendareFeatures> ResolveFeatures(CalendarClientType calendarClientType)
    {
        var defaultClient = Features.FirstOrDefault(c => c.ClientType == CalendarClientType.Default);
        var actualClient = Features.FirstOrDefault(c => c.ClientType == calendarClientType);
        var result = new List<CalendareFeatures>();
        foreach (var feature in Enum.GetValues<CalendareFeatures>())
        {
            if (HasFeatures(feature, calendarClientType))
            {
                result.Add(feature);
            }
        }
        return [.. result.OrderBy(c => c.ToString("g"), StringComparer.Ordinal)];
    }

    private static CalendarClientType Detect(HttpContext? httpContext)
    {
        var userAgent = httpContext?.DetectUserAgent();
        if (userAgent is null)
        {
            return CalendarClientType.NotDetected;
        }
        return userAgent.Value.CalendarClientType;
    }

}
