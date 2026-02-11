using System.Text.Json.Serialization;

namespace Calendare.Data.Models;


[JsonConverter(typeof(JsonStringEnumConverter<CollectionType>))]
public enum CollectionType
{
    Collection,
    Principal,
    Calendar,
    Addressbook,
}
