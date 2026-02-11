using System;
using System.Collections.Generic;
using System.Security.Cryptography;


// based on https://gist.githubusercontent.com/asimmon/f5288dfc6aca573122bda6761f0e9ea0/raw/7313fa4c1aa34bd35044c5c35e27714569f15eb5/BetterPasswordHasher.cs
// and explained in https://anthonysimmon.com/evolutive-and-robust-password-hashing-using-pbkdf2-in-dotnet/
namespace Calendare.Server.Utils;

public static class BetterPasswordHasher
{
    // Always use distinct version IDs for new versions, ideally by incrementing the highest version ID.
    private const byte VersionId1 = 0x01;
    private const byte VersionId2 = 0x02;

    private const byte DefaultVersionId = VersionId2;

    // Always preserve all the versions that might have been used to hash passwords.
    // Do not modify an existing version unless you're sure that no password was hashed with it yet.
    private static readonly Dictionary<byte, PasswordHasherVersion> Versions = new()
    {
        [VersionId1] = new PasswordHasherVersion(HashAlgorithmName.SHA256, SaltSize: 256 / 8, KeySize: 256 / 8, Iterations: 600000),
        [VersionId2] = new PasswordHasherVersion(HashAlgorithmName.SHA512, SaltSize: 512 / 8, KeySize: 512 / 8, Iterations: 210000),
    };

    public static string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        // Newly hashed passwords will always use the default version.
        var version = Versions[DefaultVersionId];

        // If you create a new version that makes the output size bigger than 1024 bytes,
        // consider allocating an array instead or using the shared array pool.
        var hashedPasswordByteCount = 1 + version.SaltSize + version.KeySize;
        Span<byte> hashedPasswordBytes = stackalloc byte[hashedPasswordByteCount];

        // Creating sub-spans is cheap and allow us to use span-based cryptography APIs.
        var saltBytes = hashedPasswordBytes.Slice(start: 1, length: version.SaltSize);
        var keyBytes = hashedPasswordBytes.Slice(start: 1 + version.SaltSize, length: version.KeySize);

        // Write the version ID first, the salt and then the key.
        hashedPasswordBytes[0] = DefaultVersionId;
        RandomNumberGenerator.Fill(saltBytes);
        Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, keyBytes, version.Iterations, version.Algorithm);

        return Convert.ToBase64String(hashedPasswordBytes);
    }

    public static PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        // We can predict the number of bytes that will be decoded from the base64 string,
        // to avoid allocating a byte array with "Convert.FromBase64String" and instead use span-based APIs.
        // Again, consider creating an array or using the shared array pool if the output size is bigger than 1024 bytes.
        var hashedPasswordByteCount = ComputeDecodedBase64ByteCount(hashedPassword);
        Span<byte> hashedPasswordBytes = stackalloc byte[hashedPasswordByteCount];

        if (!Convert.TryFromBase64String(hashedPassword, hashedPasswordBytes, out _))
        {
            // This shouldn't happen unless there's a mistake in how we compute the decoded base64 byte count.
            return PasswordVerificationResult.Failed;
        }

        if (hashedPasswordBytes.Length == 0)
        {
            return PasswordVerificationResult.Failed;
        }

        var versionId = hashedPasswordBytes[0];
        if (!Versions.TryGetValue(versionId, out var version))
        {
            // This can only happen if a developer removes a version from the dictionary,
            // or if someone was able to tampers with the hashed password.
            return PasswordVerificationResult.Failed;
        }

        var expectedHashedPasswordLength = 1 + version.SaltSize + version.KeySize;
        if (hashedPasswordBytes.Length != expectedHashedPasswordLength)
        {
            // The hashed password length doesn't match the expected length for the given version.
            // This can only happen if a developer modified an existing used version or if the hashed password was tampered with.
            return PasswordVerificationResult.Failed;
        }

        var saltBytes = hashedPasswordBytes.Slice(start: 1, length: version.SaltSize);
        var expectedKeyBytes = hashedPasswordBytes.Slice(start: 1 + version.SaltSize, length: version.KeySize);

        // Same stackalloc size considerations as above.
        Span<byte> actualKeyBytes = stackalloc byte[version.KeySize];
        Rfc2898DeriveBytes.Pbkdf2(providedPassword, saltBytes, actualKeyBytes, version.Iterations, version.Algorithm);

        // This method prevents leaking timing information when comparing the two byte spans.
        if (!CryptographicOperations.FixedTimeEquals(expectedKeyBytes, actualKeyBytes))
        {
            return PasswordVerificationResult.Failed;
        }

        // It's the responsibility of the caller to rehash the password if needed.
        return versionId != DefaultVersionId
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static int ComputeDecodedBase64ByteCount(string base64Str)
    {
        // Base64 encodes three bytes by four characters, and there can be up to two padding characters.
        var characterCount = base64Str.Length;
        var paddingCount = 0;

        if (characterCount > 0)
        {
            if (base64Str[characterCount - 1] == '=')
            {
                paddingCount++;

                if (characterCount > 1 && base64Str[characterCount - 2] == '=')
                {
                    paddingCount++;
                }
            }
        }

        return (characterCount * 3 / 4) - paddingCount;
    }

    private sealed record PasswordHasherVersion(HashAlgorithmName Algorithm, int SaltSize, int KeySize, int Iterations);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}
