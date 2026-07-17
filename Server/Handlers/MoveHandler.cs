using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Storage;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the MOVE method.
/// Support for MOVE for CALDAV resources is removed due to missing calendar client support
/// </summary>
/// <remarks>
/// The specification of the MOVE method can be found in the <see cref="https://datatracker.ietf.org/doc/html/rfc4918#section-9.9"/>WebDav specification</see>.
/// </remarks>
public class MoveHandler(DavEnvironmentRepository env, MoveCopyRepository MoveRepository, ResourceRepository ResourceRepository, RecorderSession recorder) : HandlerBase(env, recorder), IMethodHandler
{
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
        var destination = UriUtils.RemovePathBase(destinations[0]!, PathBase);
        if (!resourceSource.Privileges.HasAnyOf(PrivilegeMask.WriteContent))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resourceSource.DavName, PrivilegeMask.WriteContent);
            return;
        }
        // See also https://datatracker.ietf.org/doc/html/rfc4918#section-9.9.3 for MOVE
        var isDestinationOverwrite = request.GetOverwrite();
        var depth = request.GetDepth();
        // TODO: IF Header
        var resourceTarget = await ResourceRepository.GetResourceAsync(new CaldavUri(destination!), httpContext, httpContext.RequestAborted);
        if (resourceTarget is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        if (resourceTarget.ResourceType == DavResourceType.Unknown)
        {
            resourceTarget.ResourceType = resourceSource.ResourceType;
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
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.NotFound, Precondition.MustExist, "The source must exist.");
            return;
        }
        if (resourceTarget.Exists == true && isDestinationOverwrite == false)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.PreconditionFailed, "Resource exists but no overwrite requested");
            // await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.CollectionMustExist, "The destination collection does not exist.");
            return;
        }
        if (resourceSource.Exists == true && resourceSource.ResourceType != resourceTarget.ResourceType)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Conflict);
            return;
        }
        var isCreate = !resourceTarget.Exists;
        switch (resourceSource.ResourceType)
        {
            case DavResourceType.Calendar:
            case DavResourceType.Addressbook:
            case DavResourceType.CalendarItem:
            case DavResourceType.AddressbookItem:
                // This is currently an intended limitation of this implementation
                // mostly due to lack of calendar client support
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden, "MOVE on this resource type is not supported");
                // // Preliminary support for MOVE
                // if (resourceSource.ParentResourceType != resourceTarget.ParentResourceType)
                // {
                //     // TODO: Implement proper error response message
                //     await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                //     return;
                // }
                // if (resourceSource.Object is null || resourceTarget.Parent is null)
                // {
                //     // TODO: Implement proper error response message
                //     await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                //     return;
                // }
                // await ItemRepository.MoveAsync(resourceSource.Object, resourceTarget.Parent, resourceTarget.DavName, httpContext.RequestAborted);
                // await WriteStatusAsync(httpContext, HttpStatusCode.Created);
                // return;
                return;

            case DavResourceType.BlobItem:
                {
                    var storage = httpContext.RequestServices.GetService<IDavStorage>();
                    if (storage is null)
                    {
                        await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden, "COPY on this resource type is not supported");
                        return;
                    }
                    if (resourceTarget.Parent is null)
                    {
                        // missing intermediate collections => Conflict
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.CollectionMustExist, "The destination collection must exist.");
                        return;
                    }
                    if (resourceSource.Object is null || resourceSource.Object.BlobItem is null)
                    {
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.NotFound, Precondition.MustExist, "The source object must exist.");
                        return;
                    }
                    var _ = await MoveRepository.PrepareMoveAsync(resourceSource.Object, resourceTarget.Object, storage, httpContext.RequestAborted);
                    if (!await storage.Prepare(httpContext.RequestAborted))
                    {
                        await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError, "Move in storage failed");
                        return;
                    }
                    await MoveRepository.CommitMoveAsync(resourceSource.Object, resourceTarget.Parent, resourceTarget.DavName, resourceTarget.Object, httpContext.RequestAborted);
                    if (!await storage.Commit(httpContext.RequestAborted))
                    {
                        Log.Fatal("Storage move failed during commit-phase. Storage potentially is now inconsistent.");
                        await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError, "Move in storage severely failed, inconsistent state possible");
                        return;
                    }
                    // source equal destination => Forbidden
                    SetLocation(response, resourceTarget.DavName);
                    await WriteStatusAsync(httpContext, isCreate ? HttpStatusCode.Created : HttpStatusCode.NoContent);
                }
                return;

            case DavResourceType.Container:
                {
                    var storage = httpContext.RequestServices.GetService<IDavStorage>();
                    if (storage is null)
                    {
                        await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden, "COPY on this resource type is not supported");
                        return;
                    }
                    if (resourceTarget.Parent is null)
                    {
                        // missing intermediate collections => Conflict
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.CollectionMustExist, "The destination collection must exist.");
                        return;
                    }
                    if (resourceSource.Current is null)
                    {
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.NotFound, Precondition.MustExist, "The source collection must exist.");
                        return;
                    }
                    var _ = await MoveRepository.PrepareMoveAsync(resourceSource.Current, UriUtils.ToFolderPath(resourceTarget.DavName), resourceTarget.Parent, resourceTarget.Current, storage, httpContext.RequestAborted);
                    if (!await storage.Prepare(httpContext.RequestAborted))
                    {
                        await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError, "Copy in storage failed");
                        return;
                    }
                    await MoveRepository.CommitMoveAsync(resourceSource.Current, resourceTarget.Current, httpContext.RequestAborted);
                    if (!await storage.Commit(httpContext.RequestAborted))
                    {
                        Log.Fatal("Storage copy failed during commit-phase. Storage potentially is now inconsistent.");
                        await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError, "Copy in storage severely failed, inconsistent state possible");
                        return;
                    }

                    SetLocation(response, resourceTarget.DavName);
                    await WriteStatusAsync(httpContext, isCreate ? HttpStatusCode.Created : HttpStatusCode.NoContent);
                }
                return;

            default:
            case DavResourceType.Unknown:
                await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                break;
        }
    }
}
