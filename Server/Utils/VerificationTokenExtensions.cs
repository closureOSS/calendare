using System;
using Calendare.Data.Models;

namespace Calendare.Server.Utils;

public static class VerificationTokenExtensions
{
    public static (string? Email, string? Token) GetEmailVerificationToken(this Usr user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return (null, null);
        }
        return (user.Email, Token: $"{user.Email}|{DateTime.UtcNow.DayOfYear:D3}|{user.Id}".PrettyMD5Hash()[..6].ToUpperInvariant());
    }


    public static bool CheckVerificationToken(this Usr user, string challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge))
        {
            return false;
        }
        var (_, token) = GetEmailVerificationToken(user);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        return string.Equals(token, challenge, StringComparison.OrdinalIgnoreCase);
    }
}
