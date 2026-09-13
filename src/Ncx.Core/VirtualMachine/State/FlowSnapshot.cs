using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The flow rows of virtual machine 2.1 and 2.7, and the restore stack of 3.10, as they stood when the snapshot was
/// taken; immutable, part of the Before and After of every event (architecture 5.3).
/// </summary>
public sealed record FlowSnapshot
{
    /// <summary>
    /// file.programs: the PROGRAM sections of the file in file order.
    /// </summary>
    public required IReadOnlyList<Section> Programs { get; init; }

    /// <summary>
    /// file.subs: the SUB sections of the file by their NAME.
    /// </summary>
    public required IReadOnlyDictionary<string, Section> Subs { get; init; }

    /// <summary>
    /// labels: for each program and subprogram, its labels and the index of the block each stands on.
    /// </summary>
    public required IReadOnlyDictionary<Section, IReadOnlyDictionary<string, int>> Labels { get; init; }

    /// <summary>
    /// pc: the index of the block that runs.
    /// </summary>
    public required int Pc { get; init; }

    /// <summary>
    /// callStack: the calls that have not returned, the innermost first.
    /// </summary>
    public required IReadOnlyList<CallFrame> Calls { get; init; }

    /// <summary>
    /// repeatStack: the repeats that have passes left, the innermost first.
    /// </summary>
    public required IReadOnlyList<RepeatFrame> Repeats { get; init; }

    /// <summary>
    /// blocksExecuted: the blocks executed so far.
    /// </summary>
    public required long BlocksExecuted { get; init; }

    /// <summary>
    /// The restore stack of @SAVE and @RESTORE, the oldest entry first (virtual machine 3.10, D95).
    /// </summary>
    public required IReadOnlyList<RestoreEntry> RestoreStack { get; init; }
}
