using Calendare.Data.Models;

namespace Calendare.Server.Repository;

public class StatisticsCollectionResult
{
    public required string Username { get; set; }
    public required string Uri { get; set; }
    public string? DisplayName { get; set; }
    public CollectionType CollectionType { get; set; }
    public CollectionSubType CollectionSubType { get; set; }
    public string? PrincipalTypeName { get; set; }
    public int VEventCount { get; set; }
    public int VTodoCount { get; set; }
    public int VJournalCount { get; set; }
    public int VPollCount { get; set; }
    public int VCardCount { get; set; }
    public int VAvailabilityCount { get; set; }
    public int PropertyCount { get; set; }
}
