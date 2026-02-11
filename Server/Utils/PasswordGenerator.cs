using System.Security.Cryptography;

namespace Calendare.Server.Utils;

public static class PasswordGenerator
{
    public static string RandomPassword(int length = 24)
    {
        return RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-.,/;:!?", length);
    }
}
