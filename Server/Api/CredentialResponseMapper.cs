using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None, ThrowOnMappingNullMismatch = false)]
public static partial class CredentialResponseMapper
{
    [MapperIgnoreSource(nameof(UsrCredential.Secret))]
    [MapProperty(nameof(UsrCredential.Accesskey), nameof(CredentialResponse.Subject))]
    [MapProperty([nameof(Usr), nameof(Usr.Username)], nameof(CredentialResponse.Username))]
    [MapProperty([nameof(Usr), nameof(Usr.Email)], nameof(CredentialResponse.Email))]
    [MapProperty([nameof(Usr), nameof(Usr.EmailOk)], nameof(CredentialResponse.EmailOk))]
    private static partial CredentialResponse Map(UsrCredential source);

    [MapperIgnoreSource(nameof(UsrCredential.Secret))]
    [MapProperty(nameof(UsrCredential.Accesskey), nameof(CredentialResponse.Subject))]
    [MapProperty([nameof(Usr), nameof(Usr.Username)], nameof(CredentialResponse.Username))]
    [MapProperty([nameof(Usr), nameof(Usr.Email)], nameof(CredentialResponse.Email))]
    [MapProperty([nameof(Usr), nameof(Usr.EmailOk)], nameof(CredentialResponse.EmailOk))]
    private static partial CredentialCreateResponse MapCreate(UsrCredential source);

    public static CredentialResponse ToView(this UsrCredential source)
    {
        var target = Map(source);
        if (source.Validity is not null)
        {
            target.ValidFrom = source.Validity.Value.HasStart && source.Validity.Value.Start != NodaTime.Instant.MinValue ? source.Validity.Value.Start : null;
            target.ValidTo = source.Validity.Value.HasEnd && source.Validity.Value.End != NodaTime.Instant.MaxValue ? source.Validity.Value.End : null;
        }
        return target;
    }

    public static List<CredentialResponse> ToView(this IEnumerable<UsrCredential> source)
    {
        return [.. source.Select(x => x.ToView())];
    }

    public static CredentialCreateResponse ToCreateResponse(this UsrCredential source, string? secret = null)
    {
        var target = MapCreate(source);
        if (source.Validity is not null)
        {
            target.ValidFrom = source.Validity.Value.HasStart && source.Validity.Value.Start != NodaTime.Instant.MinValue ? source.Validity.Value.Start : null;
            target.ValidTo = source.Validity.Value.HasEnd && source.Validity.Value.End != NodaTime.Instant.MaxValue ? source.Validity.Value.End : null;
        }
        target.Secret = secret;
        return target;
    }
}

