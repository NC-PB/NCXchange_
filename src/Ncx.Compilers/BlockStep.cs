using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// One block as the STATIC walk of the virtual machine executed it, in walk order: the block with the state of the
/// channel before and after it and the events it raised (architecture 8). A block of a subprogram is one step per walk,
/// one per CALL (D99).
/// </summary>
public sealed record BlockStep
{
    /// <summary>
    /// The place of the step in the walk, from 0.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The block, as the expander left it.
    /// </summary>
    public required Block Block { get; init; }

    /// <summary>
    /// The resolved state of the channel before the block (virtual machine 7).
    /// </summary>
    public required ChannelSnapshot Before { get; init; }

    /// <summary>
    /// The resolved state of the channel after the block (virtual machine 7).
    /// </summary>
    public required ChannelSnapshot After { get; init; }

    /// <summary>
    /// The events the block raised in their order, BLOCK_WRITE left out: TOOL_BEGIN, PRELOAD, MOTION, CYCLE_CALL
    /// (virtual machine 7).
    /// </summary>
    public required IReadOnlyList<VmEvent> Events { get; init; }

    /// <summary>
    /// The program or subprogram the block stands in; null for FILE=BEGIN and FILE=END, which stand in none (language
    /// 4.13).
    /// </summary>
    public Section? Section { get; init; }

    /// <summary>
    /// True for a block whose verb moves the machine: RAPID, LINE, ARC, RETRACT, HOME, CYCLE_CALL (language 5 rule 1).
    /// </summary>
    public bool IsMotion => Block.Verb?.Key is "RAPID" or "LINE" or "ARC" or "RETRACT" or "HOME" or "CYCLE_CALL";

    /// <summary>
    /// The BLOCK_WRITE the virtual machine raised for the block; null for FILE=BEGIN and FILE=END.
    /// </summary>
    internal BlockWriteEvent? BlockWrite { get; init; }
}
