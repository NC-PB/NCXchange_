using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// CYCLE_CALL (virtual machine 3.3, 7): raised at every CYCLE_CALL with the cycle name and parameters and the call
/// point; for a CYCLE:controller=n cycle the parameters are the native words (D94).
/// </summary>
public sealed record CycleCallEvent : VmEvent
{
    /// <summary>
    /// cycle.name: DRILL, a catalog name, or the native number of CYCLE:controller=n (virtual machine 2.6).
    /// </summary>
    public required string Cycle { get; init; }

    /// <summary>
    /// The controller of a native cycle, HEIDENHAIN of CYCLE:HEIDENHAIN=251; null for a built-in or catalog cycle
    /// (D94).
    /// </summary>
    public string? Controller { get; init; }

    /// <summary>
    /// cycle.parameters: the parameter words of the CYCLE block, the native ones in source order (virtual machine 2.6,
    /// D94).
    /// </summary>
    public required IReadOnlyList<Word> Parameters { get; init; }

    /// <summary>
    /// The position of every axis after the call: the plane axes at the call point, the drilling axis at the retract
    /// plane, unknown after a cycle whose sequence the virtual machine does not know (virtual machine 3.3, D94).
    /// </summary>
    public required IReadOnlyDictionary<string, AxisPosition> At { get; init; }

    /// <inheritdoc/>
    public override string Kind => "CYCLE_CALL";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        string cycle = Controller is null ? Cycle : Cycle + " (" + Controller + ")";
        return $"cycle {cycle}, at {EventText.KnownPositions(At)}, parameters {EventText.Words(Parameters)}";
    }
}
