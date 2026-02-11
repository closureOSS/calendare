using System.Net;
using System.Text;
using System.Threading.Tasks;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;


public partial class GetHandler : HandlerBase, IMethodHandler
{
    private async Task GetAddressbookItem(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        var response = httpContext.Response;
        if (resource.Object is not null && resource.Object.RawData is not null)
        {
            SetEtagHeader(response, resource.Object.Etag);
            response.ContentType = $"{MimeContentTypes.VCard}; {MimeContentTypes.Utf8}";
            response.StatusCode = (int)HttpStatusCode.OK;
            if (isHeadRequest == false)
            {
                response.ContentLength = Encoding.UTF8.GetByteCount(resource.Object.RawData);
                await response.WriteAsync(resource.Object.RawData, httpContext.RequestAborted);
                Recorder.SetResponseBody(resource.Object.RawData);
            }
        }
        else
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
        }
    }

    private async Task GetAddressbook(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        var response = httpContext.Response;
        // var confidentialMode = resource.CurrentUser.Id != resource.Owner.Id;
        if (resource.Exists == true && resource.Current is not null)
        {
            var collectionObjects = await ItemRepository.ListCollectionObjectsAsync(resource.Current, httpContext.RequestAborted);
            StringBuilder sb = new();
            foreach (var co in collectionObjects)
            {
                sb.Append(co.RawData);
            }
            SetEtagHeader(response, resource.Object?.Etag);
            response.ContentType = $"{MimeContentTypes.VCard}; {MimeContentTypes.Utf8}";
            response.StatusCode = (int)HttpStatusCode.OK;
            if (isHeadRequest == false)
            {
                var serializedAddressbook = sb.ToString();
                response.ContentLength = Encoding.UTF8.GetByteCount(serializedAddressbook);
                await response.WriteAsync(serializedAddressbook, httpContext.RequestAborted);
                Recorder.SetResponseBody(serializedAddressbook);
            }
        }
        else
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest); // Or NotFound??
        }
    }
}
