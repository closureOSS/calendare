using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Calendare.Server.Migrations;

partial class MigrationRepository
{
    private async Task StaticDataUpdate01_Migration(CancellationToken ct)
    {
        await MergeUsrCredentialType(ct);
        await MergePrincipalType(ct);
        await MergeGrantType(ct);

        await Context.SaveChangesAsync(ct);
    }

    private async Task MergeGrantType(CancellationToken ct)
    {
        var dbList = await Context.GrantType.ToListAsync(ct);
        foreach (var dbItem in dbList)
        {
            if (StaticData.RelationshipTypeList.TryGetValue((Constants.RelationshipTypes)dbItem.Id, out var hit))
            {
                if (!string.Equals(hit.Name, dbItem.Name, System.StringComparison.Ordinal))
                {
                    dbItem.Name = hit.Name;
                }
                if (hit.Privileges != dbItem.Privileges)
                {
                    dbItem.Privileges = hit.Privileges;
                }
            }
            else
            {
                Log.Warning("Obsolete grant type {grantTypeId}/{grantType} exists", dbItem.Id, dbItem.Confers);

            }
        }
        foreach (var staticItem in StaticData.RelationshipTypeList)
        {
            var hit = dbList.FirstOrDefault(r => r.Id == (int)staticItem.Key);
            if (hit is null)
            {
                Context.GrantType.Add(staticItem.Value);
            }
        }
    }

    private async Task MergePrincipalType(CancellationToken ct)
    {
        var dbList = await Context.PrincipalType.ToListAsync(ct);
        foreach (var dbItem in dbList)
        {
            var hit = StaticData.PrincipalTypeList.Values.FirstOrDefault(r => r.Id == dbItem.Id);
            if (hit is not null)
            {
                if (!string.Equals(hit.Name, dbItem.Name, System.StringComparison.Ordinal))
                {
                    dbItem.Name = hit.Name;
                }
                if (!string.Equals(hit.Label, dbItem.Label, System.StringComparison.Ordinal))
                {
                    dbItem.Label = hit.Label;
                }
            }
            else
            {
                Log.Warning("Obsolete principal type {principalType} exists", dbItem.Label);
            }
        }
        foreach (var staticItem in StaticData.PrincipalTypeList)
        {
            var hit = dbList.FirstOrDefault(r => r.Id == staticItem.Value.Id);
            if (hit is null)
            {
                Context.PrincipalType.Add(staticItem.Value);
            }
        }
    }

    private async Task MergeUsrCredentialType(CancellationToken ct)
    {
        var dbList = await Context.UsrCredentialType.ToListAsync(ct);
        foreach (var dbItem in dbList)
        {
            if (StaticData.UserAccessTypeList.TryGetValue(dbItem.Label, out var hit))
            {
                if (!string.Equals(hit.Name, dbItem.Name, System.StringComparison.Ordinal))
                {
                    dbItem.Name = hit.Name;
                }
            }
            else
            {
                Log.Warning("Obsolete credential type {credentialType} exists", dbItem.Label);
            }
        }
        foreach (var staticItem in StaticData.UserAccessTypeList)
        {
            var hit = dbList.FirstOrDefault(r => string.Equals(r.Label, staticItem.Key, System.StringComparison.Ordinal));
            if (hit is null)
            {
                Context.UsrCredentialType.Add(staticItem.Value);
            }
        }
    }
}
