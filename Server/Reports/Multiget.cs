using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc6352#section-10.7 for Addressbook and
/// ??? for Calendar
/// </summary>
public class MultigetReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource baseResource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var ct = httpContext.RequestAborted;
        var env = httpContext.RequestServices.GetRequiredService<DavEnvironmentRepository>();
        var PathBase = env.PathBase;
        var itemRepository = httpContext.RequestServices.GetRequiredService<ItemRepository>();

        var hrefs = GetHrefs(xmlRequestDoc) ?? [];
        var calendarItems = await itemRepository.ListByUriAsync(hrefs.Select(x => CleanUri(x.Value, PathBase)).ToArray(), ct);
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var href in hrefs)
        {
            var ci = calendarItems.FirstOrDefault(c => string.Equals(c.Uri, CleanUri(href.Value, PathBase), System.StringComparison.Ordinal));
            if (ci is not null)
            {
                DavResource? resource = null;
                if (ci.CalendarItem is not null)
                {
                    resource = baseResource.Graft(ci.CalendarItem);
                }
                else if (ci.AddressItem is not null)
                {
                    resource = baseResource.Graft(ci.AddressItem);
                }
                if (resource is not null)
                {
                    var xmlProperty = await HandlerExtensions.PropertyResponse(propertyRegistry, resource, null, properties, httpContext);
                    xmlMultistatus.Add(xmlProperty);
                }
            }
            else
            {
                var xmlNotFound = new XElement(XmlNs.Dav + "response", href, new XElement(XmlNs.Dav + "status", "HTTP/1.1 404 Not Found"));
                xmlMultistatus.Add(xmlNotFound);
            }
        }

        return new(xmlDoc);
    }

    private static List<XElement>? GetHrefs(XDocument xDocument)
    {
        return [.. xDocument.Descendants().Where(x => x.Name == XmlNs.Dav + "href")];
    }

    private static string CleanUri(string path, string? pathBase)
    {
        if (pathBase is not null)
        {
            if (path.StartsWith(pathBase, System.StringComparison.Ordinal))
            {
                return path[pathBase.Length..];
            }
        }
        return path;
    }
}
