using System;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serilog;

namespace Calendare.Server.Migrations;

partial class MigrationRepository
{
    private async Task Initial_Migration(CancellationToken ct)
    {
        foreach (var pt in StaticData.UserAccessTypeList) { Context.UsrCredentialType.Add(pt.Value); }
        foreach (var pt in StaticData.PrincipalTypeList) { Context.PrincipalType.Add(pt.Value); }
        foreach (var pt in StaticData.RelationshipTypeList) { Context.GrantType.Add(pt.Value); }
        await Context.SaveChangesAsync(ct);
        await CreateRoot(ct);
    }

    private async Task CreateRoot(CancellationToken ct)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        var principalTypes = await Context.PrincipalType.ToListAsync(ct);
        var PrincipalTypePerson = principalTypes.Find(x => string.Equals(x.Label, PrincipalTypeCode.Individual, StringComparison.Ordinal)) ?? throw new InvalidOperationException("Valid principal type required");
        var accessTypes = await Context.UsrCredentialType.ToListAsync(ct);
        var PasswordAccess = accessTypes.Find(x => string.Equals(x.Label, CredentialTypes.PasswordCode, StringComparison.Ordinal)) ?? throw new InvalidOperationException("Valid credential type is required");
        {
            await Context.SaveChangesAsync(ct);
        }
        // create admin user
        var admin = new Usr
        {
            Id = StockPrincipal.Admin,
            IsActive = true,
            Username = BootstrapOptions.Username ?? "admin",
            Email = BootstrapOptions.Email ?? "calendare@closure.ch",
            EmailOk = now,
            DateFormatType = BootstrapOptions.DateFormatType ?? UserDefaults.DateFormatType,
            Locale = BootstrapOptions.Locale ?? UserDefaults.Locale,
        };
        // Hint: An initial admin password is highly recommended; otherwise a random and unknown admin password is generated at shown ONCE in the log file.
        var adminPassword = PasswordGenerator.RandomPassword();
        if (!string.IsNullOrWhiteSpace(BootstrapOptions.Password))
        {
            adminPassword = BootstrapOptions.Password;
        }
        else
        {
            Log.Error("Root admin password is {password}, for security reason change password immediately", adminPassword);
        }
        admin.Credentials.Add(new UsrCredential
        {
            Usr = admin,
            Secret = BetterPasswordHasher.HashPassword(adminPassword),
            Accesskey = BootstrapOptions.Username ?? "admin",
            CredentialType = PasswordAccess,
            Validity = new Interval(now, Instant.MaxValue),
        });
        admin.Collections.Add(new Collection
        {
            Owner = admin,
            CollectionType = CollectionType.Principal,
            PrincipalType = PrincipalTypePerson,
            ParentContainerUri = "/",
            Uri = $"/{admin.Username}/",
            DisplayName = BootstrapOptions.DisplayName ?? "Administrator",
            AuthorizedProhibit = PrivilegeMask.None,
            AuthorizedMask = PrivilegeMask.All,
            OwnerProhibit = PrivilegeMask.None,
            OwnerMask = PrivilegeMask.All,
            GlobalPermitSelf = PrivilegeMask.None,
            GlobalPermit = PrivilegeMask.None,
        });
        Context.Usr.Add(admin);
        await Context.SaveChangesAsync(ct);
    }
}
