using System.Collections.Generic;
using NodaTime;

namespace Calendare.Server.Repository;

public class CalendarObjectQuery : RepositoryQuery
{
    public List<int>? CollectionIds { get; set; }
    public int? OwnerId { get; set; }
    public Interval? Period { get; set; }
    public bool ExcludePrivate { get; set; }
    public bool ExcludeTransparent { get; set; }
    public List<string>? VObjectTypes { get; set; }
}
