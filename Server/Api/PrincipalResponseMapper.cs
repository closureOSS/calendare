using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None, ThrowOnMappingNullMismatch = false)]
public static partial class PrincipalResponseMapper
{
    // [MapperIgnoreSource(nameof(UsrCredential.Secret))]
    // [MapProperty(nameof(UsrCredential.Accesskey), nameof(CredentialResponse.Subject))]
    // [MapProperty([nameof(Usr), nameof(Usr.Username)], nameof(CredentialResponse.Username))]
    // [MapProperty([nameof(Usr), nameof(Usr.Email)], nameof(CredentialResponse.Email))]
    // [MapProperty([nameof(Usr), nameof(Usr.EmailOk)], nameof(CredentialResponse.EmailOk))]
    private static partial PrincipalResponse Map(PrincipalIntermediateResponse source);

    public static PrincipalResponse ToView(this PrincipalIntermediateResponse source, int? currentUserId = null)
    {
        var target = Map(source);
        var permissions = source.IsOwner || currentUserId == StockPrincipal.Admin ? PrivilegeMask.All : source.GlobalPermit;
        if (source.Granted is not null)
        {
            permissions |= source.Granted.Privileges;
        }
        target.Permissions = permissions;
        // if ((permissions & (PrivilegeMask.Read)) == PrivilegeMask.None)
        // {
        //     target.Timezone = target.DateFormatType = target.Color = target.Locale = null;
        //     target.HasGroups = target.HasScheduling = null;
        // }
        return target;
    }

    public static List<PrincipalResponse> ToView(this IEnumerable<PrincipalIntermediateResponse> source, int? currentUserId = null)
    {
        return [.. source.Select(x => x.ToView(currentUserId))];
    }
}
