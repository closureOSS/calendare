using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

namespace Calendare.Server.Utils;

public static class HttpRequestExtensions
{

    public static string GetFullPath(this HttpRequest request)
    {
        var pathComponents = GetAllPathComponents(request);
        return $"/{string.Join('/', pathComponents)}/";
    }

    private static string[] GetAllPathComponents(this HttpRequest request)
    {
        var requestedPath = new Uri(request.GetEncodedUrl()).LocalPath;

        var pathComponents = requestedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // TODO: Make remove base URL configurable
        pathComponents = [.. pathComponents[1..]];

        return pathComponents;
    }


    public static async Task<string> BodyAsStringAsync(this HttpRequest request, CancellationToken ct)
    {
        using (var sr = new StreamReader(request.Body))
        {
            return await sr.ReadToEndAsync(ct);
        }
    }

    public static int GetDepth(this HttpRequest request, int fallback = int.MaxValue)
    {
        var depthHeader = request.Headers["Depth"].FirstOrDefault();
        if (depthHeader is null)
        {
            return fallback;
        }
        if (string.Equals(depthHeader, "infinity", StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue;
        }
        if (!int.TryParse(depthHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth))
            return fallback;
        return depth;
    }

    public static string? GetIfMatch(this HttpRequest request)
    {
        var ifMatchHeader = request.Headers.IfMatch;
        if (ifMatchHeader == StringValues.Empty)
        {
            return null;
        }
        var etag = ifMatchHeader.FirstOrDefault();
        // TODO: Check Etag proper formatting and variants
        if (etag?.StartsWith('"') == true && etag?.EndsWith('"') == true)
        {
            etag = etag[1..^1];
        }
        return etag;
    }

    public static bool GetIfNoneMatch(this HttpRequest request)
    {
        var ifNoneMatchHeader = request?.Headers.IfNoneMatch.FirstOrDefault();
        return ifNoneMatchHeader is not null && string.Equals(ifNoneMatchHeader, "*", StringComparison.Ordinal);
    }

    public static string? GetIfScheduleTagMatch(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue("If-Schedule-Tag-Match", out var ifMatchHeader))
        {
            return null;
        }
        var etag = ifMatchHeader.FirstOrDefault();
        // // TODO: Check Etag proper formatting and variants
        if (etag?.StartsWith('"') == true && etag?.EndsWith('"') == true)
        {
            etag = etag[1..^1];
        }
        return etag;
    }

    /// <summary>
    /// Schedule-Reply Request Header https://datatracker.ietf.org/doc/html/rfc6638#section-8.1
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static bool GetDoScheduleReply(this HttpRequest request)
    {
        if (request.Headers.TryGetValue("Schedule-Reply", out var sr))
        {
            var sr0 = sr.FirstOrDefault();
            return sr0 is null || sr0.Equals("T", StringComparison.InvariantCultureIgnoreCase);
        }
        return true;    // default
    }
}
