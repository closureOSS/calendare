using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class CollectionCreateRequestMapper
{
    [MapperIgnoreTarget(nameof(Collection.Id))]
    [MapperIgnoreTarget(nameof(Collection.OwnerId))]
    [MapperIgnoreTarget(nameof(Collection.Owner))]
    [MapperIgnoreTarget(nameof(Collection.Created))]
    [MapperIgnoreTarget(nameof(Collection.Modified))]
    [MapperIgnoreTarget(nameof(Collection.Etag))]
    [MapperIgnoreTarget(nameof(Collection.ParentContainerUri))]
    private static partial Collection Map(CollectionCreateRequest source);

    public static Collection ToDto(this CollectionCreateRequest source)
    {
        var target = Map(source);
        return target;
    }

    public static List<Collection> ToDto(this IEnumerable<CollectionCreateRequest> source)
    {
        return [.. source.Select(x => x.ToDto())];
    }
}
