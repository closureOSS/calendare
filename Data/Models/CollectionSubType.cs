using System.Text.Json.Serialization;

namespace Calendare.Data.Models;


[JsonConverter(typeof(JsonStringEnumConverter<CollectionSubType>))]
public enum CollectionSubType
{
    Default,
    SchedulingOutbox,   // https://datatracker.ietf.org/doc/html/rfc6638#section-2.1 <C:schedule-outbox xmlns:C="urn:ietf:params:xml:ns:caldav"/>
    SchedulingInbox,    // https://datatracker.ietf.org/doc/html/rfc6638#section-2.2 <C:schedule-inbox xmlns:C="urn:ietf:params:xml:ns:caldav"/>
    CalendarProxyRead,
    CalendarProxyWrite,
    WebPushSubscription,
}
