using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the PROPFIND method.
/// </summary>
/// <remarks>
/// The specification of the PROPFIND method can be found in the
/// <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.1">
/// Webdav specification
/// </see>.
/// </remarks>
public class PropFindHandler : HandlerBase, IMethodHandler
{
    private readonly ResourceRepository ResourceRepository;

    public PropFindHandler(DavEnvironmentRepository env, ResourceRepository resourceRepository, RecorderSession recorder) : base(env, recorder)
    {
        ResourceRepository = resourceRepository;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resourceBase)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (!resourceBase.Privileges.HasAnyOf(PrivilegeMask.Read | PrivilegeMask.Write))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resourceBase.DavName, PrivilegeMask.Read);
            return;
        }
        var depth = request.GetDepth();
        List<DavPropertyRef> properties = [];
        var (xmlRequest, xmlSuccess) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlSuccess == false)
        {
            SetEtagHeader(response, resourceBase.Current?.Etag ?? resourceBase.DavEtag);
            SetContentLocation(response, resourceBase.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, Precondition.InvalidXml);
            return;
        }
        if (xmlRequest is not null)
        {
            if (xmlRequest?.Root is null || xmlRequest.Root.Name != XmlNs.Dav + "propfind")
            {
                SetEtagHeader(response, resourceBase.Current?.Etag ?? resourceBase.DavEtag);
                SetContentLocation(response, resourceBase.Uri.Path);
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, Precondition.InvalidXml);
                return;
            }
            Recorder.SetRequestBody(xmlRequest);
            properties = xmlRequest.GetProperties();
        }
        SetCapabilitiesHeader(response, resourceBase.ResourceType);
        if (resourceBase.Exists == false)
        {
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.NotFound, Precondition.MustExist, "That resource is not present on server.");
            return;
        }
        var principalResources = await ResourceRepository.ListPrincipalsAsResourceAsync(resourceBase, onlySelf: false, httpContext.RequestAborted);    // loads additional data for resourceBase
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        var topOfTree = DavResourceNode.CreateRoot(resourceBase);
        // Resolve to infinity not generally implemented (collections within collections)
        if (depth != 0)
        {
            switch (resourceBase.ResourceType)
            {
                case DavResourceType.Root:
                    topOfTree.AddRange(principalResources);
                    await ResourceRepository.ListChildrenAsResourcesAsync(topOfTree, resourceBase, depth, httpContext.RequestAborted);
                    break;

                case DavResourceType.Principal:
                    await ResourceRepository.ListChildrenAsResourcesAsync(topOfTree, resourceBase, depth, httpContext.RequestAborted);
                    break;

                case DavResourceType.Calendar:
                case DavResourceType.Addressbook:
                    topOfTree.AddChildren(await ResourceRepository.ListChildObjectsAsResourcesAsync(resourceBase, httpContext.RequestAborted));
                    break;

                case DavResourceType.Container:
                    if (depth > 1)
                    {
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.Forbidden, Precondition.PropfindFiniteDepth, "Only depth 0 or 1 supported.");
                        return;
                    }
                    await ResourceRepository.ListChildrenAsResourcesAsync(topOfTree, resourceBase, 1, httpContext.RequestAborted);
                    //  topOfTree.AddChildren(await ResourceRepository.ListDirectChildrenAsResourcesAsync(resourceBase, httpContext.RequestAborted));
                    topOfTree.AddChildren(await ResourceRepository.ListBlobsAsResourcesAsync(resourceBase, httpContext.RequestAborted));
                    break;

                default:
                    break;
            }
        }
        var resourceList = topOfTree.ToList(depth);
        foreach (var resource in resourceList)
        {
            if (resource is null || !resource.VerifyResourceType())
            {
                await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                return;
            }
        }
        if (resourceList.Count == 1)
        {
            SetContentLocation(response, resourceBase.DavName);
            SetEtagHeader(response, resourceBase.DavEtag);
        }
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        foreach (var resource in resourceList)
        {
            var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, resource, href: null, properties, httpContext);
            xmlMultistatus.Add(xmlResponse);
        }

        await response.BodyXmlAsync(xmlDoc, HttpStatusCode.MultiStatus, httpContext.RequestAborted);
        Recorder.SetResponseBody(xmlDoc);
    }
}
