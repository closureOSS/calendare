using System;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the Schedule GET method
/// iSchedule Receiver Capabilities
/// </summary>
/// <remarks>
/// https://datatracker.ietf.org/doc/html/draft-desruisseaux-ischedule-05
/// https://datatracker.ietf.org/doc/html/draft-desruisseaux-ischedule-05#section-5.1
/// </remarks>
public partial class ScheduleGetHandler : HandlerBase, IMethodHandler
{
    public ScheduleGetHandler(DavEnvironmentRepository env, RecorderSession recorder) : base(env, recorder)
    {
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        if (!Env.HasFeatures(CalendareFeatures.AutoScheduling, httpContext))
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        // var request = httpContext.Request;
        var response = httpContext.Response;
        response.Headers["iSchedule-Version"] = "1.0";
        response.Headers["iSchedule-Capabilities"] = "123";
        var xmlScheduleResponse = new XElement(XmlNs.ISchedule + "query-result",
        new XElement(XmlNs.ISchedule + "capabilities",
            new XElement(XmlNs.ISchedule + "serial-number", 123),
            new XElement(XmlNs.ISchedule + "versions", new XElement(XmlNs.ISchedule + "version", "1.0"))
        // TODO: Complete capabilities document ...
        ));

        var xmlDoc = new XDocument(xmlScheduleResponse);
        if (xmlDoc.Root is null) throw new InvalidOperationException("XDocument must contain a root object");
        await response.BodyXmlAsync(xmlDoc, HttpStatusCode.OK, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }
}
