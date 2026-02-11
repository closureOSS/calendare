using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Utils;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static bool IsProxyRead(this DavResource resource) => IsProxyRead(resource.DavName);
    public static bool IsProxyWrite(this DavResource resource) => IsProxyWrite(resource.DavName);

    public static bool IsProxyRead(this Collection collection) => collection.CollectionSubType == CollectionSubType.CalendarProxyRead;
    public static bool IsProxyWrite(this Collection collection) => collection.CollectionSubType == CollectionSubType.CalendarProxyWrite;

    private static bool IsProxyRead(string uri) => uri.EndsWith($"/{CollectionUris.CalendarProxyRead}/", System.StringComparison.Ordinal);
    private static bool IsProxyWrite(string uri) => uri.EndsWith($"/{CollectionUris.CalendarProxyWrite}/", System.StringComparison.Ordinal);


    public static bool IsMainPrincipal(this Collection collection) => collection.CollectionType == CollectionType.Principal && string.Equals(collection.ParentContainerUri, "/", System.StringComparison.Ordinal);

    public static string Topic(this Collection collection) => $"{collection.OwnerId}§{collection.CollectionType}-{collection.Id}".UrlEncodedMD5Hash();
    public static string Topic(this Principal principal) => $"{principal.UserId}§".UrlEncodedMD5Hash();
}
