namespace Calendare.Server.Api;

public class CollectionAmendRequest
{
    public string? Uri { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool? ExcludeFreeBusy { get; set; }
    public string? Timezone { get; set; }
}
