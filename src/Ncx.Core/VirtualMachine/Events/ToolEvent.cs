using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// TOOL_BEGIN and TOOL_END (virtual machine 3.5, 7): a tool enters or leaves the spindle of a holder, with the rpm of
/// the holder's spindle, and at TOOL_END the distance and the block count under the tool.
/// </summary>
public sealed record ToolEvent : VmEvent
{
    /// <summary>
    /// TOOL_BEGIN: the tool entered the spindle; TOOL_END: it left it.
    /// </summary>
    public required EventPhase Phase { get; init; }

    /// <summary>
    /// The tool, a number or a name (language 4.4); never tool 0, the empty spindle.
    /// </summary>
    public required ToolRef Tool { get; init; }

    /// <summary>
    /// The resource id of the holder (virtual machine 2.3).
    /// </summary>
    public required string Holder { get; init; }

    /// <summary>
    /// The rpm of the spindle of the holder, the default spindle for a holder without one (virtual machine 2.4, 5):
    /// after the block at TOOL_BEGIN, before it at TOOL_END; null when it is unknown or the machine has no such
    /// spindle.
    /// </summary>
    public decimal? Rpm { get; init; }

    /// <summary>
    /// The distance under the tool, summed from the MOTION lengths that are known while the tool was in the spindle
    /// of the holder called last; 0 at TOOL_BEGIN.
    /// </summary>
    public decimal Distance { get; init; }

    /// <summary>
    /// The blocks executed under the tool while its holder was the holder called last, from the block that brought it
    /// in to the block before the one that took it out; 0 at TOOL_BEGIN.
    /// </summary>
    public int Blocks { get; init; }

    /// <inheritdoc/>
    public override string Kind => Phase == EventPhase.Begin ? "TOOL_BEGIN" : "TOOL_END";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        string tool = $"tool {Tool}, holder {Holder}, rpm {EventText.Number(Rpm)}";
        if (Phase == EventPhase.Begin)
        {
            return tool;
        }

        return tool
            + $", distance {EventText.Number(Distance)}, blocks {Blocks.ToString(CultureInfo.InvariantCulture)}";
    }
}
