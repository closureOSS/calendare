using System.Text.Json.Serialization;

namespace Calendare.Server.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MembershipPrivilegeType>))]
public enum MembershipPrivilegeType
{
    Unassigned,
    Standard,
    ProxyRead,
    ProxyWrite,
}
