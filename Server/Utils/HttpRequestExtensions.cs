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
    extension(HttpRequest request)
    {
        public string GetFullPath(string? pathPrefix)
        {
            var requestedPath = new Uri(request.GetEncodedUrl()).LocalPath;
            var (segments, hasSlashEnding) = UriUtils.ToSegments(requestedPath, pathPrefix);
            return hasSlashEnding ? UriUtils.ToFolderPath(segments) : UriUtils.ToPath(segments);
        }

        public async Task<string> BodyAsStringAsync(CancellationToken ct)
        {
            using (var sr = new StreamReader(request.Body))
            {
                return await sr.ReadToEndAsync(ct);
            }
        }

        public int GetDepth(int fallback = int.MaxValue)
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

        public string? GetIfMatch()
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

        public bool GetIfNoneMatch()
        {
            var ifNoneMatchHeader = request?.Headers.IfNoneMatch.FirstOrDefault();
            return ifNoneMatchHeader is not null && string.Equals(ifNoneMatchHeader, "*", StringComparison.Ordinal);
        }

        public string? GetIfScheduleTagMatch()
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
        /// <returns></returns>
        public bool GetDoScheduleReply()
        {
            if (request.Headers.TryGetValue("Schedule-Reply", out var sr))
            {
                var sr0 = sr.FirstOrDefault();
                return sr0 is null || sr0.Equals("T", StringComparison.InvariantCultureIgnoreCase);
            }
            return true;    // default
        }

        /// <summary>
        /// Overwrite Request Header https://datatracker.ietf.org/doc/html/rfc4918#section-10.6
        /// </summary>
        public bool GetOverwrite()
        {
            var isOverwrite = true;
            if (!request.Headers.TryGetValue("Overwrite", out var isOverwriteFlag))
            {
                return isOverwrite; // default: true
            }
            // we treat any other value to 'F' as true
            if (string.Equals(isOverwriteFlag, "F", StringComparison.OrdinalIgnoreCase))
            {
                isOverwrite = false;
            }
            return isOverwrite;
        }

        public int GetTimeout(int fallback = 60)
        {
            // TODO: Implement get timeout header
            // Timeout: Infinite,Second-4100000000
            var timeoutHeader = request.Headers["Timeout"].FirstOrDefault();
            if (timeoutHeader is null)
            {
                return fallback;
            }
            int? optimal = null;
            int minimal = fallback;
            if (timeoutHeader.Contains("Infinite", StringComparison.OrdinalIgnoreCase))
            {
                optimal = int.MaxValue;
            }
            // ("Second-" DAVTimeOutVal | "Infinite")
            // if (string.Equals(timeoutHeader, "Infinite", StringComparison.OrdinalIgnoreCase))
            // {
            //     return int.MaxValue;
            // }
            // if (!int.TryParse(timeoutHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth))
            //     return fallback;
            return optimal is null ? minimal : optimal.Value;
        }
    }
}
