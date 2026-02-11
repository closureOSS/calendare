using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;

namespace Calendare.Server.Api;

public static partial class PrivilegeResponseMapper
{
    public static PrivilegeResponse ToResponse(this IEnumerable<AccessControlEntity> source, StaticDataRepository staticDataRepository, bool reverse = false, bool includeEmpty = false)
    {
        var grantTypes = staticDataRepository.RelationshipTypeList.Values;
        var result = new PrivilegeResponse
        {
            GrantedTo = !reverse
        };
        if (result.GrantedTo)
        {
            result.HasProxyReadCollection = result.HasProxyWriteCollection = false;
        }
        if (includeEmpty)
        {
            foreach (var gt in grantTypes)
            {
                if (gt.Id != (int)RelationshipTypes.Custom)
                {
                    result.PrivilegeGroups.Add(new PrivilegeGroupResponse { Name = gt.Name, Code = gt.Confers, Privileges = [], });
                }
            }
        }
        foreach (var ace in source)
        {
            var uri = ace.Grantee?.Uri ?? ace.Grantor?.Uri;
            if (!reverse && uri?.Contains(CollectionUris.CalendarProxyWrite, System.StringComparison.Ordinal) == true)
            {
                result.HasProxyWriteCollection = true;
            }
            else if (!reverse && uri?.Contains(CollectionUris.CalendarProxyRead, System.StringComparison.Ordinal) == true)
            {
                result.HasProxyReadCollection = true;
            }
            else
            {
                var grantLabel = ace.GrantType?.Name ?? "Custom";
                var plr = new PrivilegeLineResponse
                {
                    Uri = uri,
                    Username = ace.Grantee?.Username ?? ace.Grantor?.Owner?.Username,
                    Displayname = ace.Grantee?.DisplayName ?? ace.Grantor?.DisplayName,
                };
                if (ace.IsInherited)
                {
                    plr.IsInherited = true;
                }
                if (ace.IsIndirect)
                {
                    plr.IsIndirect = true;
                }
                var principalTypeId = ace.Grantor?.PrincipalTypeId ?? ace.Grantee?.PrincipalTypeId;
                if (principalTypeId is not null)
                {
                    if (staticDataRepository.PrincipalTypeList.TryGetValue((PrincipalTypes)(principalTypeId - 1), out var principalType))
                    {
                        plr.PrincipalType = principalType.Label;
                    }
                }
                if (string.Equals(grantLabel, "Custom", System.StringComparison.OrdinalIgnoreCase))
                {
                    plr.Grants = GetPrivilegeItemResponses(ace.Grants);
                }
                var existing = result.PrivilegeGroups.FirstOrDefault(x => string.Equals(x.Name, grantLabel, System.StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    var gt = grantTypes.FirstOrDefault(c => string.Equals(grantLabel, c.Name, System.StringComparison.InvariantCultureIgnoreCase));
                    existing ??= new PrivilegeGroupResponse { Name = gt?.Name ?? grantLabel, Code = gt?.Confers ?? "C" };
                    result.PrivilegeGroups.Add(existing);
                }
                existing.Privileges ??= [];
                existing.Privileges.Add(plr);
                existing.Privileges.Sort((l, r) => string.Compare(l.Uri, r.Uri, System.StringComparison.InvariantCultureIgnoreCase));
            }
        }
        result.PrivilegeGroups.Sort((l, r) => string.Compare(l.Name, r.Name, System.StringComparison.InvariantCultureIgnoreCase));
        return result;
    }

    private static List<PrivilegeItemResponse> GetPrivilegeItemResponses(ImmutableList<PrivilegeItem> source)
    {
        var result = new List<PrivilegeItemResponse>();
        foreach (var pi in source)
        {
            result.Add(new PrivilegeItemResponse
            {
                Id = $"{pi.Id}",
                Description = pi.Description,
            });
        }
        return result;
    }
}
