using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;

namespace Calendare.Server.Storage;

sealed class FileStorage(IOptions<FileStorageOptions> Options) : IDavStorage
{
    private readonly List<StorageResponse> Queue = [];
    public ReadOnlyCollection<StorageResponse> Results { get { return Queue.AsReadOnly(); } }

    public void AddRange(IEnumerable<StorageRequest> requests)
    {
        Queue.AddRange(requests.Select(r => new StorageResponse { Request = r, }));
    }

    public void Add(params StorageRequest[] requests)
    {
        Queue.AddRange(requests.Select(r => new StorageResponse { Request = r, }));
    }

    public Task<string> GetFilename(string uri, CancellationToken _)
    {
        var basePath = new DirectoryInfo(Options.Value.BasePath);
        var level1 = CharacterGenerator.GetRandomChar();
        var level2 = CharacterGenerator.GetRandomChar();
        var filePath = new FileInfo(Path.Combine(basePath.FullName, $"{level1}", $"{level2}", $"{Guid.CreateVersion7()}.store"));
        Directory.CreateDirectory(filePath.DirectoryName!);
        return Task.FromResult(filePath.FullName);
    }

    public Task<bool> Prepare(CancellationToken _)
    {
        var hasFailure = false;
        foreach (var req in Queue)
        {
            if (req.IsSuccess) continue;
            req.LastOperationDate = SystemClock.Instance.GetCurrentInstant();
            switch (req.Request.Operation)
            {
                case StorageOperation.Create:
                    UploadFile(req);
                    break;

                case StorageOperation.Delete:
                    continue;   // delete is done in commit phase

                case StorageOperation.Move:
                    req.StatusCode = System.Net.HttpStatusCode.NoContent;   // nothing to do in this storage implementation
                    break;

                case StorageOperation.Copy:
                    CopyFile(req);
                    break;
            }
            if (!req.IsSuccess)
            {
                hasFailure = true;
            }
        }
        return Task.FromResult(!hasFailure);
    }

    public Task<bool> Commit(CancellationToken _)
    {
        var hasFailure = false;
        foreach (var req in Queue)
        {
            if (req.IsSuccess) continue;
            req.LastOperationDate = SystemClock.Instance.GetCurrentInstant();
            switch (req.Request.Operation)
            {
                case StorageOperation.Delete:
                    DeleteFile(req);
                    break;

                default:
                    req.StatusCode ??= System.Net.HttpStatusCode.NotImplemented; // not supported here
                    break;
            }
            if (!req.IsSuccess)
            {
                hasFailure = true;
            }
        }
        return Task.FromResult(!hasFailure);
    }

    public async Task<bool> CommitImmediately(CancellationToken ct)
    {
        return await Prepare(ct) && await Commit(ct);
    }

    public Task<bool> Rollback(CancellationToken _)
    {
        return Task.FromResult(true);   // nothing to do, cleanup during garbage collection
    }

    public Task<int> Cleanup(HashSet<string> locations, CancellationToken _)
    {
        var root = new DirectoryInfo(Options.Value.BasePath);
        CleanupDirectory(locations, root);
        return Task.FromResult(0); // TODO: Implement
    }

    private static int CleanupDirectory(HashSet<string> locations, DirectoryInfo directory, int deleteCount = 0)
    {
        int deleted = deleteCount;
        foreach (var subdir in directory.EnumerateDirectories())
        {
            deleted += CleanupDirectory(locations, subdir, deleted);
        }
        foreach (var file in directory.EnumerateFiles())
        {
            if (!locations.Contains(file.FullName))
            {
                File.Delete(file.FullName);
                ++deleted;
            }
        }
        return deleted;
    }

    private static void UploadFile(StorageResponse item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.Request.Location) || string.IsNullOrEmpty(item.Request.TargetLocation))
            {
                item.StatusCode = System.Net.HttpStatusCode.BadRequest;
                return;
            }
            var tempFile = new FileInfo(item.Request.TargetLocation);
            if (!tempFile.Exists)
            {
                Log.Error("Upload requested but temporary file {file} is missing", item.Request.Location);
                item.StatusCode = System.Net.HttpStatusCode.NotFound;
                return;
            }
            tempFile.MoveTo(item.Request.Location, overwrite: true);
            item.StatusCode = System.Net.HttpStatusCode.Created;
        }
        catch (Exception e)
        {
            item.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            Log.Error("Upload file {file} from temporary file {targetFile} failed: {errorMsg}", item.Request.Location, item.Request.TargetLocation, e.Message);
        }
    }

    private static void CopyFile(StorageResponse item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.Request.Location) || string.IsNullOrEmpty(item.Request.TargetLocation))
            {
                item.StatusCode = System.Net.HttpStatusCode.BadRequest;
                return;
            }
            var sourceFile = new FileInfo(item.Request.Location);
            if (!sourceFile.Exists)
            {
                Log.Error("Copy requested but file {file} is missing", item.Request.Location);
                item.StatusCode = System.Net.HttpStatusCode.NotFound;
                return;
            }
            var targetFile = sourceFile.CopyTo(item.Request.TargetLocation, overwrite: true);
            item.StatusCode = targetFile.Exists ? System.Net.HttpStatusCode.Created : System.Net.HttpStatusCode.Processing;
        }
        catch (Exception e)
        {
            item.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            Log.Error("Copy file {file} to {targetFile} failed: {errorMsg}", item.Request.Location, item.Request.TargetLocation, e.Message);
        }
    }

    private static void DeleteFile(StorageResponse item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.Request.Location))
            {
                item.StatusCode = System.Net.HttpStatusCode.BadRequest;
                return;
            }
            var file = new FileInfo(item.Request.Location);
            if (!file.Exists)
            {
                item.StatusCode = System.Net.HttpStatusCode.NotFound;
                return;
            }
            file.Delete();
            // file.MoveTo($"{file.FullName}#del");
            item.StatusCode = System.Net.HttpStatusCode.Gone;
        }
        catch (Exception e)
        {
            item.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            Log.Error("Delete file {file} failed: {errorMsg}", item.Request.Location, e.Message);
        }
    }

    public async Task GetAsync(Data.Models.ObjectBlob item, HttpResponse response, CancellationToken ct)
    {
        response.ContentLength = item.ContentLength;
        await using FileStream stream = new FileStream(item.Location, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await stream.CopyToAsync(response.Body, cancellationToken: ct);
    }

}
