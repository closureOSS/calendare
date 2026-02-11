using Calendare.Data.Models;

namespace Calendare.Server.Repository;

public class MembershipQueryResult
{
    public int GroupId { get; set; }
    public required string GroupUri { get; set; }
    public int MemberId { get; set; }
    public required string MemberUri { get; set; }
    public string? Username { get; set; }
    public string? Displayname { get; set; }
    public string? PrincipalType { get; set; }
    public CollectionSubType CollectionSubType { get; set; }

    /// <summary>
    /// True: We are member in a group
    /// False: Member in of one of our groups
    /// </summary>
    public bool IsMember { get; set; }
}


class CollectionRefId
{
    public required int Id { get; set; }
    public required string Uri { get; set; }
}
