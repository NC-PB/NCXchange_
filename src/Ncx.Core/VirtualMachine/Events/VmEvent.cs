using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// An event of the virtual machine, one record per row of virtual machine 7: what a block of a channel did, with the
/// state of the channel around the block. The records are immutable, so a listener observes and never changes the
/// virtual machine (D61, D106); BLOCK_WRITE of the compilers is the one event with parts to edit (architecture 5.3).
/// </summary>
public abstract record VmEvent
{
    /// <summary>
    /// channel.id of the channel whose block raised the event (virtual machine 2.8).
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The block that raised the event; FILE=BEGIN and FILE=END for the events of the file.
    /// </summary>
    public required Block Block { get; init; }

    /// <summary>
    /// The resolved state of the channel before the block: the After of the events raised before it in the walk, so
    /// that a listener misses no change (virtual machine 7). What the walk does between two blocks, entering or leaving
    /// a subprogram (D99), comes with the next block.
    /// </summary>
    public required ChannelSnapshot Before { get; init; }

    /// <summary>
    /// The resolved state of the channel after the block (virtual machine 7).
    /// </summary>
    public required ChannelSnapshot After { get; init; }

    /// <summary>
    /// The event as virtual machine 7 names it, FILE_BEGIN, MOTION or TOOL_END: the hook a plugin listens for
    /// (architecture 9).
    /// </summary>
    public abstract string Kind { get; }

    /// <summary>
    /// The event as one line of text, the kind, the line of the block and the payload: TOOL_BEGIN(10): tool 1, holder
    /// H1, rpm 1592. An unknown value is written ?.
    /// </summary>
    public sealed override string ToString()
    {
        return Kind + "(" + Block.Line.ToString(CultureInfo.InvariantCulture) + "): " + PayloadText();
    }

    /// <summary>
    /// The payload of the event as text, the column Payload of virtual machine 7.
    /// </summary>
    protected abstract string PayloadText();
}
