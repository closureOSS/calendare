using System.Globalization;
using NodaTime;

namespace Calendare.Server.Utils;

public static class NodaTimeExtensions
{
    // see https://datatracker.ietf.org/doc/html/rfc2616#section-3.3.1
    public static string ToRfc2616(this Instant instant)
    {
        var utcTime = instant.InUtc();
        return utcTime.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
    }

    // see https://datatracker.ietf.org/doc/html/rfc3339#section-5.6
    public static string ToRfc3339(this Instant instant)
    {
        return instant.ToString("g", CultureInfo.InvariantCulture);
        // return instant.ToString("yyyyMMddTHHmmss'Z'", CultureInfo.InvariantCulture);
    }
}
