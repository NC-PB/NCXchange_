namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The state of one channel as it stood when the snapshot was taken: an immutable deep copy of every table of virtual
/// machine 2, the Before and the After of every event (virtual machine 7, architecture 5.3). A listener reads it and
/// never changes the virtual machine through it (D61, D106).
/// </summary>
public sealed record ChannelSnapshot
{
    /// <summary>
    /// channel.id: 1 or the CHANNEL of the program (virtual machine 2.8).
    /// </summary>
    public required int ChannelId { get; init; }

    /// <summary>
    /// The program rows of virtual machine 2.1.
    /// </summary>
    public required ProgramSnapshot Program { get; init; }

    /// <summary>
    /// The frame rows of virtual machine 2.1.
    /// </summary>
    public required FrameSnapshot Frame { get; init; }

    /// <summary>
    /// The motion rows of virtual machine 2.2.
    /// </summary>
    public required MotionSnapshot Motion { get; init; }

    /// <summary>
    /// The cycle row of virtual machine 2.6.
    /// </summary>
    public required CycleSnapshot Cycle { get; init; }

    /// <summary>
    /// The flow rows of virtual machine 2.1 and 2.7.
    /// </summary>
    public required FlowSnapshot Flow { get; init; }

    /// <summary>
    /// vars: the variables the block sees, the locals of the call that runs among them, by name (virtual machine
    /// 2.7).
    /// </summary>
    public required IReadOnlyDictionary<string, VariableValue> Vars { get; init; }

    /// <summary>
    /// The state of each spindle by its resource id (virtual machine 2.4).
    /// </summary>
    public required IReadOnlyDictionary<string, SpindleSnapshot> Spindles { get; init; }

    /// <summary>
    /// The state of each tool holder by its resource id (virtual machine 2.3).
    /// </summary>
    public required IReadOnlyDictionary<string, HolderSnapshot> Holders { get; init; }

    /// <summary>
    /// lastHolder: the resource id of the holder of the last TOOL, which OFFSET addresses (virtual machine 2.3, 3.8
    /// rule 2); null on a machine without a holder.
    /// </summary>
    public required string? LastHolder { get; init; }

    /// <summary>
    /// coolant: on or off for each coolant channel by its name (virtual machine 2.5).
    /// </summary>
    public required IReadOnlyDictionary<string, bool> Coolant { get; init; }

    /// <summary>
    /// function: the state of each named function by its name, OPEN of FUNC:SUB_CHUCK=OPEN; null while the program
    /// has set none (virtual machine 2.5).
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Functions { get; init; }

    /// <summary>
    /// channel.waitingAt: the SYNC mark the channel waits at; null for none (virtual machine 2.8).
    /// </summary>
    public required int? WaitingAt { get; init; }

    /// <summary>
    /// channel.finished: true from PROGRAM=END on (virtual machine 2.8).
    /// </summary>
    public required bool Finished { get; init; }
}
