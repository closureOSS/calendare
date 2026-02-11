using System.Text.Json.Serialization;

namespace Calendare.Server.Constants;

[JsonConverter(typeof(JsonStringEnumConverter<RelationshipTypes>))]
public enum RelationshipTypes
{
    None,
    Custom,
    Administers,
    ReadWrite,
    Read,
    Freebusy
}
