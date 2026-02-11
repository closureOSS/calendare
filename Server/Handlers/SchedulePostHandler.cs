using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Api.Models;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the Schedule POST method.
/// </summary>
/// <remarks>
///
///
/// </see>.
/// </remarks>
public partial class SchedulePostHandler : HandlerBase, IMethodHandler
{

    public SchedulePostHandler(DavEnvironmentRepository env, RecorderSession recorder) : base(env, recorder)
    {
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        if (!Env.HasFeatures(CalendareFeatures.AutoScheduling, httpContext))
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }

        await WriteStatusAsync(httpContext, HttpStatusCode.NotImplemented);
    }
}
