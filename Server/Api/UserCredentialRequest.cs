using Calendare.Server.Constants;

namespace Calendare.Server.Api;

public class UserCredentialRequest
{
    public string CredentialType { get; set; } = CredentialTypes.PasswordCode;
    public string? Template { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
