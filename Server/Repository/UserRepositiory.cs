using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Calendare.Server.Repository;

public partial class UserRepository
{
    private readonly CalendareContext Db;
    private readonly StaticDataRepository StaticData;
    private readonly PrincipalRepository PrincipalRepository;

    public UserRepository(CalendareContext calendareContext, PrincipalRepository principalRepository, StaticDataRepository staticData)
    {
        Db = calendareContext;
        StaticData = staticData;
        PrincipalRepository = principalRepository;
    }

    public async Task<Claim[]?> VerifyAsync(string username, string password, string issuer, CancellationToken ct)
    {
        var verified = await GetVerifiedUser(username, password, ct);
        if (verified is null)
        {
            return null;
        }
        var (usr, usrAccess) = verified.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, usrAccess.Accesskey, ClaimValueTypes.String, issuer),
            new(ClaimTypes.PrimarySid, usr.Id.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer, issuer),
            new(JwtRegisteredClaimNames.Sub, usr.Username, ClaimValueTypes.String, issuer),
        };
        // if (!string.IsNullOrEmpty(usr.Fullname))
        // {
        //     claims.Add(new Claim(ClaimTypes.Surname, usr.Fullname, ClaimValueTypes.String, issuer));
        // }
        if (!string.IsNullOrEmpty(usr.Email) && usr.EmailOk is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, usr.Email, ClaimValueTypes.String, issuer));
        }
        usrAccess.LastUsed = SystemClock.Instance.GetCurrentInstant();
        await Db.SaveChangesAsync(ct);
        return [.. claims];
    }

    public async Task<(Usr User, UsrCredential Credential)?> GetVerifiedUser(string username, string password, CancellationToken ct)
    {
        var usrAccess = await Db.UsrCredential.FirstOrDefaultAsync(u => u.Accesskey == username, cancellationToken: ct);
        if (usrAccess is null)
        {
            return null;
        }
        var result = BetterPasswordHasher.VerifyHashedPassword(usrAccess.Secret ?? "", password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }
        var usr = await Db.Usr.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usrAccess.UsrId, cancellationToken: ct);
        if (usr is null)
        {
            return null;
        }
        return (usr, usrAccess);
    }

    public async Task<Models.Principal?> GetPrincipalAsync(string username, CancellationToken ct)
    {
        return await PrincipalRepository.GetPrincipalAsync(new PrincipalQuery { CurrentUser = new(), Username = username, IsTracking = true, }, ct);
    }

    public async Task<Models.Principal?> GetCurrentUserPrincipalAsync(IIdentity? identity, CancellationToken ct)
    {
        return await PrincipalRepository.GetCurrentUserPrincipalAsync(identity, ct);
    }

    [return: NotNullIfNotNull(nameof(email))]
    public async Task<Models.Principal?> GetPrincipalByEmailAsync(string? email, CancellationToken ct)
    {
        return await PrincipalRepository.GetPrincipalAsync(new PrincipalQuery { CurrentUser = new(), Email = email, IsTracking = true, }, ct);
    }

    public async Task<PrivilegeMask> CheckPrivilegeAsync(Models.Principal grantor, Models.Principal grantee, CancellationToken ct)
    {
        if (grantor.Id == StockPrincipal.Admin)
        {
            var root = await Db.Collection.Where(c => c.OwnerId == StockPrincipal.Admin && c.ParentId == null).FirstOrDefaultAsync(ct);
            if (root is not null)
            {
                grantor.Id = root.Id;
            }
        }

        var rel = await Db.GrantRelation
            .Where(x => x.GrantorId == grantor.Id && x.GranteeId == grantee.Id)
            .ToListAsync(ct);
        if (rel is not null && rel.Count == 1)
        {
            return rel[0].Privileges;
        }
        return PrivilegeMask.None | grantor.GlobalPermit;
    }

    public async Task<PrincipalType?> GetPrincipalTypeAsync(string type, CancellationToken ct)
    {
        return await PrincipalRepository.GetPrincipalTypeAsync(type, ct);
    }

    public async Task<Collection?> GetPrincipalAsCollectionAsync(string userName, CancellationToken ct)
    {
        return await Db.Collection
            // .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
            // .Include(c => c.Groups).ThenInclude(m => m.PrincipalType)
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => c.Owner.Username == userName && c.CollectionType == CollectionType.Principal && c.ParentId == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Collection?> GetPrincipalAsCollectionAsync(int collectionId, CancellationToken ct)
    {
        return await Db.Collection
            // .Include(c => c.Members).ThenInclude(m => m.PrincipalType)
            // .Include(c => c.Groups).ThenInclude(m => m.PrincipalType)
            .Include(c => c.PrincipalType)
            .Include(c => c.Owner)
            .Where(c => c.Id == collectionId && c.CollectionType == CollectionType.Principal)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Collection>> GetCollectionsByType(int ownerId, CollectionSubType collectionSubType, CancellationToken ct)
    {
        var query = Db.Collection
            .Include(c => c.PrincipalType)
            .Where(c => c.OwnerId == ownerId && c.CollectionSubType == collectionSubType)
            ;
        // if (collectionSubType == CollectionType.Calendar && isDefault)
        // {
        //     query = query.Where(c => c.Uri.EndsWith($"/{CollectionUris.DefaultCalendar}/"));
        // }
        // if (collectionSubType == CollectionType.Addressbook && isDefault)
        // {
        //     query = query.Where(c => c.Uri.EndsWith($"/{CollectionUris.DefaultAddressbook}/"));
        // }
        return await query.ToListAsync(ct) ?? [];
    }
}
