using Calendare.Data.Models;
using NodaTime;

namespace Calendare.Server.Api;

public class CollectionResponse
{
    public required int Id { get; set; }
    public required string Uri { get; set; }
    public string? OwnerUsername { get; set; }
    public string? DisplayName { get; set; }
    public CollectionType CollectionType { get; set; } = CollectionType.Collection;
    public CollectionSubType CollectionSubType { get; set; } = CollectionSubType.Default;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? ScheduleTransparency { get; set; }
    public bool ExcludeFreeBusy { get; set; }
    public string? Timezone { get; set; }
    public string? ParentContainerUri { get; set; }
    public bool PublicEventsOnly { get; set; }
    public bool PubliclyReadable { get; set; }
    public bool IsDefault { get; set; }
    public bool IsTechnical { get; set; }
    public PrivilegeMask? Permissions { get; set; }
    public PrivilegeMask? OwnerProhibit { get; set; }
    public PrivilegeMask? AuthorizedProhibit { get; set; }
    public PrivilegeMask? GlobalPermitSelf { get; set; }

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}
