using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the MOVE method.
/// Support for MOVE still under investigation, most propably it will be removed due to missing client support
/// WARNING: Functionality is mostly not tested
/// </summary>
/// <remarks>
/// The specification of the MOVE method can be found in the
/// https://datatracker.ietf.org/doc/html/rfc4918#section-9.9
/// CalDav specification
/// </see>.
/// </remarks>
public partial class MoveHandler : HandlerBase, IMethodHandler
{
    private readonly ResourceRepository CollectionRepository;
    private readonly ItemRepository ItemRepository;

    public MoveHandler(DavEnvironmentRepository env, ResourceRepository collectionRepository, RecorderSession recorder, ItemRepository itemRepository) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
        ItemRepository = itemRepository;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resourceSource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (!request.Headers.TryGetValue("Destination", out var destinations))
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest, "Destination is required");
            return;
        }
        if (destinations.Count != 1)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest, "More than one destination given");
            return;
        }
        var destination = destinations.First()!;
        if (destination.Contains(':', StringComparison.Ordinal))
        {
            var destinationUri = new Uri(destinations.First()!);
            destination = destinationUri.AbsolutePath;
        }
        if (PathBase is not null && destination.StartsWith(PathBase, StringComparison.Ordinal))
        {
            destination = destination[PathBase.Length..];
        }
        if (!resourceSource.Privileges.HasAnyOf(PrivilegeMask.WriteContent))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resourceSource.DavName, PrivilegeMask.WriteContent);
            return;
        }
        var resourceTarget = await CollectionRepository.GetResourceAsync(new CaldavUri(destination!), httpContext, httpContext.RequestAborted);
        if (resourceTarget is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        if (resourceTarget.ResourceType == DavResourceType.Unknown)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        if (!resourceTarget.Privileges.HasAnyOf(PrivilegeMask.WriteContent))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resourceTarget.DavName, PrivilegeMask.WriteContent);
            return;
        }
        switch (resourceSource.ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.Principal:
            case DavResourceType.User:
                Log.Error("MOVE on this resource type {uri} not supported", request.GetEncodedUrl());
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;
            default:
                break;
        }
        if (resourceSource.Exists == false)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        if (resourceTarget.Exists == true)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Conflict);
            return;
        }
        switch (resourceSource.ResourceType)
        {
            case DavResourceType.CalendarItem:
            case DavResourceType.AddressbookItem:
                // TODO: move object
                if (resourceSource.ParentResourceType != resourceTarget.ParentResourceType)
                {
                    // TODO: Implement proper error response message
                    await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                    return;
                }
                if (resourceSource.Object is null || resourceTarget.Parent is null)
                {
                    // TODO: Implement proper error response message
                    await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                    return;
                }
                await ItemRepository.MoveAsync(resourceSource.Object, resourceTarget.Parent, resourceTarget.DavName, httpContext.RequestAborted);
                await WriteStatusAsync(httpContext, HttpStatusCode.Created);
                return;

            case DavResourceType.Container:
            case DavResourceType.Calendar:
            case DavResourceType.Addressbook:
            case DavResourceType.Unknown:
                // TODO: Move collection
                break;
        }

        await WriteStatusAsync(httpContext, HttpStatusCode.NotImplemented);
    }
}
