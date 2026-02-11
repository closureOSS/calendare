
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Calendare.Server.Middleware;

public class CaldavUri
{
    public CaldavUri(string path, string? pathPrefix = null)
    {
        var prefixSegments = pathPrefix?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hasPrefix = prefixSegments is not null && prefixSegments.Length > 0;
        var hasSlashEnding = path.EndsWith('/');
        var Segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var idx = 0;
        var expectedUsername = true;
        foreach (var segment in Segments)
        {
            var part = segment.EndsWith('/') ? segment[..^1] : segment;
            var isLast = idx == Segments.Length - 1;
            // TODO: prefix check is good weather safe ... should either match fully or not at all
            if (hasPrefix && prefixSegments?.Length > idx)
            {
                if (string.Equals(part, prefixSegments[idx], StringComparison.Ordinal))
                {
                    ++idx;
                    continue;
                }
                hasPrefix = false;
            }
            ++idx;
            if (!string.IsNullOrEmpty(part))
            {
                var decoded = HttpUtility.UrlDecode(part);
                if (expectedUsername)
                {
                    Username = decoded;
                    expectedUsername = false;
                }
                else
                {
                    if (!isLast)
                    {
                        Components.Add(decoded);
                    }
                    else
                    {
                        if (hasSlashEnding || Components.Count == 0)
                        {
                            Components.Add(decoded);
                            IsDirectory = true;
                        }
                        else
                        {
                            ItemName = decoded;
                        }
                    }
                }
            }
        }
    }

    private readonly List<string> Components = [];
    public bool IsDirectory { get; }

    public string? Username { get; init; }
    public string? ItemName { get; init; }
    public bool IsValid() => !string.IsNullOrEmpty(Username);

    public bool IsPrincipal() => IsValid() && Components.Count == 0;

    public List<string>? Collection
    {
        get
        {
            if (Components.Count <= 0)
            {
                return null;
            }
            return Components.GetRange(0, Components.Count - (IsDirectory ? 0 : 1));
        }
    }

    public string? Path
    {
        get
        {
            if (!IsValid())
            {
                return null;
            }
            if (string.IsNullOrEmpty(ItemName))
            {
                if (Components.Count == 0)
                {
                    return $"/{Username}/";
                }
                return $"/{Username}/{string.Join('/', Components.Select(EncodeSlash))}/";
            }
            return $"/{Username}/{string.Join('/', Components.Select(EncodeSlash))}/{EncodeSlash(ItemName)}";
        }
    }

    public string? ParentCollectionPath
    {
        get
        {
            if (!IsValid())
            {
                return null;
            }
            if (string.IsNullOrEmpty(ItemName))
            {
                if (Components.Count == 0)
                {
                    return "/";
                }
                var components = Components[..^1];
                if (components.Count == 0)
                {
                    return $"/{Username}/";
                }
                return $"/{Username}/{string.Join('/', components.Select(EncodeSlash))}/";
            }
            else
            {
                return $"/{Username}/{string.Join('/', Components.Select(EncodeSlash))}/";
            }
        }
    }

    private static string? EncodeSlash(string? uri)
    {
        if (uri is null) return null;
        return uri.Contains('/') ? uri.Replace("/", "%2F") : uri;
    }
}
