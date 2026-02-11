using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Api;

[Mapper(UseDeepCloning = true, EnumMappingIgnoreCase = true, RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class AddressbookItemMapper
{
    [MapperIgnoreTarget(nameof(AddressbookItem.FormattedName))]
    [MapperIgnoreTarget(nameof(AddressbookItem.Name))]
    [MapperIgnoreTarget(nameof(AddressbookItem.NickName))]
    private static partial AddressbookItem Map(CollectionObject source);

    private static partial void MapInto(ObjectAddress source, AddressbookItem target);

    public static AddressbookItem ToAddressbookView(this SyncJournal source)
    {
        if (source.CollectionObject is null || source.CollectionObject.AddressItem is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        var target = Map(source.CollectionObject);
        MapInto(source.CollectionObject.AddressItem, target);
        return target;
    }

    public static AddressbookItem ToAddressbookView(this CollectionObject source)
    {
        if (source is null || source.AddressItem is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        var target = Map(source);
        MapInto(source.AddressItem, target);
        return target;
    }

    public static List<AddressbookItem> ToAddressbookView(this IEnumerable<SyncJournal> source)
    {
        return [.. source.Select(x => x.ToAddressbookView())];
    }
}
