namespace Calendare.Server.Api.Models;

public class GroupMemberRef
{
    public required string Uri { get; set; }
    public string? Username { get; set; }
    public string? Displayname { get; set; }
    public string? OwnerDisplayname { get; set; }
    public string? PrincipalType { get; set; }
    public MembershipPrivilegeType MembershipType { get; set; } = MembershipPrivilegeType.Standard;
    public bool? IsVirtual { get; set; }
}
