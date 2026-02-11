using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class CollectionResponseMapper
{
    [MapperIgnoreSource(nameof(Collection.OwnerId))]
    [MapperIgnoreSource(nameof(Collection.Etag))]
    private static partial CollectionResponse Map(Collection source);

    public static CollectionResponse ToView(this Collection source, PrivilegeScope scope = PrivilegeScope.Unauthenticated, List<GrantRelation>? grants = null)
    {
        var target = Map(source);
        if (string.Equals(target.Uri, $"/{source.Owner.Username}/{CollectionUris.DefaultAddressbook}/", System.StringComparison.Ordinal) ||
            string.Equals(target.Uri, $"/{source.Owner.Username}/{CollectionUris.DefaultCalendar}/", System.StringComparison.Ordinal))
        {
            target.IsDefault = true;
        }
        if (string.Equals(target.Uri, $"/{source.Owner.Username}/{CollectionUris.CalendarProxyRead}/", System.StringComparison.Ordinal) ||
            string.Equals(target.Uri, $"/{source.Owner.Username}/{CollectionUris.CalendarProxyWrite}/", System.StringComparison.Ordinal) ||
            string.Equals(target.Uri, $"/{source.Owner.Username}/{CollectionUris.PushSubscription}/", System.StringComparison.Ordinal))
        {
            target.IsDefault = true;
            target.IsTechnical = true;
        }
        if (target.CollectionSubType == CollectionSubType.SchedulingOutbox || target.CollectionSubType == CollectionSubType.SchedulingInbox)
        {
            target.IsDefault = true;
            target.IsTechnical = true;
        }
        if (source.ScheduleTransparency?.Equals(ScheduleTransparency.Transparent, System.StringComparison.InvariantCultureIgnoreCase) == true)
        {
            target.ExcludeFreeBusy = true;
        }
        var permissions = PrivilegeMask.None;
        switch (scope)
        {
            case PrivilegeScope.Owner:
                permissions = PrivilegeMask.All & source.OwnerMask;
                break;

            case PrivilegeScope.Admin:
                permissions = PrivilegeMask.All;
                break;

            case PrivilegeScope.Authenticated:
                {
                    var globalPermits = source.GlobalPermit;
                    if (grants is not null && grants.Count > 0)
                    {
                        var local = grants.FirstOrDefault(c => c.GrantorId == source.Id);
                        local ??= grants.FirstOrDefault(c => c.GrantorId == source.ParentId);
                        local ??= grants.FirstOrDefault(c => c.GrantorId == source.OwnerId);
                        permissions = (globalPermits | (local?.Privileges ?? PrivilegeMask.None)) & source.AuthorizedMask;
                    }
                    else
                    {
                        permissions = globalPermits & source.AuthorizedMask;
                    }
                }
                break;

            default:
                break;
        }
        target.Permissions = permissions;
        if (!permissions.HasAnyOf(PrivilegeMask.ReadAcl | PrivilegeMask.WriteAcl))
        {
            target.OwnerProhibit = target.AuthorizedProhibit = target.GlobalPermitSelf = null;
        }
        if (!permissions.HasAnyOf(PrivilegeMask.ReadAcl | PrivilegeMask.WriteAcl | PrivilegeMask.ReadCurrentUserPrivilegeSet))
        {
            target.Permissions = null;
        }
        return target;
    }

    public static List<CollectionResponse> ToView(this IEnumerable<Collection> source, PrivilegeScope scope = PrivilegeScope.Unauthenticated, List<GrantRelation>? grants = null)
    {
        return [.. source.Select(x => x.ToView(scope, grants))];
    }
}
