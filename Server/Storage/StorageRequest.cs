namespace Calendare.Server.Storage;

public sealed class StorageRequest
{
    public required StorageOperation Operation { get; init; }

    /// <summary>Reference object BLOB</summary>
    public int? ObjectBlobId { get; set; }

    /// <summary>Current physical location path</summary>
    public string? Location { get; set; }

    /// <summary>Target physical location path. For create operation physical temporary file location</summary>
    public string? TargetLocation { get; set; }

    /// <summary>Mimetype (Optional, depends on support by the storage)</summary>
    public string? ContentType { get; set; }

    /// <summary>File size (Optional, depends on support by the storage)</summary>
    public long? ContentLength { get; set; }
}
