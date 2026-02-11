using System;
using EmailValidation;

namespace Calendare.Server.Utils;

public static class EmailExtensions
{
    public static bool IsEmailAddress(this string address)
    {
        return EmailValidator.Validate(address, allowTopLevelDomains: false, allowInternational: true);
    }

    public static string EmailFromUri(string email)
    {
        if (email.StartsWith("mailto:", StringComparison.InvariantCultureIgnoreCase))
        {
            email = email["mailto:".Length..];
        }
        return email;
    }
}
