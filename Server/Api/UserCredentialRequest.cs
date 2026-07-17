using System.Text.Json.Serialization;
using Calendare.Server.Constants;
using NodaTime;

namespace Calendare.Server.Api;

public class UserCredentialLoginRequest
{
    public string CredentialType { get; set; } = CredentialTypes.PasswordCode;
    public string? Template { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Description { get; set; }
}


[JsonConverter(typeof(JsonStringEnumConverter<UserCredentialCreateTemplate>))]
public enum UserCredentialCreateTemplate
{
    /// <summary>
    /// Application key type (like API key)
    /// </summary>
    ApplicationKey,

    /// <summary>
    /// Password type with user's e-mail as accesskey
    /// </summary>
    Email,

    /// <summary>
    /// Password type with user's username as accesskey
    /// </summary>
    Username,

    /// <summary>
    /// Json Web Token from a OIDC provider (not supported for direct API creation)
    /// </summary>
    JwtBearer,

    /// <summary>
    /// Password type with generic username/password (legacy, use application keys)
    /// </summary>
    Generic,
}

public class UserCredentialCreateRequest
{
    public required UserCredentialCreateTemplate Template { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Issuer { get; set; }
    public string? Description { get; set; }
    public Instant? ValidFrom { get; set; }
    public Instant? ValidTo { get; set; }
}

public class UserCredentialResetRequest
{
    public string? Password { get; set; }
}
