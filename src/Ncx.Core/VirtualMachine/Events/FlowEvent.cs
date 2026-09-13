using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// JUMP, CALL, RETURN and REPEAT (virtual machine 7): raised at every flow word with its target, its condition and the
/// call depth. STATIC mode records JUMP, REPEAT and RETURN and follows CALL (virtual machine 1, D99).
/// </summary>
public sealed record FlowEvent : VmEvent
{
    /// <summary>
    /// JUMP, CALL, RETURN or REPEAT.
    /// </summary>
    public required FlowKind Flow { get; init; }

    /// <summary>
    /// The label of JUMP and REPEAT (END for JUMP=END), the subprogram or the external program of CALL; null for
    /// RETURN.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// The condition of IF in the same block as written; null for a flow word without IF. STATIC mode does not
    /// evaluate it (virtual machine 1).
    /// </summary>
    public Value? Condition { get; init; }

    /// <summary>
    /// The call depth of the block: 0 in a program, 1 in a subprogram a program called (virtual machine 2.7).
    /// </summary>
    public required int Depth { get; init; }

    /// <inheritdoc/>
    public override string Kind => Flow switch
    {
        FlowKind.Jump => "JUMP",
        FlowKind.Call => "CALL",
        FlowKind.Return => "RETURN",
        _ => "REPEAT",
    };

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        var parts = new List<string>();
        if (Target is not null)
        {
            parts.Add("target " + Target);
        }

        if (Condition is not null)
        {
            parts.Add("condition " + Condition.ToCanonical());
        }

        parts.Add("depth " + Depth.ToString(CultureInfo.InvariantCulture));
        return string.Join(", ", parts);
    }
}
