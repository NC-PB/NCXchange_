using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The flow rows of virtual machine 2.1 and 2.7: the programs, subprograms and labels of the pre-pass, the pc, the
/// call and repeat stacks and the count of executed blocks, with the restore stack of @SAVE and @RESTORE (virtual
/// machine 3.10). Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class FlowState
{
    // file.programs, file.subs and the labels are empty until the pre-pass over the file (virtual machine 2.1, 2.7);
    // pc starts at the first block, the call and repeat stacks empty, blocksExecuted 0 (2.7); the restore stack starts
    // empty (3.10).
    public FlowState()
    {
        Pc = 0;
        BlocksExecuted = 0;
    }

    /// <summary>
    /// file.programs: the PROGRAM sections in file order.
    /// </summary>
    public List<Section> Programs { get; } = [];

    /// <summary>
    /// file.subs: the SUB sections by their NAME.
    /// </summary>
    public Dictionary<string, Section> Subs { get; } = new();

    /// <summary>
    /// labels: for each program and subprogram, its labels and the index of the block each stands on.
    /// </summary>
    public Dictionary<Section, Dictionary<string, int>> Labels { get; } = new();

    /// <summary>
    /// pc: the index of the block that runs.
    /// </summary>
    public int Pc { get; set; }

    /// <summary>
    /// callStack.
    /// </summary>
    public Stack<CallFrame> Calls { get; } = new();

    /// <summary>
    /// repeatStack.
    /// </summary>
    public Stack<RepeatFrame> Repeats { get; } = new();

    public long BlocksExecuted { get; set; }

    /// <summary>
    /// The restore stack of @SAVE and @RESTORE, the newest entry last (virtual machine 3.10, D95).
    /// </summary>
    public List<RestoreEntry> RestoreStack { get; } = [];

    /// <summary>
    /// An immutable copy of the flow as it is now.
    /// </summary>
    public FlowSnapshot Snapshot()
    {
        // The copy keeps every list, stack and table as it is now while the live flow goes on (code-guidelines 7).
        var labels = new Dictionary<Section, IReadOnlyDictionary<string, int>>();
        foreach (KeyValuePair<Section, Dictionary<string, int>> section in Labels)
        {
            labels[section.Key] = new Dictionary<string, int>(section.Value).AsReadOnly();
        }

        return new FlowSnapshot
        {
            Programs = new List<Section>(Programs).AsReadOnly(),
            Subs = new Dictionary<string, Section>(Subs).AsReadOnly(),
            Labels = labels.AsReadOnly(),
            Pc = Pc,
            Calls = new List<CallFrame>(Calls).AsReadOnly(),
            Repeats = new List<RepeatFrame>(Repeats).AsReadOnly(),
            BlocksExecuted = BlocksExecuted,
            RestoreStack = new List<RestoreEntry>(RestoreStack).AsReadOnly(),
        };
    }
}
