using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;

public static class HandlerExtensions
{
    public static (XDocument xmlResponse, XElement xmlMultistatus) CreateMultistatusDocument()
    {
        var xmlMultistatus = new XElement(XmlNs.Dav + "multistatus");
        var xmlResponse = xmlMultistatus.CreateDocument();
        return (xmlResponse, xmlMultistatus);
    }

    public static (XDocument xmlResponse, XElement xmlError) CreateErrorDocument()
    {
        var xmlError = new XElement(XmlNs.Dav + "error");
        var xmlResponse = new XDocument(xmlError);
        if (xmlResponse.Root is null) throw new InvalidOperationException("XDocument must contain a root object");
        return (xmlResponse, xmlError);
    }

    public static (XDocument xmlResponse, XElement xmlNeedPrivileges) CreateNeedPrivilegeDocument()
    {
        var (xmlResponse, xmlError) = CreateErrorDocument();
        var xmlNeedPrivileges = new XElement(XmlNs.Dav + "need-privileges");
        xmlError.Add(xmlNeedPrivileges);
        return (xmlResponse, xmlNeedPrivileges);
    }

    public static XElement XmlPropList(string status)
    {
        var xmlPropstat = new XElement(XmlNs.Dav + "propstat");
        var xmlProp = new XElement(XmlNs.Dav + "prop");
        xmlPropstat.Add(xmlProp, new XElement(XmlNs.Dav + "status", $"HTTP/1.1 {status}"));
        return xmlProp;
    }

    public static async Task<XElement> PropertyResponse(DavPropertyRepository propertyRegistry, DavResource resource, string? href, List<DavPropertyRef>? properties, HttpContext ctx)
    {
        var xmlPropsSuccess = XmlPropList("200 OK");
        var xmlPropsNotFound = XmlPropList("404 Not Found");
        var xmlPropsForbidden = XmlPropList("403 Forbidden");
        var props = properties is not null && properties.Count != 0 ? [.. properties.Select(x => x.Name)] : propertyRegistry.ListDefaultPropertyNames(resource.ResourceType);
        foreach (var propName in props ?? [])
        {
            var davProperty = propertyRegistry.Property(propName, resource.ResourceType);
            var xmlProp = new XElement(propName);
            if (davProperty is not null)
            {
                PropertyUpdateResult isAllowed = davProperty.GetValue is null ? PropertyUpdateResult.Success : PropertyUpdateResult.NotFound;
                if (davProperty.GetValue is not null)
                {
                    var xmlQueryProp = properties?.FirstOrDefault(z => z.Name == propName)?.Element;
                    isAllowed = await davProperty.GetValue(xmlProp, xmlQueryProp, resource, ctx);
                }
                switch (isAllowed)
                {
                    case PropertyUpdateResult.Success:
                        xmlPropsSuccess.Add(xmlProp);
                        break;
                    case PropertyUpdateResult.Forbidden:
                        xmlPropsForbidden.Add(xmlProp);
                        break;
                    case PropertyUpdateResult.Ignore:
                        break;
                    default:
                    case PropertyUpdateResult.NotFound:
                        xmlPropsNotFound.Add(xmlProp);
                        break;
                }
            }
            else
            {
                xmlPropsNotFound.Add(xmlProp);
            }
        }

        var xmlResponse = new XElement(XmlNs.Dav + "response", new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{href ?? resource.DavName}"));
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
}
