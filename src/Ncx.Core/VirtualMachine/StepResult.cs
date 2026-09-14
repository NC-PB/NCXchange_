using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// One step of a run: the block the channel executed, and whether its run is over (architecture 5, Step). The job
/// scheduler advances every channel that is neither finished nor waiting by one step per round (virtual machine 3.7).
/// </summary>
internal sealed record StepResult
{
    /// <summary>
    /// The block the step executed, its expressions resolved in INTERPRETED mode; null when the run was over before the
    /// step executed a block.
    /// </summary>
    public Block? Executed { get; init; }

    /// <summary>
    /// True when the run is over: its program ended at PROGRAM=END, or an ERROR stopped it.
    /// </summary>
    public bool Ended { get; init; }

    /// <summary>
    /// True when an ERROR stopped the run (virtual machine 2.9).
    /// </summary>
    public bool Stopped { get; init; }
}
