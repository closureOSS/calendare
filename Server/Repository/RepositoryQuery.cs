using Calendare.Server.Models;

namespace Calendare.Server.Repository;

public abstract class RepositoryQuery
{
    public required Principal CurrentUser { get; init; }
    public bool IsTracking { get; set; }
}
