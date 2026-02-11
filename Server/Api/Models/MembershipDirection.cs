using System.Text.Json.Serialization;

namespace Calendare.Server.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MembershipDirection>))]
public enum MembershipDirection
{
    Members,
    Memberships,
    Both,
}
