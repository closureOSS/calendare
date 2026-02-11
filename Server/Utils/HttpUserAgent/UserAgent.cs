using Calendare.Server.Api.Models;

namespace Calendare.Server.Utils.HttpUserAgent;

public readonly struct UserAgent
{
    public string Raw { get; init; }
    public Platform Platform { get; init; }
    public CalendarClientType CalendarClientType { get; init; }
}
