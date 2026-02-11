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
        if (!string.IsNullOrEmpty(usr.Username))
        {
            var trimmed = usr.Username.Trim();
            if (!trimmed.Equals(usr.Username, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (usr.Username.Contains('@', System.StringComparison.Ordinal))
            {
                if (usr.Username.IsEmailAddress())
                {
                    return true;
                }
            }
            else
            {
                if (UsernameRegex().IsMatch(usr.Username))
                {
                    return true;
                }
            }
        }
        return false;
    }

    [GeneratedRegex(@"^[a-zA-Z]+[a-zA-Z0-9._\-\ ]*[a-zA-Z0-9]+$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex UsernameRegex();
}
