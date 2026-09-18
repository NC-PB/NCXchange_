using Ncx.Core.Machine;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The options of one run of the virtual machine, passed explicitly; there are no global settings (code-guidelines 5,
/// Options; architecture 5). A test constructs the options it needs; a front end starts from <see cref="ForMachine"/>.
/// </summary>
public sealed record VmOptions
{
    /// <summary>
    /// ExpandCycles: a CYCLE_CALL is raised as its individual MOTION events, for analytics (virtual machine 3.3, D37).
    /// False by default.
    /// </summary>
    public bool ExpandCycles { get; init; }

    /// <summary>
    /// Every executed block also raises BLOCK_WRITE, the last of its events, with the words of the block and no output
    /// lines yet, so that the compiler subscribed to the run writes each block from its Before and After (virtual
    /// machine 7, architecture 5.3 and 8). False by default: check, trace and analyze see the other events only.
    /// </summary>
    public bool RaiseBlockWrite { get; init; }

    /// <summary>
    /// skip_blocks: which SKIP blocks are skipped; none by default, every SKIP block runs (D53).
    /// </summary>
    public SkipBlocks SkipBlocks { get; init; } = SkipBlocks.None;

    /// <summary>
    /// The block cap of INTERPRETED mode, the ERROR "possible endless loop" beyond it; 1 000 000 by default (virtual
    /// machine 3.6).
    /// </summary>
    public long BlockCap { get; init; } = 1_000_000;

    /// <summary>
    /// How deep calls and repeats may nest; a CALL beyond it is the ERROR "call depth exceeded"; 8 by default (virtual
    /// machine 3.6, 3.9, D99).
    /// </summary>
    public int CallDepth { get; init; } = 8;

    /// <summary>
    /// The arc tolerance in the active units; null for the defaults of D36, 0.01 mm and 0.0005 in (virtual machine
    /// 3.2).
    /// </summary>
    public decimal? ArcTolerance { get; init; }

    /// <summary>
    /// What reading an unassigned variable does: an ERROR, or 0 under unassigned = 0 (virtual machine 3.6, D38).
    /// </summary>
    public UnassignedVariable Unassigned { get; init; } = UnassignedVariable.Error;

    /// <summary>
    /// The number of channels of the job whose channel program the run compiles; null outside a job. SYNC in a
    /// single-channel job is a WARNING (virtual machine 3.7, 5), and the compile of one channel of a job with several
    /// channels is none (implementation 16, P6-02). The job scheduler tells its own virtual machines.
    /// </summary>
    public int? JobChannels { get; init; }

    /// <summary>
    /// The options a machine file gives: the block cap, the call depth and unassigned of [variables] (machine-config 7,
    /// virtual machine 3.6); everything else at its default.
    /// </summary>
    /// <param name="machine">The machine file, or the built-in default machine of D103.</param>
    public static VmOptions ForMachine(MachineConfig machine)
    {
        // TODO(question): machine-config names no key for the arc tolerance of D36 ("the tolerance from the machine
        // configuration", virtual machine 3.2); it stays null, the D36 defaults per units, until one is named (D139).
        return new VmOptions
        {
            BlockCap = machine.Variables.BlockCap,
            CallDepth = machine.Variables.CallDepth,
            Unassigned = machine.Variables.Unassigned,
        };
    }
}
