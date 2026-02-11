using System.Text.Json.Serialization;

namespace Calendare.Server.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<CalendarClientType>))]
public enum CalendarClientType
{
    NotDetected,
    Thunderbird,
    MacOSCalendar,
    EMClient,
    DAVx5,

    /// <summary>
    /// Any clients (just for filtering, not used in detection)
    /// </summary>
    Default,
}
