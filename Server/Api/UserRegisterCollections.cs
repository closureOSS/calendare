using System.Text.Json.Serialization;

namespace Calendare.Server.Api;

[JsonConverter(typeof(JsonStringEnumConverter<UserRegisterCollections>))]
public enum UserRegisterCollections
{
    Default,
    Calendar,
    Address,
    Proxy,
    Scheduling,
    WebPush,
}
