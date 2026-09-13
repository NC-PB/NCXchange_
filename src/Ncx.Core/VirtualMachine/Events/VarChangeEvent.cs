using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// VAR_CHANGE (virtual machine 7): raised at every VAR, and at every ARG of a CALL that enters a subprogram, with the
/// name, the old and the new value (2.7, 3.6).
/// </summary>
public sealed record VarChangeEvent : VmEvent
{
    /// <summary>
    /// The variable, Q1, V1 (language 4.9).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value before; null while the variable was unassigned, which a local V1 to V33 of the callee is when an ARG
    /// gives it its value.
    /// </summary>
    public VariableValue? OldValue { get; init; }

    /// <summary>
    /// The value assigned: a number, a string, or UNKNOWN from an expression in STATIC mode (virtual machine 1).
    /// </summary>
    public required VariableValue NewValue { get; init; }

    /// <inheritdoc/>
    public override string Kind => "VAR_CHANGE";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        string oldValue = OldValue is null ? EventText.Unknown : OldValue.ToString();
        return $"{Name} {oldValue} -> {NewValue}";
    }
}
