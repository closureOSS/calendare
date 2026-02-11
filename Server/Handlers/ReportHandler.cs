using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Reports;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the REPORT method.
/// </summary>
/// <remarks>
/// The specification of the REPORT method can be found in the
/// CalDav https://datatracker.ietf.org/doc/html/rfc4791#section-7.1 and WebDav https://datatracker.ietf.org/doc/html/rfc3253#section-3.6
/// specification.
/// </see>.
/// </remarks>
public partial class ReportHandler : HandlerBase, IMethodHandler
{
    private readonly Dictionary<XName, Type> Reports;

    public ReportHandler(DavEnvironmentRepository env, IOptions<CaldavOptions> config, RecorderSession recorder) : base(env, recorder)
    {
        Reports = config.Value.Reports;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (resource.Current is null && resource.ResourceType != Models.DavResourceType.Root && resource.ResourceType != Models.DavResourceType.Principal)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        if (!resource.Privileges.HasAnyOf())
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
            return;
        }

        var (xmlRequestDoc, _) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlRequestDoc is null || xmlRequestDoc?.Root is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest, "Report request definition missing");
            return;
        }
        Recorder.SetRequestBody(xmlRequestDoc);


        if (Reports.TryGetValue(xmlRequestDoc.Root.Name, out var reportType))
        {
            var properties = xmlRequestDoc.GetProperties();
            var report = (IReport)httpContext.RequestServices.GetRequiredService(reportType);
            var reportResponse = await report.Report(xmlRequestDoc, resource, properties, httpContext);
            if (reportResponse is not null)
            {
                if (reportResponse.Doc is not null)
                {
                    Recorder.SetResponseBody(reportResponse.Doc);
                    await response.BodyXmlAsync(reportResponse.Doc, HttpStatusCode.MultiStatus, httpContext.RequestAborted);
                }
                else if (!string.IsNullOrEmpty(reportResponse.Content) || !string.IsNullOrEmpty(reportResponse.ContentType))
                {
                    Recorder.SetResponseBody(reportResponse.Content ?? "");
                    response.ContentType = reportResponse.ContentType;
                    response.StatusCode = (int)reportResponse.StatusCode;
                    await response.WriteAsync(reportResponse.Content ?? "", httpContext.RequestAborted);
                }
                else
                {
                    if (reportResponse.MissingPrivilege == PrivilegeMask.None)
                    {
                        await WriteStatusAsync(httpContext, reportResponse.StatusCode, reportResponse.Comment);
                    }
                    else
                    {
                        await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, reportResponse.MissingPrivilege);
                    }
                }
            }
            else
            {
                await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            }
        }
        else
        {
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "supported-report", $"\"{xmlRequestDoc.Root.Name}\" is not a supported report type.");
        }
    }
}
