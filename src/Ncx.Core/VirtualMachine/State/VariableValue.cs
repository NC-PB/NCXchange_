using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The value of a variable of a channel (virtual machine 2.7): a number or a string as VAR, ARG or the vars file gave
/// it (language 4.9), or UNKNOWN. UNKNOWN is a value like the others: in STATIC mode a variable set from an expression
/// holds it (virtual machine 1), and a SYS_ name whose state is unknown reads it (virtual machine 2.7).
/// </summary>
public sealed record VariableValue
{
    private VariableValue(Value? value)
    {
        Value = value;
    }

    /// <summary>
    /// UNKNOWN: the value of a variable set from an expression in STATIC mode, and of a SYS_ name whose state is
    /// unknown (virtual machine 1, 2.7).
    /// </summary>
    public static VariableValue Unknown { get; } = new(value: null);

    /// <summary>
    /// The number or string the variable holds, an IntegerValue, a DecimalValue or a StringValue kept as written; null
    /// for UNKNOWN.
    /// </summary>
    public Value? Value { get; }

    /// <summary>
    /// True for UNKNOWN.
    /// </summary>
    public bool IsUnknown => Value is null;

    /// <summary>
    /// A known value: a number or a string, the values a variable takes (language 4.9).
    /// </summary>
    /// <param name="value">An IntegerValue, a DecimalValue or a StringValue.</param>
    public static VariableValue Of(Value value)
    {
        // A variable holds a number or a string (language 4.9). An expression is evaluated, or UNKNOWN, before its
        // value gets here, and no other value type is the value of a variable.
        if (value is not (IntegerValue or DecimalValue or StringValue))
        {
            throw new ArgumentException(
                $"A variable holds a number or a string, not {value.ToCanonical()} (language 4.9).", nameof(value));
        }

        return new VariableValue(value);
    }

    /// <summary>
    /// The value as NCX writes it, or UNKNOWN.
    /// </summary>
    public override string ToString()
    {
        return Value is null ? "UNKNOWN" : Value.ToCanonical();
    }
}
