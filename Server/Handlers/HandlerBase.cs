using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;


public abstract class HandlerBase
{
    protected readonly RecorderSession Recorder;
    protected readonly string PathBase;
    protected readonly DavEnvironmentRepository Env;

    protected HandlerBase(DavEnvironmentRepository env, RecorderSession recorderSession)
    {
        Recorder = recorderSession;
        PathBase = env.PathBase;
        Env = env;
    }

    public async Task WriteStatusAsync(HttpContext httpContext, HttpStatusCode httpStatusCode, string? comment = null)
    {
        var response = httpContext.Response;
        response.StatusCode = (int)httpStatusCode;
        if (comment is not null)
        {
            response.ContentLength = Encoding.UTF8.GetByteCount(comment);
            response.ContentType = $"{MimeContentTypes.Plaintext}; {MimeContentTypes.Utf8}";
            await response.WriteAsync(comment, cancellationToken: httpContext.RequestAborted);
            Recorder.SetResponseBody(comment);
        }
    }

    public async Task WriteErrorXmlAsync(HttpContext httpContext, HttpStatusCode httpStatusCode, XName errorName, string? comment = null)
    {
        var (xmlDoc, xmlError) = HandlerExtensions.CreateErrorDocument();
        if (string.IsNullOrEmpty(comment))
        {
            xmlError.Add(new XElement(errorName));
        }
        else
        {
            xmlError.Add(new XElement(errorName), comment);
        }
        await httpContext.Response.BodyXmlAsync(xmlDoc, httpStatusCode, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }

    public async Task WriteErrorNeedPrivilegeAsync(HttpContext httpContext, string uri, PrivilegeMask privileges)
    {
        var (xmlDoc, xmlNeedPrivileges) = HandlerExtensions.CreateNeedPrivilegeDocument();
        xmlNeedPrivileges.AddMissingPrivileges(ExternalUrl(uri), privileges);
        await httpContext.Response.BodyXmlAsync(xmlDoc, HttpStatusCode.Forbidden, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }

    public void SetEtagHeader(HttpResponse response, string? etag)
    {
        if (!string.IsNullOrEmpty(etag))
        {
            response.Headers.ETag = $"\"{etag}\"";
        }
    }

    public void SetScheduleHeader(HttpResponse response, string? scheduleTag)
    {
        if (!string.IsNullOrEmpty(scheduleTag))
        {
            response.Headers["Schedule-Tag"] = $"\"{scheduleTag}\"";
        }
    }

    protected string ExternalUrl(string? uri) => $"{PathBase}{uri}";

    protected void SetContentLocation(HttpResponse response, string? uri) => response.Headers.ContentLocation = ExternalUrl(uri);

    protected void SetLocation(HttpResponse response, string? uri) => response.Headers.Location = ExternalUrl(uri);

    protected void SetCapabilitiesHeader(HttpResponse response)
    {
        // For 1,2,3 see https://datatracker.ietf.org/doc/html/rfc4918#section-18
        // as LOCK is not supported, only 1 and 3 are reported
        var capabilities = new List<string> {
            "1", "3", "access-control",
            "calendar-access",          // https://datatracker.ietf.org/doc/html/rfc4791#section-5.1
            "calendar-no-timezone",     // https://datatracker.ietf.org/doc/html/rfc7809#section-3.1.1"
            "extended-mkcol",           // https://datatracker.ietf.org/doc/html/rfc5689#section-3.1
            "add-member",
            "bind",                     // https://datatracker.ietf.org/doc/html/rfc5842.html#section-8.1
            "addressbook",
            "sync-collection",
            "calendar-availability",    // https://datatracker.ietf.org/doc/html/rfc7953#section-7.2.1
        };
        if (Env.HasFeatures(CalendareFeatures.ResourceSharing, response.HttpContext))
        {
            capabilities.AddRange(["resource-sharing"]); // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-4.1
            capabilities.AddRange(["calendarserver-sharing"]);  // caldav-sharing.txt, similar to resource-sharing; SABRE reports this option instead of resource-sharing
        }
        if (Env.HasFeatures(CalendareFeatures.CalendarProxy, response.HttpContext))
        {
            capabilities.AddRange(["calendar-proxy"]); // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
        }
        if (Env.HasFeatures(CalendareFeatures.AutoScheduling, response.HttpContext))
        {
            // calendar-auto-schedule https://datatracker.ietf.org/doc/html/rfc6638#section-2
            capabilities.AddRange(["calendar-auto-schedule", "calendar-schedule"]);
        }
        if (Env.HasFeatures(CalendareFeatures.WebdavPush, response.HttpContext))
        {
            // https://bitfireat.github.io/webdav-push/draft-bitfire-webdav-push-00.html#name-service-detection
            capabilities.AddRange(["webdav-push"]);
        }
        response.Headers["DAV"] = capabilities
            .Order(System.StringComparer.Ordinal)
            .Select((s, i) => new { Index = i, Capability = s })
            .GroupBy(x => x.Index / 7)
            .Select(g => string.Join(',', g.Select(x => x.Capability)))
            .ToArray();
    }
}
