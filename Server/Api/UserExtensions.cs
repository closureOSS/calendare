using System.Text.RegularExpressions;
using Calendare.Data.Models;
using Calendare.Server.Utils;

namespace Calendare.Server.Api;

public static partial class UserExtensions
{
    public static bool Verify(this Usr usr)
    {
        if (!string.IsNullOrEmpty(usr.Email))
        {
            usr.Email = usr.Email.Trim();
            if (!usr.Email.IsEmailAddress())
            {
                return false;
            }
        }
        else
        {
            usr.Email = null;
            usr.EmailOk = null;
        }
        return IsValidUsername(usr.Username);
    }

    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }
        var trimmed = username.Trim();
        if (!trimmed.Equals(username, System.StringComparison.Ordinal))
        {
            return false;
        }
        if (username.Contains('@', System.StringComparison.Ordinal))
        {
            if (username.IsEmailAddress())
            {
                return true;
            }
        }
        else
        {
            if (UsernameRegex.IsMatch(username))
            {
                return true;
            }
        }
        return false;
    }

    [GeneratedRegex(@"^[a-zA-Z]+[a-zA-Z0-9._\-\ ]*[a-zA-Z0-9]+$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex UsernameRegex { get; }
}
