using System.Security.Cryptography;

namespace Calendare.Server.Utils;

public static class CharacterGenerator
{
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static char GetRandomChar()
    {
        var index = RandomNumberGenerator.GetInt32(AlphanumericChars.Length);
        return AlphanumericChars[index];
    }
}
