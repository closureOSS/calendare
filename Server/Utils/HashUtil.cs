using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Calendare.Server.Utils;

public static class HashUtil
{
    public static string PrettyMD5Hash(this string text) => PrettyHash(MD5Hash(text));

    public static string UrlEncodedMD5Hash(this string text) => Base64Hash(MD5Hash(text));


    private static ReadOnlySpan<byte> MD5Hash(string text) => MD5.HashData(Encoding.UTF8.GetBytes(text));

    private static string PrettyHash(ReadOnlySpan<byte> hash) => Convert.ToHexStringLower(hash);

    private static string Base64Hash(ReadOnlySpan<byte> hash) => Base64Url.EncodeToString(hash);
}
