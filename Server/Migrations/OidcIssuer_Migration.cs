using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Server.Migrations;

partial class MigrationRepository
{
    private async Task OidcIssuer04_Migration(CancellationToken ct)
    {
        await MoveOidcIssuerAsync(ct);
        await MarkLegacyCredentialsAsync(ct);

        await Context.SaveChangesAsync(ct);
    }

    private async Task MoveOidcIssuerAsync(CancellationToken ct)
    {
        var dbList = await Context.UsrCredential
            .Where(c => c.CredentialTypeId == 3)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        foreach (var co in dbList)
        {
            co.Issuer = co.Secret;
            co.Description = $"OIDC from {co.Issuer}";
            co.Secret = null;
        }
    }

    private async Task MarkLegacyCredentialsAsync(CancellationToken ct)
    {
        var dbList = await Context.UsrCredential
            .Where(c => c.CredentialTypeId == 1 && c.Validity != null)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        foreach (var co in dbList)
        {
            co.Description = $"Legacy credential, switch to application credential";
        }
    }
}
