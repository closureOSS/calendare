//urn: ietf:params:xml: ns: caldav

using System.Xml.Linq;

namespace Calendare.Server.Constants;

public static class CollectionNames
{
    public static readonly XName Principal = XmlNs.Dav + "principal";
    public static readonly XName Collection = XmlNs.Dav + "collection";
    public static readonly XName Calendar = XmlNs.Caldav + "calendar";
    public static readonly XName CalendarInbox = XmlNs.Caldav + "schedule-inbox";
    public static readonly XName CalendarOutbox = XmlNs.Caldav + "schedule-outbox";
    public static readonly XName Addressbook = XmlNs.Carddav + "addressbook";
    public static readonly XName ProxyRead = XmlNs.CalenderServer + "calendar-proxy-read";
    public static readonly XName ProxyWrite = XmlNs.CalenderServer + "calendar-proxy-write";
    public static readonly XName Notifications = XmlNs.CalenderServer + "notifications";
}
