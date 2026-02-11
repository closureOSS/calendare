using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc3744#section-9.3
/// </summary>
public class PrincipalMatchReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var depth = httpContext.Request.GetDepth(0);
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null || !resource.Exists || depth != 0)
        {
            return new(HttpStatusCode.BadRequest);
        }
        var resourceRepository = httpContext.RequestServices.GetRequiredService<ResourceRepository>();
        var principal = (await resourceRepository.ListPrincipalsAsResourceAsync(resource, true, httpContext.RequestAborted)).FirstOrDefault();
        if (principal is null)
        {
            return new(HttpStatusCode.NotFound);
        }
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, principal, null, properties, httpContext);
        xmlMultistatus.Add(xmlResponse);
        return new(xmlDoc);
    }
}
