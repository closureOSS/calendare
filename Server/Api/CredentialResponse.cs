using Calendare.Data.Models;
using NodaTime;

namespace Calendare.Server.Api;

public class CredentialResponse
{
    public string Subject { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public Instant? EmailOk { get; set; }
    public UsrCredentialType? CredentialType { get; set; }
    public string? Description { get; set; }
    public string? Issuer { get; set; }

    public Instant? LastUsed { get; set; }
    public Instant? Locked { get; set; }
    public Instant? ValidFrom { get; set; }
    public Instant? ValidTo { get; set; }

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}

public class CredentialCreateResponse : CredentialResponse
{
    public string? Secret { get; set; }
}
