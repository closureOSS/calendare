using System.Collections.Generic;

namespace Calendare.Server.Api.Models;

public class FeatureByClient
{
    public required CalendarClientType ClientType { get; set; }
    public List<CalendareFeatures> Enabled { get; set; } = [];
}

public class FeatureResponse
{
    public required string Version { get; set; }
    public required string PathBase { get; set; }
    public List<string> DbmsSchema { get; set; } = [];
    public List<string> DbmsDataMigrations { get; set; } = [];

    public List<CalendareFeatures> Features { get; set; } = [];
    public List<FeatureByClient> FeaturesEnabled { get; set; } = [];
}
