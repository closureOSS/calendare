using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class UserResponseMapper
{
    [MapperIgnoreTarget(nameof(PrincipalResponse.IsRoot))]
    [MapperIgnoreTarget(nameof(PrincipalResponse.IsEmailVerified))]
    [MapperIgnoreTarget(nameof(PrincipalResponse.HasGroups))]
    [MapperIgnoreTarget(nameof(PrincipalResponse.HasScheduling))]
    [MapperIgnoreTarget(nameof(PrincipalResponse.Permissions))]
    private static partial PrincipalResponse Map(Principal source);

    public static PrincipalResponse ToView(this Principal source, int? currentUserId = null, bool? hasGroups = null, bool? hasScheduling = null)
    {
        var target = Map(source);
        target.IsRoot = source.UserId == StockPrincipal.Admin;
        target.IsEmailVerified = source.EmailOk is not null;
        target.HasGroups = hasGroups;
        target.HasScheduling = hasScheduling;
        var permissions = source.GlobalPermit;
        if (currentUserId == StockPrincipal.Admin)
        {
            permissions = PrivilegeMask.All;
        }
        else if (currentUserId == source.UserId)
        {
            permissions = PrivilegeMask.All & source.OwnerMask;
        }
        else if (source.Permissions is not null)
        {
            var direct = source.Permissions.FirstOrDefault();
            if (direct is not null)
            {
                permissions |= direct.Privileges;
                permissions &= source.AuthorizedMask;
            }
        }
        target.Permissions = permissions;
        return target;
    }

    public static List<PrincipalResponse> ToView(this IEnumerable<Principal> source, int? currentUserId = null, bool? hasGroups = null, bool? hasScheduling = null)
    {
        return [.. source.Select(x => x.ToView(currentUserId, hasGroups, hasScheduling))];
    }
}
