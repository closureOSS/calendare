using System;
using System.Linq;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Properties;

namespace Calendare.Server.Calendar;

public static class ComponentConversionExtensions
{
    public static VEvent ToConfidential(this VEvent vevent)
    {
        var target = new VEvent { Builder = vevent.Builder, };
        var safeProperties = new string[] {
            PropertyName.Class,
            PropertyName.DateStart, PropertyName.DateEnd, PropertyName.Duration,
            PropertyName.Uid, PropertyName.Sequence,
            PropertyName.Created, PropertyName.DateStamp,
            PropertyName.RecurrenceRule, PropertyName.RecurrenceId, PropertyName.RecurrenceDate,
            PropertyName.RecurrenceExceptionDate, PropertyName.RecurrenceExceptionRule,
        };
        target.Properties.AddRange(vevent.Properties.
            Where(x => safeProperties.Contains(x.Name, StringComparer.Ordinal)).
            Select(x => x.DeepClone()));
        target.Summary.Set("Busy");
        return target;
    }
}
