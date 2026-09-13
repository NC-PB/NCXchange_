namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// STATE_CHANGE (virtual machine 7): one per modal state variable a block changed, with the old and the new value; the
/// rows of trace (6) and of the datum, feed and speed lists (8).
/// </summary>
public sealed record StateChangeEvent : VmEvent
{
    /// <summary>
    /// The state variable, named by the key of the word that sets it with the resource or the axis it stands on as its
    /// address (the state keys of virtual machine 3.10): X, F, UNITS, SPINDLE:S1, RPM:S1, TOOL:H1, OFFSET:LEN:H1,
    /// COOLANT:STANDARD, FUNC:SUB_CHUCK, SETPOS:C; CHAIN for the transform chain (2.1).
    /// </summary>
    public required string Variable { get; init; }

    /// <summary>
    /// The value before the block as NCX writes it, CW, 1500, 0 (MACHINE); empty when it was unknown or none (virtual
    /// machine 6).
    /// </summary>
    public required string OldValue { get; init; }

    /// <summary>
    /// The value after the block as NCX writes it; empty when it is unknown or none.
    /// </summary>
    public required string NewValue { get; init; }

    /// <inheritdoc/>
    public override string Kind => "STATE_CHANGE";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return $"{Variable} {EventText.Shown(OldValue)} -> {EventText.Shown(NewValue)}";
    }
}
