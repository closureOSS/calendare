using Calendare.Server.Models;

namespace Calendare.Server.Repository;

public record CredentialRef(Principal Principal, string Subject, string? CredentialType = null)
{
}
