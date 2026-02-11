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

    public async Task<UsrCredential?> GetCredential(Principal principal, int credentialId, CancellationToken cancellationToken)
    {
        return await Db.UsrCredential
            .Include(c => c.CredentialType)
            .Where(uc => uc.UsrId == principal.UserId && uc.Id == credentialId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UsrCredential?> UpdateLock(Principal principal, int credentialId, bool isLocked, CancellationToken cancellationToken)
    {
        var credential = await GetCredential(principal, credentialId, cancellationToken);
        if (credential is null)
        {
            return null;
        }
        if ((credential.Locked is not null && isLocked) || (credential.Locked is null && !isLocked))
        {
            return credential;
        }
        credential.Locked = isLocked ? SystemClock.Instance.GetCurrentInstant() : null;
        await Db.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<UsrCredential?> Reset(Principal principal, int credentialId, string username, string? password, CancellationToken cancellationToken)
    {
        var credential = await GetCredential(principal, credentialId, cancellationToken);
        if (credential is null)
        {
            return null;
        }
        if (!string.Equals(credential.Accesskey, username, System.StringComparison.Ordinal))
        {
            Log.Error("Username mismatch {username} differs from credential {credentialUsername}", username, credential.Accesskey);
            return null;
        }
        credential.Secret = password;
        credential.Modified = SystemClock.Instance.GetCurrentInstant();
        await Db.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task Delete(Principal principal, int credentialId, CancellationToken cancellationToken)
    {
        var credential = await GetCredential(principal, credentialId, cancellationToken);
        if (credential is null)
        {
            return; // not found is a success
        }
        Db.UsrCredential.Remove(credential);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UsrCredential?> Create(Principal principal, UsrCredentialType credentialType, string username, string? password, CancellationToken cancellationToken)
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
            Secret = password,
            Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
        };
        Db.UsrCredential.Add(credential);
        await Db.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<UsrCredential?> LinkByEmail(string email, string sub, string issuer, CancellationToken ct)
    {
        var user = await Db.Usr.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (user is null)
        {
            return null;
        }
        var credential = new UsrCredential
        {
            Usr = user,
            CredentialTypeId = CredentialTypes.JwtBearer,
            Accesskey = sub,
            Secret = issuer,
            Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
        };
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
        var credential = new UsrCredential
        {
            Usr = user,
            CredentialTypeId = CredentialTypes.JwtBearer,
            Accesskey = sub,
            Secret = issuer,
            Validity = new Interval(SystemClock.Instance.GetCurrentInstant(), Instant.MaxValue),
        };
        Db.UsrCredential.Add(credential);
        await Db.SaveChangesAsync(ct);
        return credential;
    }

}
