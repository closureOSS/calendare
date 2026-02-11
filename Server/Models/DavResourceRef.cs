namespace Calendare.Server.Models;

public class DavResourceRef
{
    public required string DavName { get; set; }
    public string? DavEtag { get; set; }
    public string? ScheduleTag { get; set; }
    public bool Exists { get; set; }
}
