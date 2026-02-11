using System.Collections.Generic;
using System.Linq;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Repository;

namespace Calendare.Server.Api;

public static class MembershipMapper
{
    public static List<GroupMemberRef> ToMemberlistView(this IEnumerable<MembershipQueryResult> source)
    {
        var result = new List<GroupMemberRef>();
        foreach (var msr in source)
        {
            result.Add(msr.ToMember());
        }
        return result;
    }

    public static MembershipResponse ToMembershipView(this IEnumerable<MembershipQueryResult> source)
    {
        var result = new MembershipResponse
        {
        };
        if (source is null || !source.Any())
        {
            return result;
        }
        foreach (var msr in source.Where(m => m.IsMember))
        {
            result.Memberships ??= [];
            result.Memberships.Add(msr.ToMember());
        }
        foreach (var grp in source.Where(m => m.MemberId == 0))
        {
            result.Groups ??= [];
            result.Groups.Add(new GroupRef { Group = grp.ToGroup(), });
        }
        foreach (var msr in source.Where(m => !m.IsMember && m.MemberId != 0))
        {
            var gmr = msr.ToMembership();
            if (gmr.MembershipType == MembershipPrivilegeType.Unassigned)
            {
                continue;
            }
            var group = result.Groups?.FirstOrDefault(g => g.Group.MembershipType == gmr.MembershipType);
            if (group is null)
            {
                // TODO: Error???
            }
            else
            {
                group.Members ??= [];
                group.Members.Add(gmr);
            }
        }
        return result;
    }

    private static MembershipPrivilegeType GetMembershipPrivilegeType(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return MembershipPrivilegeType.Unassigned;
        }
        if (uri.Contains(CollectionUris.CalendarProxyWrite))
        {
            return MembershipPrivilegeType.ProxyWrite;
        }
        else if (uri.Contains(CollectionUris.CalendarProxyRead))
        {
            return MembershipPrivilegeType.ProxyRead;
        }
        return MembershipPrivilegeType.Standard;
    }

    private static GroupMemberRef ToMember(this MembershipQueryResult msr) => new()
    {
        Uri = msr.MemberUri,
        Displayname = msr.Displayname,
        PrincipalType = msr.PrincipalType,
        Username = msr.Username,
        MembershipType = GetMembershipPrivilegeType(msr.MemberUri),
    };

    private static GroupMemberRef ToMembership(this MembershipQueryResult msr) => new()
    {
        Uri = msr.MemberUri,
        Displayname = msr.Displayname,
        PrincipalType = msr.PrincipalType,
        Username = msr.Username,
        MembershipType = GetMembershipPrivilegeType(msr.GroupUri),
    };

    private static GroupMemberRef ToGroup(this MembershipQueryResult msr) => new()
    {
        Uri = msr.GroupUri,
        Displayname = msr.Displayname,
        PrincipalType = msr.PrincipalType,
        Username = msr.Username,
        MembershipType = GetMembershipPrivilegeType(msr.GroupUri),
    };
}
