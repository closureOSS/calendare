using System.Collections.Generic;
using System.Linq;
using System.Net;
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
/// https://datatracker.ietf.org/doc/html/rfc3253#section-3.8
/// </summary>
public class ExpandPropertyReport : ReportBase, IReport
{
    private string PathBase = string.Empty;
    private ResourceRepository? ResourceRepository;

    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        // if (!resource.Privileges.HasFlag(PrivilegeMask.ReadCurrentUserPrivilegeSet))
        // {
        //     return new(PrivilegeMask.Read);
        // }
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null)
        {
            return new(HttpStatusCode.BadRequest);
        }
        if (!resource.Exists)
        {
            return new(HttpStatusCode.NotFound);
        }
        var env = httpContext.RequestServices.GetRequiredService<DavEnvironmentRepository>();
        PathBase = env.PathBase;

        ResourceRepository = httpContext.RequestServices.GetRequiredService<ResourceRepository>();
        var principal = resource.ResourceType == DavResourceType.Principal ? resource : (await ResourceRepository.ListPrincipalsAsResourceAsync(resource, true, httpContext.RequestAborted)).FirstOrDefault();
        if (principal is null)
        {
            return new(HttpStatusCode.NotFound);
        }

        var expandProperties = xmlRequestDoc.Root.Elements(XmlNs.Dav + "property");
        if (!expandProperties.Any())
        {
            // TODO: return empty document?
            return new(HttpStatusCode.BadRequest);
        }
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        await BaseNodeResponse(xmlRequestDoc.Root, xmlMultistatus, propertyRegistry, principal, httpContext);
        // testcase(s): 0542
        return new(xmlDoc);
    }

    private async Task<XElement> BaseNodeResponse(XElement xmlRequest, XElement xmlResponseParent, DavPropertyRepository propertyRegistry, DavResource resource, HttpContext httpContext)
    {
        var xmlResponse = new XElement(XmlNs.Dav + "response", new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.DavName}"));
        xmlResponseParent.Add(xmlResponse);
        var expandProperties = xmlRequest.Elements(XmlNs.Dav + "property");
        if (!expandProperties.Any())
        {
            // TODO: are missing property elements an error?
            return xmlRequest;
        }
        var xmlPropsSuccess = HandlerExtensions.XmlPropList("200 OK");
        var xmlPropsNotFound = HandlerExtensions.XmlPropList("404 Not Found");
        var xmlPropsForbidden = HandlerExtensions.XmlPropList("403 Forbidden");
        foreach (var xmlExpandProperty in expandProperties)
        {
            var attrName = xmlExpandProperty.Attribute("name");
            var attrNamespace = xmlExpandProperty.Attribute("namespace");
            if (attrName is null || attrNamespace is null)
            {
                // return new(HttpStatusCode.BadRequest);
                continue;
            }
            var propertyName = XName.Get(attrName.Value, attrNamespace.Value);
            var property = propertyRegistry.Property(propertyName, resource.ResourceType);
            if (property is null || property.GetValue is null)
            {
                xmlPropsNotFound.Add(new XElement(propertyName));
                continue;
            }
            var propGetSuccess = await property.GetValue(xmlExpandProperty, xmlExpandProperty, resource, httpContext);
            if (propGetSuccess != PropertyUpdateResult.Success)
            {
                // TODO: ignore, fail or report notfound on property to resolve?
                xmlPropsNotFound.Add(new XElement(propertyName));
                continue;
            }
            var xmlProperty = new XElement(propertyName);
            xmlPropsSuccess.Add(xmlProperty);
            var hrefs = xmlExpandProperty.Elements(XmlNs.Dav + "href");
            if (hrefs is null || !hrefs.Any())
            {
                // TODO: report notfound or leave empty?
                continue;
            }
            foreach (var href in hrefs)
            {
                var childContext = await ResourceRepository!.GetResourceAsync(new Middleware.CaldavUri(href.Value, PathBase), httpContext, httpContext.RequestAborted);
                if (!childContext.VerifyResourceType())
                {
                    continue;
                }
                // TODO: Check privileges to access the resource
                var dcProps = GetProperties(xmlExpandProperty);
                var xmlChild = await HandlerExtensions.PropertyResponse(propertyRegistry, childContext, null, dcProps, httpContext);
                // TODO: Replace with recursive call if <property> contains another <property> ...
                xmlProperty.Add(xmlChild);
            }
        }
        if (xmlPropsSuccess.Elements().Any())
        {
            xmlResponse.Add(xmlPropsSuccess.Parent);
        }
        if (xmlPropsForbidden.Elements().Any())
        {
            xmlResponse.Add(xmlPropsForbidden.Parent);
        }
        if (xmlPropsNotFound.Elements().Any())
        {
            xmlResponse.Add(xmlPropsNotFound.Parent);
        }
        return xmlResponse;
    }

    private static List<DavPropertyRef> GetProperties(XElement xml)
    {
        var xmlProperties = xml.Elements(XmlNs.Dav + "property");
        if (xmlProperties is null || !xmlProperties.Any())
        {
            return [];
        }
        var properties = new List<DavPropertyRef>();
        foreach (var xmlProperty in xmlProperties)
        {
            var attrName = xmlProperty.Attribute("name");
            if (attrName is null || attrName.Value is null)
            {
                continue;
            }
            var attrNamespace = xmlProperty.Attribute("namespace");
            // Log.Debug("Adding {prop}", subNode.Name);
            properties.Add(new DavPropertyRef
            {
                Name = XName.Get(attrName.Value, attrNamespace?.Value ?? "DAV:"),
                Element = xmlProperty,
                IsExpensive = false,
            });
        }
        return properties;
    }
}
