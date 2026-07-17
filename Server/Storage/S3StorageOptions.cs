namespace Calendare.Server.Storage;

public class S3StorageOptions
{
    public string? Bucket { get; set; }
    public string? Host { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Region { get; set; }
    public bool PathStyle { get; set; } = false;
}
