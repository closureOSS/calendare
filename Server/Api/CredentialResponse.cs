using Calendare.Data.Models;
using NodaTime;

namespace Calendare.Server.Api;

public class CredentialResponse
{
    public int Id { get; set; }
    public string Subject { get; set; } = default!;
    public int UsrId { get; set; }
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public Instant? EmailOk { get; set; }
    public UsrCredentialType? CredentialType { get; set; }
    public int CredentialTypeId { get; set; }
    public Instant? LastUsed { get; set; }
    public Instant? Locked { get; set; }

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}
