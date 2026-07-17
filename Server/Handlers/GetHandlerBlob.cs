using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Models;
using Calendare.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Handlers;

public partial class GetHandler : HandlerBase, IMethodHandler
{
    private async Task GetBlobItem(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        if (resource.Object is null || resource.Object.BlobItem is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        var storage = httpContext.RequestServices.GetService<IDavStorage>();
        if (storage is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        var response = httpContext.Response;
        SetEtagHeader(response, resource.Object.Etag);
        response.ContentType = resource.Object.BlobItem.ContentType;
        response.StatusCode = (int)HttpStatusCode.OK;
        if (isHeadRequest == false)
        {
            await storage.GetAsync(resource.Object.BlobItem, response, httpContext.RequestAborted);
        }
    }

    private async Task GetCollectionIndex(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        if (resource.Exists == false || resource.Current is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest); // Or NotFound??
            return;
        }
        var response = httpContext.Response;
    }
}
