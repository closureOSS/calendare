//urn: ietf:params:xml: ns: caldav

using System.Xml.Linq;

namespace Calendare.Server.Constants;

/// <summary><see cref="https://datatracker.ietf.org/doc/html/rfc4918#section-16"/>Preconditions WebDav</see></summary>
public static class Precondition
{
    public static readonly XName SupportedCalendarData = XmlNs.Caldav + "supported-calendar-data";
    public static readonly XName ValidCalendarData = XmlNs.Caldav + "valid-calendar-data";
    public static readonly XName ValidCalendarObjectResource = XmlNs.Caldav + "valid-calendar-object-resource";
    public static readonly XName SupportedAddressData = XmlNs.Carddav + "supported-address-data";
    public static readonly XName ValidAddressbookData = XmlNs.Carddav + "valid-addressbook-data";
    public static readonly XName CollectionMustExist = XmlNs.Dav + "collection-must-exist";
    public static readonly XName InvalidXml = XmlNs.Dav + "invalid-xml";
    public static readonly XName Duplicate = XmlNs.Dav + "duplicate";
    public static readonly XName IfMatch = XmlNs.Dav + "if-match";
    public static readonly XName IfNoneMatch = XmlNs.Dav + "if-none-match";
    public static readonly XName MustExist = XmlNs.Dav + "must-exist";

    /// <summary>This server does not allow infinite-depth PROPFIND requests on collections.</summary>
    public static readonly XName PropfindFiniteDepth = XmlNs.Dav + "propfind-finite-depth";

    /// <summary>The client attempted to set a protected property in a PROPPATCH (such as DAV:getetag).</summary>
    public static readonly XName CannotModifyProtectedProperty = XmlNs.Dav + "cannot-modify-protected-property";

    public static readonly XName NoExternalEntities = XmlNs.Dav + "no-external-entities";
    public static readonly XName ContentEncoding = XmlNs.Dav + "content-encoding";
    public static readonly XName SupportedReport = XmlNs.Dav + "supported-report";
    public static readonly XName SubscriptionId = XmlNs.Bitfire + "subscription-id";

}
