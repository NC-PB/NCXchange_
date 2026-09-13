using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// One motion of the sequence of a CYCLE_CALL of the built-in drilling family, raised as a MOTION event of its own
/// under the VM option ExpandCycles, for analytics (virtual machine 3.3, D37).
/// </summary>
internal sealed record CycleMotion
{
    /// <summary>
    /// RAPID for the positioning, the approach, the return between pecks and the retract; LINE for a feed at CYCLE_F.
    /// </summary>
    public required Verb Verb { get; init; }

    /// <summary>
    /// The axes the motion moves, each at its target as the position store holds it; unknown where a value is.
    /// </summary>
    public required IReadOnlyDictionary<string, AxisPosition> To { get; init; }

    /// <summary>
    /// CYCLE_F for a LINE; null for a RAPID, and for a LINE of a cycle that gives no known CYCLE_F.
    /// </summary>
    public decimal? Feed { get; init; }
}
