using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;

namespace Calendare.Server.Repository;

public class CredentialRepository
{
    private readonly CalendareContext Db;

    public CredentialRepository(CalendareContext calendareContext)
    {
        Db = calendareContext;
    }

    public async Task<List<UsrCredential>> ListCredentials(Principal principal, CancellationToken cancellationToken)
    {
        return await Db.UsrCredential
            .Include(c => c.CredentialType)
            .Where(uc => uc.UsrId == principal.UserId)
            .OrderBy(uc => uc.CredentialTypeId).ThenBy(uc => uc.Accesskey)
            .ToListAsync(cancellationToken);
    }

    public async Task<(UsrCredential? Credential, bool Multiple)> GetCredential(CredentialRef credentialRef, CancellationToken cancellationToken)
    {
        var credentials = await Db.UsrCredential
            .Include(c => c.CredentialType)
            .Where(uc => uc.UsrId == credentialRef.Principal.UserId && uc.Accesskey == credentialRef.Subject
                && (credentialRef.CredentialType == null || uc.CredentialType.Label == credentialRef.CredentialType))
            .ToListAsync(cancellationToken);
        if (credentials is null || credentials.Count == 0)
        {
            return (null, false);
        }
        if (credentials.Count > 1)
        {
            return (null, true);
        }
        return (credentials[0], false);
    }

    public async Task<UsrCredential?> UpdateLock(CredentialRef credentialRef, bool doLock, CancellationToken cancellationToken)
    {
        var (credential, _) = await GetCredential(credentialRef, cancellationToken);
        if (credential is null) return null;
        if ((credential.Locked is not null && doLock) || (credential.Locked is null && !doLock))
        {
            return credential;
        }
        credential.Locked = doLock ? SystemClock.Instance.GetCurrentInstant() : null;
        await Db.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<UsrCredential?> Reset(CredentialRef credentialRef, string? passwordHash, CancellationToken cancellationToken)
    {
        var (credential, _) = await GetCredential(credentialRef, cancellationToken);
        if (credential is null || credential.CredentialTypeId == CredentialTypes.JwtBearer)
        {
            return null;
        }
        credential.Secret = passwordHash;
        credential.Modified = SystemClock.Instance.GetCurrentInstant();
        await Db.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<bool> Delete(CredentialRef credentialRef, CancellationToken cancellationToken)
    {
        var (credential, multiple) = await GetCredential(credentialRef, cancellationToken);
        if (credential is null)
        {
            return multiple == false;   // not found is treated as success
        }
        Db.UsrCredential.Remove(credential);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UsrCredential?> Create(Principal principal, UsrCredentialType credentialType, string username, string? passwordHash, string? description, CancellationToken cancellationToken)
    {
        var existing = await Db.UsrCredential.FirstOrDefaultAsync(c => c.Accesskey == username && c.CredentialTypeId == credentialType.Id, cancellationToken);
        if (existing is not null)
        {
            Log.Error("Credential username {username} already exists", username);
            return null;
        }
        var credential = new UsrCredential
        {
            UsrId = principal.UserId,
            CredentialTypeId = credentialType.Id,
            Accesskey = username,
            Secret = passwordHash,
            Description = description,
            Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
        };
        Db.UsrCredential.Add(credential);
        await Db.SaveChangesAsync(cancellationToken);
        await Db.Entry(credential).Reference(c => c.CredentialType).LoadAsync(cancellationToken);
        return credential;
    }

    public static UsrCredential BuildJwtBearerCredential(Usr user, string accessKey, string issuer)
    {
        var credential = new UsrCredential
        {
            Usr = user,
            CredentialTypeId = CredentialTypes.JwtBearer,
            Accesskey = accessKey,
            Issuer = issuer,
            Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
        };
        return credential;
    }

    public async Task<UsrCredential?> LinkByEmail(string email, string sub, string issuer, CancellationToken ct)
    {
        var user = await Db.Usr.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (user is null)
        {
            return null;
        }
        var credential = BuildJwtBearerCredential(user, sub, issuer);
        Db.UsrCredential.Add(credential);
        await Db.SaveChangesAsync(ct);
        return credential;
    }

    public async Task<UsrCredential?> Link(Usr target, string sub, string issuer, CancellationToken ct)
    {
        var user = await Db.Usr.FirstOrDefaultAsync(c => target.Id == c.Id, ct);
        if (user is null)
        {
            return null;
        }
        var credential = BuildJwtBearerCredential(user, sub, issuer);
        Db.UsrCredential.Add(credential);
        await Db.SaveChangesAsync(ct);
        return credential;
    }

}
