using System.Globalization;
using NodaTime;

namespace Calendare.Server.Utils;

public static class NodaTimeExtensions
{
    extension(Instant instant)
    {
        // see https://datatracker.ietf.org/doc/html/rfc2616#section-3.3.1
        public string ToRfc2616()
        {
            var utcTime = instant.InUtc();
            return utcTime.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        // see https://datatracker.ietf.org/doc/html/rfc3339#section-5.6
        public string ToRfc3339()
        {
            return instant.ToString("g", CultureInfo.InvariantCulture);
            // return instant.ToString("yyyyMMddTHHmmss'Z'", CultureInfo.InvariantCulture);
        }
    }
}
