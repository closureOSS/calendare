using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calendare.VSyntaxReader.Properties;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    public async Task<List<SchedulingItem>> ApplyInboxMsg(HttpContext httpContext, List<SchedulingItem> messages)
    {
        var result = new List<SchedulingItem>();
        foreach (var msg in messages)
        {
            msg.IsResolved = true;
            if (msg.Resource is null)
            {
                // external delivery -> skipping
                continue;
            }
            switch (msg.Calendar.Method?.ToUpperInvariant())
            {
                case "REQUEST":
                    {
                        var response = await InboxRequest(httpContext, msg);
                        if (response is not null)
                        {
                            result.Add(response);
                        }
                    }
                    break;

                case "REPLY":
                    {
                        var response = await InboxReply(httpContext, msg);
                        if (response is not null)
                        {
                            result.AddRange(response);
                        }
                    }
                    break;

                case "CANCEL":
                    {
                        var response = await InboxCancel(httpContext, msg);
                        if (response is not null)
                        {
                            result.AddRange(response);
                        }
                    }
                    break;

                default:
                    Log.Error("Method {method} not supported in scheduling inbox", msg.Calendar.Method);
                    break;
            }
        }
        if (result.Any(x => x.IsResolved == false))
        {
            result.AddRange(await ApplyInboxMsg(httpContext, [.. result.Where(x => x.IsResolved == false)]));
        }
        return result;
    }

    private readonly List<string> InboxSyncProperties = [
        PropertyName.DateStart,
        PropertyName.DateEnd,
        PropertyName.DateStamp,
        PropertyName.Due,
        PropertyName.Duration,
        PropertyName.Created,
        PropertyName.LastModified,
        PropertyName.RecurrenceRule,
        PropertyName.RecurrenceDate,
        PropertyName.RecurrenceExceptionDate,
        PropertyName.RecurrenceExceptionRule,
        PropertyName.RecurrenceId,
        PropertyName.Organizer,
        PropertyName.Sequence,
        PropertyName.RequestStatus,
    ];
}
