using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Utils;
using NodaTime;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true)]
public static partial class UserRegisterRequestMapper
{
    [MapperIgnoreTarget(nameof(Usr.Id))]
    [MapperIgnoreTarget(nameof(Usr.Username))]
    [MapperIgnoreTarget(nameof(Usr.EmailOk))]
    [MapperIgnoreTarget(nameof(Usr.Created))]
    [MapperIgnoreTarget(nameof(Usr.Modified))]
    [MapperIgnoreTarget(nameof(Usr.Closed))]
    [MapperIgnoreSource(nameof(UserAmendRequest.DisplayName))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Timezone))]
    [MapperIgnoreSource(nameof(UserAmendRequest.Color))]
    [MapperIgnoreSource(nameof(UserAmendRequest.Description))]
    [MapperIgnoreSource(nameof(UserAmendRequest.IsEmailVerified))]
    private static partial Usr Map(UserAmendRequest source);

    [MapperIgnoreTarget(nameof(Usr.Id))]
    [MapperIgnoreTarget(nameof(Usr.EmailOk))]
    [MapperIgnoreTarget(nameof(Usr.Created))]
    [MapperIgnoreTarget(nameof(Usr.Modified))]
    [MapperIgnoreTarget(nameof(Usr.Closed))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.SkipCollections))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.DisplayName))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Timezone))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Color))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Description))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Password))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Type))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.IsEmailVerified))]
    private static partial Usr Map(UserRegisterRequest source);

    [MapProperty(nameof(UserRegisterRequest.Password), nameof(UsrCredential.Secret))]
    [MapProperty(nameof(UserRegisterRequest.Username), nameof(UsrCredential.Accesskey))]
    [MapperIgnoreTarget(nameof(UsrCredential.Id))]
    [MapperIgnoreTarget(nameof(UsrCredential.UsrId))]
    [MapperIgnoreTarget(nameof(UsrCredential.Usr))]
    [MapperIgnoreTarget(nameof(UsrCredential.Created))]
    [MapperIgnoreTarget(nameof(UsrCredential.Modified))]
    [MapperIgnoreTarget(nameof(UsrCredential.Locked))]
    [MapperIgnoreTarget(nameof(UsrCredential.Validity))]
    [MapperIgnoreTarget(nameof(UsrCredential.LastUsed))]
    [MapperIgnoreTarget(nameof(UsrCredential.CredentialType))]
    [MapperIgnoreTarget(nameof(UsrCredential.CredentialTypeId))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Email))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.IsEmailVerified))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.DateFormatType))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.SkipCollections))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Locale))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.IsActive))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.DisplayName))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Color))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Description))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.Type))]
    [MapperIgnoreSource(nameof(UserAmendRequest.Timezone))]
    [MapperIgnoreSource(nameof(UserRegisterRequest.IsEmailVerified))]
    private static partial UsrCredential MapAccess(UserRegisterRequest source);

    public static Usr ToDto(this UserRegisterRequest source)
    {
        var target = Map(source);
        if (source.IsEmailVerified)
        {
            target.EmailOk = SystemClock.Instance.GetCurrentInstant();
        }
        return target;
    }

    public static UsrCredential ToAccessDto(this UserRegisterRequest source)
    {
        var target = MapAccess(source);
        if (!string.IsNullOrEmpty(target.Secret))
        {
            target.Secret = BetterPasswordHasher.HashPassword(target.Secret);
            target.CredentialTypeId = CredentialTypes.Password;
        }
        return target;
    }

    public static List<Usr> ToDto(this IEnumerable<UserRegisterRequest> source)
    {
        return [.. source.Select(x => x.ToDto())];
    }

    public static Usr ToDto(this UserAmendRequest source)
    {
        var target = Map(source);
        if (source.IsEmailVerified)
        {
            target.EmailOk = SystemClock.Instance.GetCurrentInstant();
        }
        return target;
    }
}
