using System;
using System.Security.Cryptography;

namespace Calendare.Server.Utils;

public static class PasswordGenerator
{
    public static string RandomPassword(int length = 24)
    {
        return RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-.,/;:!?", length);
    }

    public static string RandomUriSegment(int length = 12)
    {
        return $"{RandomNumberGenerator.GetString("abcdefghijkmnpqrstuvwxyz", 1)}{RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789-.", Math.Max(0, length - 1))}";
    }
}
