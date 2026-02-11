using System;

namespace Calendare.Server.Addressbook;

public class TextMatch
{
    public required string MatchType { get; set; }
    public bool NegateCondition { get; set; }
    public string? Value { get; set; }
    public string? Collation { get; set; }

    public Func<string, bool> Compile()
    {
        // TODO: Collation
        return MatchType switch
        {
            "equals" => (target) => NegateCondition ^ (Value is not null && target.Equals(Value, StringComparison.InvariantCultureIgnoreCase)),
            "starts-with" => (target) => NegateCondition ^ (Value is not null && target.StartsWith(Value, StringComparison.InvariantCultureIgnoreCase)),
            "ends-with" => (target) => NegateCondition ^ (Value is not null && target.EndsWith(Value, StringComparison.InvariantCultureIgnoreCase)),
            _ => (target) => NegateCondition ^ (Value is not null && target.Contains(Value, StringComparison.InvariantCultureIgnoreCase)),
        };
    }
}
