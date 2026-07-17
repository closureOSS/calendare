using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;

namespace Calendare.Server.Storage;

sealed class S3Storage(IOptions<S3StorageOptions> Options) : IDavStorage
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
        var s3path = UriUtils.ToPath(["webdav", $"{Guid.CreateVersion7()}"]);
        return Task.FromResult(s3path[1..]);    // the S3 name MUST NOT start with a '/'
    }

    public async Task<bool> Prepare(CancellationToken ct)
    {
        var hasFailure = false;
        foreach (var req in Queue)
        {
            if (req.IsSuccess) continue;
            req.LastOperationDate = SystemClock.Instance.GetCurrentInstant();
            switch (req.Request.Operation)
            {
                case StorageOperation.Create:
                    await UploadAsync(req, ct);
                    break;

                case StorageOperation.Delete:
                    continue;   // delete is done in commit phase

                case StorageOperation.Move:
                    req.StatusCode = System.Net.HttpStatusCode.NoContent;   // nothing to do in this storage implementation
                    break;

                case StorageOperation.Copy:
                    await CopyAsync(req, ct);
                    break;
            }
            if (!req.IsSuccess)
            {
                hasFailure = true;
            }
        }
        return !hasFailure;
    }

    public async Task<bool> Commit(CancellationToken ct)
    {
        // var hasFailure = false;
        try
        {
            var deleteKeys = Queue.Where(req => !req.IsSuccess && req.Request.Operation == StorageOperation.Delete && req.Request.Location is not null).Select(req => req.Request.Location!);
            await DeleteAsync(deleteKeys, ct);
            // TODO: Check logic with bulk deletion...
            Queue
                .Where(req => !req.IsSuccess && req.Request.Operation == StorageOperation.Delete && req.Request.Location is not null)
                .ToList()
                .ForEach(req => req.StatusCode = System.Net.HttpStatusCode.Gone);
        }
        catch (Exception e)
        {
            Log.Error(e, "Delete files failed");
            return false;
        }
        return true;
        // foreach (var req in Queue)
        // {
        //     if (req.IsSuccess) continue;
        //     req.LastOperationDate = SystemClock.Instance.GetCurrentInstant();
        //     switch (req.Request.Operation)
        //     {
        //         case StorageOperation.Delete:
        //             req.StatusCode = System.Net.HttpStatusCode.Gone;
        //             break;

        //         default:
        //             req.StatusCode ??= System.Net.HttpStatusCode.NotImplemented; // not supported here
        //             break;
        //     }
        //     if (!req.IsSuccess)
        //     {
        //         hasFailure = true;
        //     }
        // }
        // return !hasFailure;
    }

    public async Task<bool> CommitImmediately(CancellationToken ct)
    {
        return await Prepare(ct) && await Commit(ct);
    }

    public Task<bool> Rollback(CancellationToken ct)
    {
        return Task.FromResult(true);   // nothing to do, cleanup during garbage collection
    }

    public async Task<int> Cleanup(HashSet<string> locations, CancellationToken ct)
    {
        var s3Client = CreateS3Client() ?? throw new InvalidOperationException("S3 access failed");
        var args = new ListObjectsV2Request()
        {
            BucketName = Options.Value.Bucket,
        };
        int deleteCount = 0;
        List<string> todelete = [];
        ListObjectsV2Response response;
        do
        {
            response = await s3Client.ListObjectsV2Async(args, ct);
            foreach (var item in response.S3Objects ?? [])
            {
                if (!locations.Contains(item.Key))
                {
                    todelete.Add(item.Key);
                }
                if (todelete.Count > 900)
                {
                    await DeleteAsync(todelete, ct);
                    deleteCount += todelete.Count;
                    todelete.Clear();
                }
            }
            args.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated is true);
        if (todelete.Count > 0)
        {
            await DeleteAsync(todelete, ct);
            deleteCount += todelete.Count;
            todelete.Clear();
        }
        return deleteCount;
    }

    private AmazonS3Client? S3Client = null;
    private AmazonS3Client CreateS3Client()
    {
        if (S3Client is null)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = Options.Value.Host,
                ForcePathStyle = Options.Value.PathStyle,
            };
            if (!string.IsNullOrEmpty(Options.Value.Region))
            {
                config.AuthenticationRegion = Options.Value.Region;
            }
            S3Client = new AmazonS3Client(Options.Value.AccessKey, Options.Value.SecretKey, config);
        }
        return S3Client;
    }

    private async Task UploadAsync(StorageResponse item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.Request.Location) || string.IsNullOrEmpty(item.Request.TargetLocation))
        {
            item.StatusCode = System.Net.HttpStatusCode.BadRequest;
            return;
        }
        var tempFile = new FileInfo(item.Request.TargetLocation);
        if (!tempFile.Exists)
        {
            Log.Error("Upload requested but temporary file {file} is missing", item.Request.TargetLocation);
            item.StatusCode = System.Net.HttpStatusCode.NotFound;
            return;
        }
        try
        {
            var args = new PutObjectRequest
            {
                BucketName = Options.Value.Bucket,
                Key = item.Request.Location,
                FilePath = tempFile.FullName,
            };
            var s3Client = CreateS3Client() ?? throw new InvalidOperationException("S3 access failed");
            var result = await s3Client.PutObjectAsync(args, ct);
            item.StatusCode = System.Net.HttpStatusCode.Created;
        }
        catch (AmazonS3Exception e)
        {
            if (e.ErrorCode.Equals("PreconditionFailed", StringComparison.OrdinalIgnoreCase))
            {
                item.StatusCode = System.Net.HttpStatusCode.PreconditionFailed;
                Log.Information("{objectname}: exists already, skipping upload", item.Request.Location);
                return;
            }
            item.StatusCode = System.Net.HttpStatusCode.BadRequest;
            Log.Error(e, "{objectname}: Upload failed, {error}", item.Request.Location, e.Message);
        }
        catch (Exception e)
        {
            item.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            Log.Error(e, "{objectname}: Upload failed, {error}", item.Request.Location, e.Message);
        }
    }

    private async Task CopyAsync(StorageResponse item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.Request.Location) || string.IsNullOrEmpty(item.Request.TargetLocation))
        {
            item.StatusCode = System.Net.HttpStatusCode.BadRequest;
            return;
        }
        var args = new CopyObjectRequest
        {
            SourceBucket = Options.Value.Bucket,
            SourceKey = item.Request.Location,
            DestinationKey = item.Request.TargetLocation,
            DestinationBucket = Options.Value.Bucket,
        };
        try
        {
            var s3Client = CreateS3Client() ?? throw new InvalidOperationException("S3 access failed");
            var response = await s3Client.CopyObjectAsync(args, ct);
            item.StatusCode = System.Net.HttpStatusCode.Created;
        }
        catch (Exception e)
        {
            item.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            Log.Error("Copy file {file} to {targetFile} failed: {errorMsg}", item.Request.Location, item.Request.TargetLocation, e.Message);
        }
    }

    private async Task DeleteAsync(IEnumerable<string> objectNames, CancellationToken ct)
    {
        var keys = new List<KeyVersion>();
        foreach (var fn in objectNames)
        {
            keys.Add(new KeyVersion { Key = fn });
        }
        var args = new DeleteObjectsRequest
        {
            BucketName = Options.Value.Bucket,
            Objects = keys,
        };
        try
        {
            var s3Client = CreateS3Client() ?? throw new InvalidOperationException("S3 access failed");

            var response = await s3Client.DeleteObjectsAsync(args, ct);
        }
        catch (DeleteObjectsException e)
        {
            DeleteObjectsResponse errorResponse = e.Response;
            // TODO: Error message
            // Console.WriteLine("x {0}", errorResponse.DeletedObjects.Count);

            // Console.WriteLine($"Successfully deleted {errorResponse.DeletedObjects.Count}.");
            // Console.WriteLine($"No. of objects failed to delete = {errorResponse.DeleteErrors.Count}");

            // Console.WriteLine("Printing error data...");
            // foreach (DeleteError deleteError in errorResponse.DeleteErrors)
            // {
            //     Console.WriteLine($"Object Key: {deleteError.Key}\t{deleteError.Code}\t{deleteError.Message}");
            // }
            Log.Error(e, "Delete failed, {error}", e.Message);
        }
    }

    public async Task GetAsync(Data.Models.ObjectBlob item, HttpResponse response, CancellationToken ct)
    {
        response.ContentLength = item.ContentLength;
        var args = new GetObjectRequest
        {
            BucketName = Options.Value.Bucket,
            Key = item.Location,
        };
        try
        {
            var s3Client = CreateS3Client() ?? throw new InvalidOperationException("S3 access failed");
            using var s3request = await s3Client.GetObjectAsync(args, ct);
            await s3request.ResponseStream.CopyToAsync(response.Body, ct);
        }
        catch (AmazonS3Exception e)
        {
            Log.Error("{objectname}: Download failed, {error}", item.Location, e.Message);

        }
    }
}
