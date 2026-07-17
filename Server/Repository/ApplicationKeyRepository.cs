using System;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Server.Options;
using Calendare.Server.Utils;
using Microsoft.Extensions.Options;

namespace Calendare.Server.Repository;

public class ApplicationKeyRepository
{
    private readonly StringCryptor? Cryptor = null;

    public ApplicationKeyRepository(IOptions<UserConstraintOptions> userConstraints)
    {
        if (!string.IsNullOrEmpty(userConstraints.Value.ApplicationKeySecret))
        {
            Cryptor = new StringCryptor(userConstraints.Value.ApplicationKeySecret);
        }
    }

    public async Task<(string Username, string Password)?> DetectTokenAsync(string applicationKey, CancellationToken ct)
    {
        if (Cryptor is null) return null;
        var (Success, ClearText) = await Cryptor.TryDecryptBase64UrlAsync(applicationKey, ct);
        if (!Success || string.IsNullOrWhiteSpace(ClearText))
        {
            return null;
        }
        var parts = ClearText.Split('|');
        if (parts.Length != 2) return null;
        return (parts[0], parts[1]);
    }

    public static async Task<string> CreateTokenAsync(UserConstraintOptions userConstraint, string? username, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userConstraint.ApplicationKeySecret))
        {
            throw new ArgumentNullException(nameof(userConstraint));
        }
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentNullException(nameof(username));
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentNullException(nameof(password));
        }
        var sc = new StringCryptor(userConstraint.ApplicationKeySecret);
        return await sc.EncryptBase64UrlAsync($"{username}|{password}", ct);
    }
}
