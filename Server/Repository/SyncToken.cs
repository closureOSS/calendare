using System;
using System.Text.RegularExpressions;
using Calendare.Server.Utils;
using NodaTime;

namespace Calendare.Server.Repository;

public partial class SyncToken
{
    public Guid Id { get; set; }

    public int CollectionId { get; set; }

    public Instant Created { get; set; }

    public string Uri => $"http://calendare.org/ns/sync/{(Id != Guid.Empty ? Id.ToBase64Url() : "0")}";

    public static Guid? ParseUri(string tokenUri)
    {
        Match m = SyncTokenRegex().Match(tokenUri);
        if (m.Success)
        {
            var token = m.Groups[1].Value;
            if (string.Equals(token, "0", StringComparison.Ordinal))
            {
                return Guid.Empty;
            }
            if (GuidUtil.TryGuidFromBase64Url(token, out Guid guid))
            {
                return guid;
            }
        }
        return null;
    }

#pragma warning disable MA0023 // Add RegexOptions.ExplicitCapture
    [GeneratedRegex(@"http://calendare.org/ns/sync/([A-Za-z0-9_\-=]+)", RegexOptions.None, matchTimeoutMilliseconds: 200)]
#pragma warning restore MA0023 // Add RegexOptions.ExplicitCapture
    private static partial Regex SyncTokenRegex();


    public static SyncToken Sentinel => new()
    {
        Id = Guid.Empty,
        Created = SystemClock.Instance.GetCurrentInstant(),
    };
}

