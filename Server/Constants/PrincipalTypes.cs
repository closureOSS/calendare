using System.Text.Json.Serialization;

namespace Calendare.Server.Constants;

[JsonConverter(typeof(JsonStringEnumConverter<PrincipalTypes>))]
public enum PrincipalTypes
{
    Individual,
    Resource,
    Group,
    Room,
}
