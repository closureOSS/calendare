using System.Collections.Generic;
using Calendare.Server.Api.Models;

namespace Calendare.Server.Options;

public class ClientFeatureSet
{
    public CalendarClientType ClientType { get; set; } = CalendarClientType.Default;
    public List<CalendareFeatures> Enable { get; set; } = [];
    public List<CalendareFeatures> Disable { get; set; } = [];
}
