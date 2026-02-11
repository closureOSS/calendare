using System;

namespace Calendare.Server.Calendar;

public class TextMatch
{
    public bool NegateCondition { get; set; }
    public string? Value { get; set; }
    public string? Collation { get; set; }

    public Func<string, bool> Compile()
    {
        // TODO: Collation
        return (target) => NegateCondition ^ (Value is not null && target.Contains(Value, StringComparison.InvariantCultureIgnoreCase));
    }
}
