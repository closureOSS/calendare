using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Calendare.Server.Utils;

public static class UriUtils
{
    public const char Delimiter = '/';

    public static bool IsFolder(string? uri)
    {
        return !string.IsNullOrEmpty(uri) && uri.EndsWith(Delimiter);
    }

    public static string ToFolderPath(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return $"{Delimiter}";
        if (!IsFolder(uri))
        {
            return uri + Delimiter;
        }
        return uri;
    }

    public static string ToFolderPath(string[]? segments)
    {
        return ToFolderPath(ToPath(segments));
    }

    public static string ToPath(string[]? segments)
    {
        if (segments is null)
        {
            return $"{Delimiter}";
        }
        return $"/{string.Join(Delimiter, segments)}";
    }

    public static string ToEscapedUri(params string?[] segments)
    {
        if (segments is null || segments.Length == 0)
        {
            return $"{Delimiter}";
        }
        var parts = segments
            .SelectMany(part => (part ?? "").Split(Delimiter, StringSplitOptions.RemoveEmptyEntries))
            .Select(part => Uri.EscapeDataString(DecodeSlash(part)));
        var isDirectory = (segments[^1] ?? "").EndsWith(Delimiter);
        return isDirectory ? ToFolderPath([.. parts]) : ToPath([.. parts]);
    }

    public static string ToEscapedFolderUri(params string?[] segments)
    {
        return ToFolderPath(ToEscapedUri(segments));
    }

    [return: NotNullIfNotNull(nameof(uri))]
    public static string? EncodeSlash(string? uri)
    {
        if (uri is null) return null;
        return uri.Contains('/', StringComparison.Ordinal) ? uri.Replace("/", "%2F", StringComparison.Ordinal) : uri;
    }

    [return: NotNullIfNotNull(nameof(uri))]
    public static string? DecodeSlash(string? uri)
    {
        if (uri is null) return null;
        return uri.Contains("%2F", StringComparison.Ordinal) ? uri.Replace("%2F", "/", StringComparison.Ordinal) : uri;
    }

    [return: NotNullIfNotNull(nameof(uri))]
    public static string[]? ToSegments(string? uri)
    {
        return uri?.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
    }

    public static (string[]? segments, bool hasSlashEnding) ToSegments(string? path, string? pathPrefix)
    {
        if (path is null)
        {
            return (null, false);
        }
        var prefixSegments = ToSegments(pathPrefix);
        var hasSlashEnding = IsFolder(path);
        var segments = ToSegments(path);
        if (prefixSegments is not null && prefixSegments.Length > 0)
        {
            if (prefixSegments!.Length <= segments.Length)
            {
                var startsWithPrefix = segments.Take(prefixSegments.Length).SequenceEqual(prefixSegments, StringComparer.InvariantCulture);
                if (startsWithPrefix)
                {
                    segments = [.. segments.Skip(prefixSegments.Length)];
                }
            }
        }
        return (segments, hasSlashEnding);
    }

    public static string RemovePathBase(string path, string? pathPrefix)
    {
        var isWellFormed = Uri.IsWellFormedUriString(path, UriKind.RelativeOrAbsolute);
        if (!isWellFormed)
        {
            throw new ArgumentException("Invalid URI", nameof(path));
        }
        if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri))
        {
            throw new ArgumentException("Invalid URI", nameof(path));
        }
        var absolutePath = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        if (pathPrefix is null)
        {
            return absolutePath;
        }
        var (segments, hasSlashEnding) = ToSegments(absolutePath, pathPrefix);
        return hasSlashEnding ? ToFolderPath(segments) : ToPath(segments);
    }
}
