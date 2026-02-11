using Calendare.Data.Models;
using Riok.Mapperly.Abstractions;

namespace Calendare.Server.Recorder;

[Mapper(UseDeepCloning = false, EnumMappingIgnoreCase = true)]
public static partial class RecorderMapper
{
    [MapperIgnoreTarget(nameof(TrxJournal.Id))]
    [MapperIgnoreTarget(nameof(TrxJournal.Created))]
    // [MapperIgnoreSource(nameof(RecorderSession.RequestLeader))]
    // [MapperIgnoreSource(nameof(RecorderSession.ResponseStatus))]
    private static partial TrxJournal Map(RecorderSession source);

    public static TrxJournal ToDto(this RecorderSession source)
    {
        var target = Map(source);
        return target;
    }
}
