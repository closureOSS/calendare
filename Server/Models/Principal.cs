using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using NodaTime;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Models;

public class Principal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public Instant? EmailOk { get; set; }
    public string? Etag { get; set; }
    public string? DisplayName { get; set; }
    public string? Timezone { get; set; }
    public string? Description { get; set; } = "";
    public string? DateFormatType { get; set; }
    public string? Locale { get; set; }
    public string? Color { get; set; }
    public int? OrderBy { get; set; }
    public string Uri { get; set; } = default!;
    public Guid PermanentId { get; set; }
    public CollectionType CollectionType => CollectionType.Principal;
    public CollectionSubType CollectionSubType { get; set; } = CollectionSubType.Default;

    public PrincipalType PrincipalType { get; set; } = null!;
    public int PrincipalTypeId { get; set; }
    public PrivilegeMask GlobalPermitSelf { get; set; } = PrivilegeMask.None;
    public PrivilegeMask GlobalPermit { get; set; } = PrivilegeMask.None;
    public PrivilegeMask AuthorizedProhibit { get; set; } = PrivilegeMask.None;
    public PrivilegeMask AuthorizedMask { get; set; } = PrivilegeMask.All;
    public PrivilegeMask OwnerProhibit { get; set; } = PrivilegeMask.None;
    public PrivilegeMask OwnerMask { get; set; } = PrivilegeMask.All;

    public ICollection<CollectionProperty> Properties { get; } = new List<CollectionProperty>();

    public List<GrantRelation> Permissions { get; set; } = [];

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}



[Mapper(UseDeepCloning = false, EnumMappingIgnoreCase = true)]
public static partial class PrincipalMapper
{
    [MapperIgnoreSource(nameof(Collection.Parent))]
    [MapperIgnoreSource(nameof(Collection.ParentId))]
    [MapperIgnoreSource(nameof(Collection.CollectionType))]
    [MapperIgnoreSource(nameof(Collection.ParentContainerUri))]
    [MapperIgnoreSource(nameof(Collection.ScheduleTransparency))]
    [MapperIgnoreSource(nameof(Collection.Objects))]
    [MapperIgnoreSource(nameof(Collection.Children))]
    [MapperIgnoreSource(nameof(Collection.Groups))]
    [MapperIgnoreSource(nameof(Collection.Members))]
    [MapperIgnoreSource(nameof(Collection.SyncJournal))]
    [MapProperty(nameof(Collection.OwnerId), nameof(Principal.UserId))]
    [MapProperty(nameof(Collection.Grants), nameof(Principal.Permissions))]
    [MapNestedProperties(nameof(Collection.Owner))]
    private static partial Principal Map(Collection source);

    public static Principal ToPrincipal(this Collection source)
    {
        var target = Map(source);
        return target;
    }

    public static List<Principal> ToPrincipal(this IEnumerable<Collection> source)
    {
        return [.. source.Select(x => x.ToPrincipal())];
    }
}
