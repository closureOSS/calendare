using System.Collections.Generic;

namespace Calendare.Server.Api.Models;

public class GroupRef
{
    public required GroupMemberRef Group { get; set; }
    public List<GroupMemberRef> Members { get; set; } = [];
}

public class MembershipResponse
{
    public List<GroupMemberRef>? Memberships { get; set; }
    public List<GroupRef>? Groups { get; set; }
}

public class MembershipMemberRequest
{
    public required string Uri { get; set; }
    public MembershipPrivilegeType MembershipType { get; set; }
}

public class MembershipGroupRequest
{
    public required string Uri { get; set; }
    public List<MembershipMemberRequest> Members { get; set; } = [];
}

public class MembershipRequest
{
    public required List<MembershipGroupRequest> Groups { get; set; }
}
