using System.Collections.Generic;
using System.Text.Json.Serialization;
using Calendare.Server.Constants;

namespace Calendare.Server.Api;

public class UserRegisterRequest : UserAmendRequest
{
    /// <summary>
    /// Unique technical username (part of the principal URI)
    /// Must be a valid path component
    /// </summary>
    public required string Username { get; set; }
    public string? Password { get; set; }
    public required string Type { get; set; } = PrincipalTypeCode.Individual;
    public List<UserRegisterCollections> SkipCollections { get; set; } = [];
}

[JsonSerializable(typeof(UserRegisterRequest))]
[JsonSerializable(typeof(CollectionCreateRequest))]
public partial class CalendareSerializerContext : JsonSerializerContext { }
