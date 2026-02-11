//urn: ietf:params:xml: ns: caldav

using System.Xml.Linq;

namespace Calendare.Server.Constants;

public static class XmlNs
{
    public static readonly XNamespace Caldav = "urn:ietf:params:xml:ns:caldav";
    public const string CaldavPrefix = "C";

    public static readonly XNamespace Carddav = "urn:ietf:params:xml:ns:carddav";
    public const string CarddavPrefix = "V";

    public static readonly XNamespace CalenderServer = "http://calendarserver.org/ns/";
    public const string CalenderServerPrefix = "CS";

    public static readonly XNamespace AppleIcal = "http://apple.com/ns/ical/";
    public const string AppleIcalPrefix = "A";

    public static readonly XNamespace ISchedule = "urn:ietf:params:xml:ns:ischedule";
    public const string ISchedulePrefix = "I";

    public static readonly XNamespace Bitfire = "https://bitfire.at/webdav-push";
    public const string BitfirePrefix = "P";

    public static readonly XNamespace IceWarp = "http://icewarp.com/ns/";
    public const string IceWarpPrefix = "IC";


    /// <summary>
    /// Main DAV namespace (<c>DAV:</c>).
    /// </summary>
    public static readonly XNamespace Dav = "DAV:";

    /// <summary>
    /// Main DAV namespace prefix (<c>D</c>).
    /// Some WebDAV clients don't parse the server generated XML properly
    /// and expect that all DAV nodes use the "D" prefix. Although it is
    /// perfectly legal to use a different namespace prefix, we do use it
    /// to maximize compatibility.
    /// </summary>
    public const string DavPrefix = "D";
}
