using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Calendare.Server.Utils;


public class StringCryptor
{
    private byte[] Key = [];

    public StringCryptor(string passphrase)
    {
        Key = DeriveKeyFromPassphrase(passphrase, 256 / 8).password;
    }

    public async Task<string> DecryptAsync(byte[] ciphertext, CancellationToken ct)
    {
        using var aes = AesCreate();
        aes.Key = Key;
        string result;
        using (MemoryStream ms = new(ciphertext))
        {
            var iv = new byte[aes.BlockSize / 8];
            var readCnt = await ms.ReadAsync(iv, ct);
            aes.IV = iv;
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (CryptoStream cryptoStream = new(ms, decryptor, CryptoStreamMode.Read))
            {
                using (var streamReader = new StreamReader(cryptoStream, Encoding.UTF8))
                {
                    result = await streamReader.ReadToEndAsync(ct);
                }
            }
        }
        return result;
    }

    public async Task<byte[]> EncryptAsync(string cleartext, CancellationToken ct)
    {
        using Aes aes = AesCreate();
        aes.Key = Key;
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] result;
        using (MemoryStream ms = new())
        {
            await ms.WriteAsync(aes.IV, 0, aes.IV.Length, ct);
            using (CryptoStream cryptoStream = new(ms, encryptor, CryptoStreamMode.Write))
            {
                await cryptoStream.WriteAsync(Encoding.UTF8.GetBytes(cleartext), ct);
                await cryptoStream.FlushFinalBlockAsync(ct);
                result = ms.ToArray();
            }
        }
        return result;
    }

    private static Aes AesCreate()
    {
        var aes = Aes.Create();
        // aes.KeySize = 256;
        // aes.BlockSize = 128;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;
        return aes;
    }

    private (byte[] password, byte[] salt) DeriveKeyFromPassphrase(string password, int keysize)
    {
        var salt = Array.Empty<byte>();
        // var salt = RandomNumberGenerator.GetBytes(keysize);
        var iterations = 4096;
        var desiredKeyLength = keysize;
        var hashMethod = HashAlgorithmName.SHA384;
        Key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, hashMethod, desiredKeyLength);
        return (Key, salt);
    }
}

public static class StringCryptorExtensions
{
    extension(StringCryptor cryptor)
    {
        public async Task<string> EncryptBase64Async(string cleartext, CancellationToken ct) => Convert.ToBase64String(await cryptor.EncryptAsync(cleartext, ct));
        public async Task<string> DecryptBase64Async(string ciphertext, CancellationToken ct) => await cryptor.DecryptAsync(Convert.FromBase64String(ciphertext), ct);
        public async Task<string> EncryptBase64UrlAsync(string cleartext, CancellationToken ct) => Base64UrlEncoder.Encode(await cryptor.EncryptAsync(cleartext, ct));
        public async Task<string> DecryptBase64UrlAsync(string ciphertext, CancellationToken ct) => await cryptor.DecryptAsync(Base64UrlEncoder.DecodeBytes(ciphertext), ct);
        public async Task<(bool Success, string? ClearText)> TryDecryptBase64UrlAsync(string ciphertext, CancellationToken ct)
        {
            try
            {
                var cleartext = await cryptor.DecryptAsync(Base64UrlEncoder.DecodeBytes(ciphertext), ct);
                return (true, cleartext);
            }
            catch (Exception)
            {
                return (false, null);
            }
        }
    }
}
