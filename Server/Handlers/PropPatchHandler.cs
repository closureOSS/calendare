using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the PROPPATCH method.
/// </summary>
/// <remarks>
/// The specification of the PROPPATCH method can be found in the
/// <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.2">
/// Webdav specification
/// </see>.
/// </remarks>
public class PropPatchHandler : HandlerBase, IMethodHandler
{
    private readonly CollectionRepository CollectionRepository;

    public PropPatchHandler(DavEnvironmentRepository env, CollectionRepository collectionRepository, RecorderSession recorder) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (resource.ResourceType == Models.DavResourceType.Unknown || resource.Exists == false)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        var (xmlRequest, _) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlRequest is null || xmlRequest?.Root is null)
        {
            // response.Headers.ETag = resource.Current?.Etag;
            SetEtagHeader(response, resource.Current?.Etag);
            // response.Headers.ContentLocation = $"{PathBase}{resource.Uri.Path}";
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "invalid-xml");
            return;
        }
        if (xmlRequest.Root.Name != XmlNs.Dav + "propertyupdate")
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
            return;
        }
        Recorder.SetRequestBody(xmlRequest);
        // if (resource.Exists == false)
        // {
        //     await WriteErrorXmlAsync(httpContext, HttpStatusCode.NotFound, XmlNs.Dav + "must-exist", "That resource is not present on server.");
        //     return;
        // }
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.WriteProperties))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.WriteProperties);
            return;
        }
        var propsAmend = xmlRequest.GetPropertiesStatic();
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();

        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.CalendarItem:
            case DavResourceType.AddressbookItem:
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;

            case DavResourceType.Unknown:
                Log.Error("Nothing found at that location {uri}", resource.DavName);
                await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                return;

            default:
                if (!resource.VerifyResourceType())
                {
                    Log.Error("PROPPATCH {uri} requires existing collection", resource.Uri.Path);
                    await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                    return;
                }
                break;
        }
        Collection? collection = await CollectionRepository.GetAsync(resource.DavName, httpContext.RequestAborted);
        var status = await UpdateProperties(collection, propertyRegistry, resource, propsAmend, httpContext);
        if (!status.Failure && collection is not null)
        {
            await CollectionRepository.UpdateAsync(collection, httpContext.RequestAborted);
        }
        var xmlResponse = Response(resource.DavName, status);
        xmlMultistatus.Add(xmlResponse);

        await response.BodyXmlAsync(xmlDoc, HttpStatusCode.MultiStatus, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }

    public static async Task<DavPatchStatus> UpdateProperties(Collection? collection, DavPropertyRepository propertyRegistry, DavResource resource, List<DavPropertyStatic> propsAmend, HttpContext httpContext)
    {
        var status = new DavPatchStatus { Properties = propsAmend, };
        if (collection is null)
        {
            status.Failure = true;
            status.ResponseDescription = "Collection was not found";
            status.Error = XmlNs.Dav + "todo---not-found";
            return status;
        }
        foreach (var prop in propsAmend)
        {
            var propFunc = propertyRegistry.Property(prop.Name, resource.ResourceType);
            if (propFunc is null)
            {
                prop.StatusCode = "405 Not found";
            }
            else
            {
                if (prop.ToDelete == true)
                {
                    if (propFunc.Remove is null)
                    {
                        prop.StatusCode = "403 Forbidden";
                        status.Error = XmlNs.Dav + "cannot-modify-protected-property";
                        status.ResponseDescription = "Deletion not supported";
                    }
                    else
                    {
                        if (prop.Value is not null)
                        {
                            prop.IsSuccess = await propFunc.Remove(prop.Value, resource, collection, httpContext);
                        }
                        if (prop.IsSuccess != PropertyUpdateResult.Success)
                        {
                            prop.StatusCode = "409 Conflict";
                            status.ResponseDescription = "Deletion not possible";
                        }
                    }
                }
                else
                {
                    if (propFunc.Update is null)
                    {
                        prop.StatusCode = "403 Forbidden";
                        status.Error = XmlNs.Dav + "cannot-modify-protected-property";
                    }
                    else
                    {
                        if (prop.Value is not null)
                        {
                            prop.IsSuccess = await propFunc.Update(prop.Value, resource, collection, httpContext);
                        }
                        if (prop.IsSuccess != PropertyUpdateResult.Success)
                        {
                            prop.StatusCode = "409 Conflict";
                        }
                    }
                }
            }
            if (prop.IsSuccess != PropertyUpdateResult.Success)
            {
                status.Failure = true;
                // break;
            }
        }
        return status;
    }

    private XElement Response(string href, DavPatchStatus status)
    {
        var xmlResponse = new XElement(XmlNs.Dav + "response", new XElement(XmlNs.Dav + "href", ExternalUrl(href)));
        return PropStatResponse(xmlResponse, status);
    }

    public static XElement PropStatResponse(XElement xmlResponse, DavPatchStatus status)
    {
        if (status.Failure)
        {
            var grouped = status.Properties.GroupBy(x => x.StatusCode, System.StringComparer.Ordinal);
            foreach (var grp in grouped)
            {
                var xmlPropstat = new XElement(XmlNs.Dav + "propstat",
                    new XElement(XmlNs.Dav + "status", $"HTTP/1.1 {grp.Key}"));
                xmlResponse.Add(xmlPropstat);
                var xmlProp = new XElement(XmlNs.Dav + "prop");
                xmlPropstat.Add(xmlProp);
                foreach (var prop in grp)
                {
                    xmlProp.Add(new XElement(prop.Name));
                }
            }
            if (!string.IsNullOrEmpty(status.ResponseDescription))
            {
                xmlResponse.Add(new XElement(XmlNs.Dav + "responsedescription", status.ResponseDescription));
            }
            if (status.Error is not null)
            {
                xmlResponse.Add(new XElement(XmlNs.Dav + "error", new XElement(status.Error)));
            }
        }
        else
        {
            var xmlPropstat = new XElement(XmlNs.Dav + "propstat", new XElement(XmlNs.Dav + "status", "HTTP/1.1 200 Ok"));
            xmlResponse.Add(xmlPropstat);
            var xmlProp = new XElement(XmlNs.Dav + "prop");
            xmlPropstat.Add(xmlProp);
            foreach (var prop in status.Properties)
            {
                xmlProp.Add(new XElement(prop.Name));
            }
        }
        return xmlResponse;
    }
}
