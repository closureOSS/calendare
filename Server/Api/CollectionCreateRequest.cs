using Calendare.Data.Models;

namespace Calendare.Server.Api;

public class CollectionCreateRequest
{
    public required string Uri { get; set; }
    public string? DisplayName { get; set; }
    public CollectionType CollectionType { get; set; } = CollectionType.Collection;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? ScheduleTransparency { get; set; }
    public string? Timezone { get; set; }
    public bool PublicEventsOnly { get; set; }
    public bool PubliclyReadable { get; set; }
}
