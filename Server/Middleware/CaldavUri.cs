
using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Server.Api;
using Calendare.Server.Utils;

namespace Calendare.Server.Middleware;

public class CaldavUri
{
    public CaldavUri(string path, string? pathPrefix = null)
    {
        var (segments, hasSlashEnding) = UriUtils.ToSegments(path, pathPrefix);
        if (segments is null) throw new ArgumentNullException(nameof(path));
        var idx = 0;
        var hasNoValidSegments = true;
        var expectedUsername = true;
        foreach (var segment in segments)
        {
            var isLast = idx++ == segments.Length - 1;
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }
            var decoded = Uri.UnescapeDataString(segment);
            if (expectedUsername)
            {
                expectedUsername = false;
                if (!UserExtensions.IsValidUsername(decoded))
                {
                    break;
                }
                Username = decoded;
                continue;
            }
            if (!isLast)
            {
                Components.Add(decoded);
                continue;
            }
            TrailingSegment = decoded;
            if (hasSlashEnding || Components.Count == 0)
            {
                Components.Add(decoded);
                IsDirectory = true;
            }
            else
            {
                ItemName = decoded;
            }
            hasNoValidSegments = false;
        }
        IsRoot = segments.Length == 0;
        IsResource = !hasNoValidSegments && !string.IsNullOrEmpty(Username);
        IsPrincipal = !string.IsNullOrEmpty(Username) && Components.Count == 0;
        IsInvalid = !(IsRoot || IsResource || IsPrincipal);
        if (!IsInvalid)
        {
            IsSubResource = Components.Count > 1;
        }
    }

    private readonly List<string> Components = [];

    public string? Username { get; }
    public string? ItemName { get; }
    public string? TrailingSegment { get; }

    /// <summary>
    /// URI is malformed and does not point to any possible resource
    /// </summary>
    public bool IsInvalid { get; }

    /// <summary>
    /// Uri refers to a resource (collection or object) with a principal.
    /// </summary>
    public bool IsResource { get; }

    /// <summary>
    /// URI indicates a folder/directory (ends in a / or no path component). IsDirectory implies IsResource.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// URI refers to the root directory (principal not defined)
    /// </summary>
    public bool IsRoot { get; }

    /// <summary>
    /// URI refers to a principal
    /// </summary>
    public bool IsPrincipal { get; }

    public bool IsSubResource { get; }

    public string? Path
    {
        get
        {
            if (IsInvalid || IsRoot)
            {
                return null;
            }
            if (string.IsNullOrEmpty(ItemName))
            {
                return UriUtils.ToFolderPath(UriUtils.ToPath([Username!, .. Components.Select(UriUtils.EncodeSlash)!]));
            }
            return UriUtils.ToPath([Username!, .. Components.Select(UriUtils.EncodeSlash)!, UriUtils.EncodeSlash(ItemName)]);
        }
    }

    public string? ParentCollectionPath
    {
        get
        {
            if (IsInvalid)
            {
                return null;
            }
            var components = Components;
            if (string.IsNullOrEmpty(ItemName))
            {
                switch (Components.Count)
                {
                    case 0: return UriUtils.ToFolderPath([]);
                    case 1: return UriUtils.ToFolderPath([Username!]);
                }
                components = Components[..^1];
            }
            return UriUtils.ToFolderPath([Username!, .. components.Select(UriUtils.EncodeSlash)!]);
        }
    }
}
