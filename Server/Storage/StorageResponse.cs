using System.Net;
using NodaTime;

namespace Calendare.Server.Storage;

public sealed class StorageResponse
{
    public required StorageRequest Request { get; init; }
    public Instant? LastOperationDate { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
    public bool IsSuccess
    {
        get
        {
            if (StatusCode is null) return false;
            return Request.Operation switch
            {
                StorageOperation.Copy or StorageOperation.Create => StatusCode == HttpStatusCode.Created || StatusCode == HttpStatusCode.NoContent,
                StorageOperation.Delete => StatusCode == HttpStatusCode.NotFound || StatusCode == HttpStatusCode.Gone || StatusCode == HttpStatusCode.NoContent,
                StorageOperation.Move => StatusCode == HttpStatusCode.NoContent,
                _ => false,
            };
        }
    }
    public string? Message { get; set; }
}
