using System.Collections.Generic;
using Calendare.Data.Models;

namespace Calendare.Server.Repository;

public class CollectionReadQuery : RepositoryQuery
{
    public required string OwnerUsername { get; set; }
    public bool IncludeAllCollectionTypes { get; set; }
    public List<CollectionType>? CollectionTypes { get; set; }
    public List<CollectionSubType>? CollectionSubTypes { get; set; }
}
