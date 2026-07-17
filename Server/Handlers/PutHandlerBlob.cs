using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Storage;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Handlers;

public partial class PutHandler : IMethodHandler
{
    // https://datatracker.ietf.org/doc/html/rfc4791#section-5.3.2
    private async Task AmendBlob(HttpContext httpContext, DavResource? resource)
    {
        var request = httpContext.Request;

        // the parent must exist
        // the resource type of the parent must be Container
        if (resource is null || resource.Parent is null || resource.ParentResourceType != DavResourceType.Container)
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-9.7.1
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.CollectionMustExist, "The destination collection does not exist.");
            return;
        }
        var storage = httpContext.RequestServices.GetService<IDavStorage>();
        if (storage is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }

        var resourceOriginal = resource.ToLight();
        var ifNoneMatch = request.GetIfNoneMatch();
        if (ifNoneMatch && resource.Object is not null)
        {
            Log.Error("URI {uri} is already mapped (If-None-Match)", resource.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfNoneMatch, "Existing resource matches 'If-None-Match' header - not accepted.");
            return;
        }
        var maxBodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySizeFeature is not null && !maxBodySizeFeature.IsReadOnly)
        {
            maxBodySizeFeature.MaxRequestBodySize = null;
        }
        var tempFile = await DownloadAsFileAsync(httpContext);
        if (tempFile is null || !tempFile.Exists)
        {
            // TODO: Check if error suitable for file create failed
            await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            return;
        }
        // TODO: Final location of file
        // TODO: Check if httpContext.Request.ContentLength != tempFile.Length?
        // TODO: Investigate support for X-OC-MTime and X-File-Mtime to adjust modification date
        var etag = await tempFile.PrettyMD5Hash(httpContext.RequestAborted);
        var target = resource.Object ?? new();
        target.OwnerId = resource.Owner.UserId;
        target.ActualUserId = resource.CurrentUser.UserId;
        target.Segment = resource.Uri.TrailingSegment!;
        target.Uri = resource.Uri.Path!;
        target.Uid ??= Guid.NewGuid().ToString();
        target.Etag = etag;
        target.RawData = string.Empty;
        target.VObjectType = "BLOB";
        target.BlobItem = new()
        {
            CollectionObject = target,
            Location = tempFile.FullName,
            ContentType = request.ContentType ?? "application/octet-stream",
            ContentLength = tempFile.Length,
            // TODO: Add marker for virus check
        };
        target.Collection ??= resource.Parent;
        var storageRequest = new StorageRequest
        {
            Operation = StorageOperation.Create,
            TargetLocation = tempFile.FullName,
            Location = await storage.GetFilename(target.Uri, httpContext.RequestAborted),
            ContentType = target.BlobItem.ContentType,
            ContentLength = target.BlobItem.ContentLength,
        };
        storage.Add(storageRequest);
        if (!await storage.CommitImmediately(httpContext.RequestAborted))
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            return;
        }
        target.BlobItem.Location = storageRequest.Location;
        var opCode = await VerifyOperation(httpContext, resource, resourceOriginal, target);
        if (opCode != DbOperationCode.Failure)
        {
            SetEtagHeader(httpContext.Response, target.Etag);
            await AmendCollectionObject(httpContext, opCode, target);
        }
    }

    private static async Task<FileInfo?> DownloadAsFileAsync(HttpContext httpContext)
    {
        try
        {
            // var outputPath = Path.GetTempFileName();
            var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.store");
            await using var fileStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920, // 80 KB buffer prevents LOH fragmentation
                useAsync: true);
            await httpContext.Request.Body.CopyToAsync(fileStream, httpContext.RequestAborted);
            return new FileInfo(outputPath);
        }
        catch (Exception e)
        {
            Log.Error(e, "Create temp file for upload failed");
            return null;
        }
    }
}

