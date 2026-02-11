using System.Collections.Generic;
using System.Linq;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Models;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class DavResourceRefMapper
{
    private static partial DavResourceRef Map(DavResource source);

    public static DavResourceRef ToLight(this DavResource source)
    {
        var target = Map(source);
        return target;
    }

    public static List<DavResourceRef> ToView(this IEnumerable<DavResource> source)
    {
        return [.. source.Select(x => x.ToLight())];
    }
}
