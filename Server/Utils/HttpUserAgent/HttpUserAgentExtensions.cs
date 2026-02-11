using System.Text.RegularExpressions;
using Calendare.Server.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Utils.HttpUserAgent;


public static partial class HttpUserAgentExtensions
{
    public static UserAgent? DetectUserAgent(this HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("User-Agent", out var userAgentRaw))
        {
            return null;
        }
        var raw = userAgentRaw.ToString();

        var matchMacCalendar = MacCalendarRegex().Match(raw);
        if (matchMacCalendar.Success)
        {
            return new UserAgent { Raw = raw, CalendarClientType = CalendarClientType.MacOSCalendar, Platform = Platform.MacOS | Platform.IOS, };
        }

        var matchEMClient = EMClientRegex().Match(raw);
        if (matchEMClient.Success)
        {
            return new UserAgent { Raw = raw, CalendarClientType = CalendarClientType.EMClient, Platform = Platform.NotDetected, };
        }

        var matchDAVx5 = DAVx5Regex().Match(raw);
        if (matchDAVx5.Success)
        {
            return new UserAgent { Raw = raw, CalendarClientType = CalendarClientType.DAVx5, Platform = Platform.Android, };
        }

        var thunderBird = ThunderbirdRegex().Match(raw);
        if (thunderBird.Success)
        {
            return new UserAgent { Raw = raw, CalendarClientType = CalendarClientType.Thunderbird, Platform = Platform.NotDetected, };
        }

        return new UserAgent { Raw = raw, CalendarClientType = CalendarClientType.NotDetected, Platform = Platform.NotDetected };
    }

    // Mac Calendar/[Mac]
    //      User-Agent: macOS/15.2 (24C101) dataaccessd/1.0
    //      User-Agent: macOS/15.1.1 (24B91) dataaccessd/1.0
    //      User-Agent: macOS/14.6.1 (23G93) dataaccessd/1.0
    //      User-Agent: iOS/15.2 (24C101) remindd/1203
    [GeneratedRegex(@"(\S+OS)\/([\d.]+) \((.*)\) (dataaccessd|remindd)\/(?'version'[\d.]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex MacCalendarRegex();

    // emClient/[*]
    //      User-Agent: eMClient/10.1.4588.0
    //      User-Agent: eMClient/10.1.4828.0
    [GeneratedRegex(@"eMClient\/(?'version'[\d.]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex EMClientRegex();

    // Vivaldi/Windows:
    //      User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36

    // DAVx5/Android:
    //      User-Agent: DAVx5/4.4.4-ose (dav4jvm; okhttp/4.12.0) Android/8.1.0
    [GeneratedRegex(@"DAVx5\/(?'version'[\S]+) (.*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex DAVx5Regex();

    // Tasks/Android:
    //      User-Agent: org.tasks/14.1 (okhttp3) Android/8.1.0

    // Thunderbird/[*]
    //      User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Thunderbird/128.5.1
    [GeneratedRegex(@"Mozilla(.*) Thunderbird\/(?'version'[\d.]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ThunderbirdRegex();

    // OneCalendar Android APP
    //      User-Agent: curl/7.54
}
