using System;
using System.Globalization;

namespace Calendare.Server.Utils;

public sealed class HttpDateParser
{
    private static readonly string[] Rfc2616Formats =
    [
        "ddd, d MMM yyyy HH:mm:ss 'GMT'", // RFC 822 / RFC 1123 (Preferred)
        "dddd, d-MMM-yy HH:mm:ss 'GMT'", // RFC 850 / RFC 1036
        "ddd MMM  d HH:mm:ss yyyy",        // ANSI C's asctime() format
    ];

    public static bool TryParseRfc2616(string input, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParseExact(
            input,
            Rfc2616Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out result
        );
    }
}
