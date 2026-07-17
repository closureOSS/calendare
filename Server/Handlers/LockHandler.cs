using System;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the LOCK method.
///
/// This is a empty implementation to fullfil standard requirements. No locking or unlocking is taking place.
/// </summary>
/// <remarks>
/// The specification of the LOCK method can be found in <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.10"></see>.
/// </remarks>
public partial class LockHandler : HandlerBase, IMethodHandler
{
    private readonly ItemRepository ItemRepository;
    private readonly CollectionRepository CollectionRepository;

    public LockHandler(DavEnvironmentRepository env, CollectionRepository collectionRepository, ItemRepository itemRepository, RecorderSession recorder) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
        ItemRepository = itemRepository;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        // if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
        // {
        //     Log.Warning("Content type missing or invalid", request.ContentType);
        // }
        switch (resource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
                // case DavResourceType.Calendar:
                // case DavResourceType.Addressbook:
                Log.Error("LOCK on this resource type {uri} not supported", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;

            default:
                break;
        }
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Read | PrivilegeMask.Write))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
            return;
        }
        // TODO: If header (to update an existing LOCK, e.g. If: (<urn:uuid:e71d4fae-5dec-22d6-fea5-00a0c91e6be4>))
        var davLock = new DavLock
        {
            InfiniteDepth = request.GetDepth(0) != 0,
            Timeout = request.GetTimeout(),
        };
        var (xmlRequest, xmlSuccess) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlSuccess == false)
        {
            SetEtagHeader(response, resource.Current?.Etag ?? resource.DavEtag);
            SetContentLocation(response, resource.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, Precondition.InvalidXml);
            return;
        }
        if (xmlRequest is not null)
        {
            if (xmlRequest?.Root is null || xmlRequest.Root.Name != XmlNs.Dav + "lockinfo")
            {
                SetEtagHeader(response, resource.Current?.Etag ?? resource.DavEtag);
                SetContentLocation(response, resource.Uri.Path);
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, Precondition.InvalidXml);
                return;
            }
            Recorder.SetRequestBody(xmlRequest);
            xmlRequest.GetLockinfo(davLock);
        }
        davLock.Token = $"urn:uuid:{Guid.NewGuid()}";
        // TODO: Add Lock-Token Header
        response.Headers["Lock-Token"] = $"<{davLock.Token}>";
        var xmlDoc = XElementLockinfoExtensions.LockResponse(davLock, resource);
        // SetCapabilitiesHeader(response, resource.ResourceType);
        await response.BodyXmlAsync(xmlDoc, HttpStatusCode.OK, httpContext.RequestAborted);
    }

}
