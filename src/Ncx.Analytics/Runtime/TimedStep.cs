using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// One time the runtime estimate charges: a motion, a dwell, a cycle dwell or a spindle start or stop, with the event
/// it belongs to, whose block, channel and After give its tool and its place in the block range, and the SECTION it
/// stood in.
/// </summary>
internal sealed record TimedStep
{
    /// <summary>
    /// The seconds charged.
    /// </summary>
    public required double Seconds { get; init; }

    /// <summary>
    /// The MOTION, DWELL, CYCLE_CALL or STATE_CHANGE the time belongs to.
    /// </summary>
    public required VmEvent Event { get; init; }

    /// <summary>
    /// The text of the SECTION the event stood in; null before the first SECTION of its program.
    /// </summary>
    public string? Section { get; init; }
}
