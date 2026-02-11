using System.Text.Json.Serialization;

namespace Calendare.Server.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<CalendareFeatures>))]
public enum CalendareFeatures
{
    /// <summary>
    /// Calendar proxy (Apple CalendarServer extension)
    ///
    /// https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
    /// </summary>
    CalendarProxy,

    /// <summary>
    /// Adds members to the proxy group read or read/write based on granted privileges.
    /// Privileges must contain at least read or read/write to be considered as member.
    ///
    /// Allows calendar clients (such as MacOS) to show the calendar in the calendar list.
    /// </summary>
    VirtualProxyMembers,

    /// <summary>
    /// Resource sharing (Apple extension)
    ///
    /// https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-4.1
    /// </summary>
    ResourceSharing,

    /// <summary>
    /// Server scheduling
    ///
    /// https://datatracker.ietf.org/doc/html/rfc6638#section-2
    /// </summary>
    AutoScheduling,

    /// <summary>
    /// (Under development/experimental) WebDAV Push
    ///
    /// Specification https://github.com/bitfireAT/webdav-push/blob/main/content.mkd and see also
    /// Nextcloud extension for WebDAV-Push https://github.com/bitfireAT/nc_ext_dav_push
    /// </summary>
    WebdavPush,

    /// <summary>
    /// Allow vCard 4 formatted addresses
    ///
    /// Warning: As of March 2025 MacOS Contacts doesn't support vCard 4 and simply doesn't show these addresses at all.
    /// </summary>
    VCard4,

    /// <summary>
    /// Ignore invalid sync tokens and return all changes (similar to empty token)
    ///
    /// Doesn't send a GONE status (as required by the RFC)
    /// </summary>
    SyncCollectionSuppressTokenGone,
}
