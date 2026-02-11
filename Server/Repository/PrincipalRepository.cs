using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data;
using Calendare.Data.Models;
using Calendare.Server.Api;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Models.DavProperties;
using Calendare.Server.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Calendare.Server.Repository;

public class PrincipalRepository
{
    private readonly CalendareContext Db;
    private readonly StaticDataRepository StaticData;

    public PrincipalRepository(CalendareContext calendareContext, StaticDataRepository staticData)
    {
        Db = calendareContext;
        StaticData = staticData;
    }

    public async Task<Models.Principal?> GetPrincipalAsync(PrincipalQuery query, CancellationToken ct)
    {
        var usr = await GetUserAsync(query, ct);
        if (usr is not null && usr.Collections.Count > 0)
        {
            var principal = usr.Collections.First(c => c.CollectionType == CollectionType.Principal && c.ParentId == null).ToPrincipal();
            return principal;
        }
        return null;
    }

    public async Task<Usr?> GetUserAsync(PrincipalQuery query, CancellationToken ct)
    {
        var sql = QueryPrincipal(query);
        var usr = await sql.FirstOrDefaultAsync(ct);
        return usr;
    }

    private IQueryable<Usr> QueryPrincipal(PrincipalQuery query)
    {
        var sql = Db.Usr.AsQueryable();
        if (!query.IncludeProxy)
        {
            sql = sql
            .Include(u => u.Collections
                .Where(c => c.CollectionType == CollectionType.Principal && c.ParentId == null)
                .OrderBy(c => c.Uri)
            )
            .ThenInclude(c => c.PrincipalType);
        }
        else
        {
            sql = sql
            .Include(u => u.Collections
                .Where(c => c.CollectionType == CollectionType.Principal
                || c.CollectionSubType == CollectionSubType.SchedulingOutbox
                || c.CollectionSubType == CollectionSubType.SchedulingInbox)
                .OrderBy(c => c.Uri)
            )
            .ThenInclude(c => c.PrincipalType)
            ;
        }
        sql = sql.Include(u => u.Collections).ThenInclude(c => c.Grants.Where(g => g.GranteeId == query.CurrentUser.Id));
        if (!query.IsValid())
        {
            sql = sql.Where(u => false);
        }
        if (!string.IsNullOrEmpty(query.Username))
        {
            sql = sql.Where(u => u.Username == query.Username);
        }
        if (!string.IsNullOrEmpty(query.Email))
        {
            var email = EmailExtensions.EmailFromUri(query.Email);
            sql = sql.Where(u => u.Email == email && u.EmailOk != null);
        }
        if (!query.IsTracking)
        {
            sql = sql.AsNoTrackingWithIdentityResolution();
        }
        return sql;
    }

    public async Task<Models.Principal?> GetCurrentUserPrincipalAsync(IIdentity? identity, CancellationToken ct)
    {
        if (identity is null || identity?.Name is null)
        {
            return null;
        }
        var accessTypeId = identity.AuthenticationType switch
        {
            AuthenticationTypes.Basic => CredentialTypes.Password,
            AuthenticationTypes.JwtBearer => CredentialTypes.JwtBearer,
            _ => CredentialTypes.Password
        };
        var usr = await Db.Usr
            .Include(usr => usr.Credentials.Where(cred => cred.Accesskey == identity.Name))
            .Include(usr => usr.Collections.Where(col => col.CollectionType == CollectionType.Principal && col.ParentId == null)).ThenInclude(c => c.PrincipalType)
            .Where(c => c.Credentials.Any(cred => cred.Accesskey == identity.Name && cred.CredentialTypeId == accessTypeId && cred.Locked == null))
            .AsSingleQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ;
        if (usr is not null && usr.Collections.Count > 0)
        {
            var principal = usr.Collections.First().ToPrincipal();
            return principal;
        }
        return null;
    }

    public IQueryable<Collection> QueryPrincipalsAsync(PrincipalListQuery query)
    {
        var sql = Db.Collection
            .Include(u => u.Owner)
            .Include(u => u.PrincipalType)
            // .Include(u => u.Grants.Where(grants => grants.GranteeId == query.CurrentUser.Id))
            .Where(c => c.CollectionType == CollectionType.Principal && c.ParentId == null)
            ;
        if (query.CurrentUser.UserId != StockPrincipal.Admin || query.IncludeSystemAccounts == false)
        {
            sql = sql.Where(c => c.OwnerId != StockPrincipal.Admin);
        }
        if (!query.Unrestricted)
        {
            /* Similar to:
            select gr."privileges", c.* from collection c
            inner join grant_relation gr on gr.grantor_id = c.id
            where c."collection_type" = 'principal' and c.parent_id is null
            and gr.grantee_id in (
            select c.id from usr u
            inner join collection c on c.owner_id = u.id and c."collection_type" = 'principal'
            where u.username='user1')
            order by c.id;
            */
            sql = sql.Join(
                Db.GrantRelation.Where(gr => gr.GranteeId == query.CurrentUser.Id),
                grantor => grantor.Id,
                grantee => grantee.GrantorId,
                (grantor, grantee) => grantor
            );
            sql = sql.Concat(
                Db.Collection
                .Include(u => u.Owner)
                .Include(u => u.PrincipalType)
                .Where(c => c.Id == query.CurrentUser.Id || ((c.GlobalPermit & PrivilegeMask.Read) == PrivilegeMask.Read && c.CollectionType == CollectionType.Principal && c.ParentId == null))
            );
        }
        if (query.PrincipalTypes is not null)
        {
            var ptypes = query.PrincipalTypes.Select(c => c.Id).ToList();
            sql = sql.Where(c => c.PrincipalTypeId != null && ptypes.Contains(c.PrincipalTypeId.Value));
        }
        if (!query.IsTracking)
        {
            sql = sql.AsNoTracking();
        }
        if (!string.IsNullOrEmpty(query.SearchTerm))
        {
            var searchTerm = $"%{query.SearchTerm}%";
            sql = sql.Where(c =>
                c.DisplayName == null || EF.Functions.ILike(c.DisplayName, searchTerm) ||
                c.Owner.Email == null || EF.Functions.ILike(c.Owner.Email, searchTerm) ||
                c.Owner.Username == null || EF.Functions.ILike(c.Owner.Username, searchTerm)
            );
        }
        return sql;
    }


    public async Task<List<PrincipalResponse>> ListPrincipalsAsync(PrincipalListQuery query, CancellationToken ct)
    {
        var sql = QueryPrincipalsAsync(query);
        var usr = await sql
            .Select(u => new PrincipalIntermediateResponse
            {
                Uri = u.Uri,
                Username = u.Owner.Username,
                PrincipalType = u.PrincipalType,
                Email = u.Owner.Email,
                DisplayName = u.DisplayName,
                Timezone = u.Timezone,
                Description = u.Description,
                Locale = u.Owner.Locale,
                DateFormatType = u.Owner.DateFormatType,
                Color = u.Color,
                OrderBy = u.OrderBy,
                IsRoot = u.OwnerId == StockPrincipal.Admin,
                Granted = u.Grants.FirstOrDefault(c => c.GranteeId == query.CurrentUser.Id),
                GlobalPermit = u.GlobalPermit,
                IsOwner = u.OwnerId == query.CurrentUser.UserId,
                // OrderBy=u.Grants.FirstOrDefault()==null?0:1,
            })
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken: ct);
        return [.. usr.ToView(query.CurrentUser.Id).DistinctBy(c => c.Uri)];
    }

    public async Task<PrincipalType?> GetPrincipalTypeAsync(string type, CancellationToken ct)
    {
        var pts = await Db.PrincipalType.Where(pts => pts.Label == type).SingleOrDefaultAsync(ct);
        return pts;
    }

    public async Task<string> CreateAsync(Usr usr, Principal? admin, CancellationToken ct)
    {
        usr.Credentials.ToList().ForEach(x => { x.Usr = usr; x.LastUsed = null; });
        usr.Collections.ToList().ForEach(x => { x.Owner = usr; x.Etag = x.Uri.PrettyMD5Hash(); });
        var principalCollection = usr.Collections.FirstOrDefault(x => x.IsMainPrincipal());
        var proxyRead = usr.Collections.FirstOrDefault(c => c.IsProxyRead());
        var proxyWrite = usr.Collections.FirstOrDefault(c => c.IsProxyWrite());
        foreach (var collection in usr.Collections.OrderBy(c => c.Uri, System.StringComparer.OrdinalIgnoreCase))
        {
            CollectionRepository.CalculatePermissions(collection, collection.Parent);
        }
        Db.Usr.Add(usr);
        if (principalCollection is not null)
        {
            if (proxyRead is not null)
            {
                var privs = StaticData.RelationshipTypeList[RelationshipTypes.Read];
                Db.GrantRelation.Add(new GrantRelation
                {
                    GrantorId = principalCollection.Id,
                    GranteeId = proxyRead.Id,
                    GrantTypeId = privs.Id,
                    Privileges = privs.Privileges,
                });
            }
            if (proxyWrite is not null)
            {
                var privs = StaticData.RelationshipTypeList[RelationshipTypes.ReadWrite];
                Db.GrantRelation.Add(new GrantRelation
                {
                    GrantorId = principalCollection.Id,
                    GranteeId = proxyWrite.Id,
                    GrantTypeId = privs.Id,
                    Privileges = privs.Privileges,
                });
            }
            if (admin is not null)
            {
                var privs = StaticData.RelationshipTypeList[RelationshipTypes.Administers];
                Db.GrantRelation.Add(new GrantRelation
                {
                    GrantorId = principalCollection.Id,
                    GranteeId = admin.Id,
                    GrantTypeId = privs.Id,
                    Privileges = privs.Privileges,
                });
            }
        }
        await Db.SaveChangesAsync(ct);
        return usr.Username;
    }

    public async Task<Usr?> UpdateAsync(Principal principal, UserAmendRequest request, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(principal.Username, ct);
        var collectionPrincipal = currentUser?.Collections.FirstOrDefault();
        if (currentUser is null || collectionPrincipal is null)
        {
            return null;
        }
        collectionPrincipal.DisplayName = request.DisplayName;
        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Equals(currentUser.Email, System.StringComparison.InvariantCultureIgnoreCase))
        {
            currentUser.Email = request.Email;
            // currentUser.EmailOk = SystemClock.Instance.GetCurrentInstant();
            currentUser.EmailOk = null;
        }
        if (!string.IsNullOrWhiteSpace(request.DateFormatType))
        {
            currentUser.DateFormatType = request.DateFormatType;
        }
        if (!string.IsNullOrWhiteSpace(request.Locale))
        {
            currentUser.Locale = request.Locale;
        }
        if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            collectionPrincipal.Timezone = request.Timezone;
        }
        if (!string.IsNullOrWhiteSpace(request.Color))
        {
            collectionPrincipal.Color = request.Color;
        }
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            collectionPrincipal.Description = request.Description;
        }
        currentUser.Modified = collectionPrincipal.Modified = SystemClock.Instance.GetCurrentInstant();
        await Db.SaveChangesAsync(ct);
        return currentUser;
    }

    public async Task<Usr?> ConfirmEmail(Principal principal, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(principal.Username, ct);
        if (currentUser is null)
        {
            return null;
        }
        if (currentUser.EmailOk is null)
        {
            currentUser.EmailOk =
            currentUser.Modified = SystemClock.Instance.GetCurrentInstant();
            await Db.SaveChangesAsync(ct);
        }
        return currentUser;
    }

    public async Task<Usr?> DeleteAsync(string username, CancellationToken ct)
    {
        // TODO: permissions ???
        var usr = await GetUserAsync(username, ct);
        if (usr is null || usr.Id == StockPrincipal.Admin)
        {
            return null;
        }
        Db.Remove(usr);
        await Db.SaveChangesAsync(ct);
        return usr;
    }

    private async Task<Usr?> GetUserAsync(string username, CancellationToken ct)
    {
        return await QueryPrincipal(new PrincipalQuery { CurrentUser = new(), Username = username, IsTracking = true, }).FirstOrDefaultAsync(ct);
    }

}
