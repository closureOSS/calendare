using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Storage;

public interface IDavStorage
{
    Task<string> GetFilename(string uri, CancellationToken ct);

    void AddRange(IEnumerable<StorageRequest> requests);
    void Add(params StorageRequest[] requests);

    Task<bool> Commit(CancellationToken ct);
    Task<bool> Prepare(CancellationToken ct);
    Task<bool> CommitImmediately(CancellationToken ct);
    Task<bool> Rollback(CancellationToken ct);

    Task<int> Cleanup(HashSet<string> locations, CancellationToken ct);

    ReadOnlyCollection<StorageResponse> Results { get; }

    Task GetAsync(Data.Models.ObjectBlob item, HttpResponse httpResponse, CancellationToken ct);
}
