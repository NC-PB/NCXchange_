using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Handlers;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// Applies the two-word tool change (language 4.4) to a holder (virtual machine 3.5): the transition table of the tool
/// change states Empty, Loaded and Pending (architecture 5.2).
/// </summary>
internal static class ToolChangeRules
{
    // Tool 0 is the empty spindle: TOOL=0 empties it and PRELOAD=0 clears the preload (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    // PRELOAD=n prepares n and PRELOAD=0 clears the preload; the spindle is not touched. Preloading the tool that is
    // already in the spindle is a no-op on the machine: a WARNING, and nothing changes (virtual machine 3.5, row
    // PRELOAD=n; architecture 5.2).
    public static void ApplyPreload(HolderState holder, ToolRef tool, Block block, Diagnostics diagnostics)
    {
        if (tool == s_emptySpindle)
        {
            holder.Preloaded = null;
            return;
        }

        if (holder.SpindleTool == tool)
        {
            diagnostics.Warning(block, DiagnosticCodes.ToolAlreadyInSpindle,
                $"Tool {tool} is already in the spindle; PRELOAD={tool} changes nothing (virtual machine 3.5).");
            return;
        }

        holder.Preloaded = tool;
    }

    // TOOL=n puts n into the spindle. The preload is consumed only when it names the
    // tool that arrives; a different preload is a WARNING because the magazine has to
    // cycle twice (virtual machine 3.5, row TOOL=n).
    public static void ApplyTool(HolderState holder, ToolRef tool, Block block, Diagnostics diagnostics)
    {
        if (holder.Preloaded is ToolRef preloaded && preloaded != tool)
        {
            diagnostics.Warning(block, DiagnosticCodes.PreloadMismatch,
                $"Tool {preloaded} was preloaded but tool {tool} is called (virtual machine 3.5).");
        }

        // The change itself: TOOL_END for the old tool, TOOL_BEGIN for the new one, is
        // raised by the caller from the Before and After snapshots.
        // TODO(question): virtual machine 3.5 and the sample of code-guidelines 2 keep a different preload after
        // TOOL=k, while the state diagram of architecture 5.2 draws TOOL=k as a transition from Pending to Loaded; the
        // preload is kept, as the virtual machine says, until that is answered.
        holder.SpindleTool = tool;
        holder.Preloaded = holder.Preloaded == tool ? null : holder.Preloaded;
    }

    // A bare TOOL changes to the preloaded tool and is an ERROR when nothing is
    // preloaded (virtual machine 3.5, row TOOL).
    public static void ApplyBareTool(HolderState holder, Block block, Diagnostics diagnostics)
    {
        if (holder.Preloaded is not ToolRef preloaded)
        {
            diagnostics.Error(block, DiagnosticCodes.NothingPreloaded,
                "TOOL without a value needs a preloaded tool (virtual machine 3.5).");
            return;
        }

        holder.SpindleTool = preloaded;
        holder.Preloaded = null;
    }

    // TOOL=0 empties the spindle; a preload stays (virtual machine 3.5, row TOOL=0; architecture 5.2, Loaded to
    // Empty).
    public static void ApplyEmptySpindle(HolderState holder)
    {
        holder.SpindleTool = s_emptySpindle;
    }

    // TOOL while a cycle is active or compensation is on is a WARNING (virtual machine 3.5, row TOOL=0; 5), and the
    // change ends the cycle (virtual machine 4, row cycle: reset by TOOL with a WARNING).
    public static void CheckCycleAndCompensation(ChannelState state, Word tool, Block block, Diagnostics diagnostics)
    {
        if (state.Cycle.Active)
        {
            diagnostics.Warning(block, DiagnosticCodes.ToolChangeWhileCycleActive,
                $"{tool.ToCanonical()} while the cycle {state.Cycle.Name} is active; the tool change ends the cycle "
                + "(virtual machine 3.5, 4).");
            CycleHandlers.EndCycle(state);
        }

        if (state.Motion.Comp != Compensation.Off)
        {
            diagnostics.Warning(block, DiagnosticCodes.ToolChangeWithCompensationOn,
                $"{tool.ToCanonical()} while the compensation is on (virtual machine 3.5, 5).");
        }
    }
}
