using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The cycle definition of language 4.7: the row of virtual machine 2.6. CYCLE_CALL, the execution of the cycle, is
/// step 5 (virtual machine 3.3, P1-03).
/// </summary>
internal static class CycleHandlers
{
    /// <summary>
    /// Registers the cycle word; its parameter words belong to it and set nothing on their own.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["CYCLE"] = ApplyCycle;
    }

    /// <summary>
    /// CYCLE=OFF clears the cycle: name OFF, no parameters, the axis back at the tool axis of the workplane (virtual
    /// machine 2.6). TOOL (with a WARNING) and PROGRAM=END end it the same way (virtual machine 4).
    /// </summary>
    public static void EndCycle(ChannelState state)
    {
        state.Cycle.Name = null;
        state.Cycle.Controller = null;
        state.Cycle.Parameters.Clear();
        state.Cycle.Axis = CycleState.ToolAxisOf(state.Frame.Workplane);
    }

    // CYCLE=name defines the active cycle with the parameter words of its block and AXIS, by default the tool axis of
    // the workplane; a new CYCLE replaces all parameters, nothing is inherited (virtual machine 2.6). CYCLE:controller=n
    // sets the cycle and stores the native parameters of the block unresolved, in source order (virtual machine 3 step
    // 3, D94).
    private static void ApplyCycle(Word word, BlockContext context)
    {
        if (word.Addr is null && BlockContext.IdentOf(word) == "OFF")
        {
            EndCycle(context.State);
            return;
        }

        CycleState cycle = context.State.Cycle;
        cycle.Name = word.Value.ToCanonical();
        cycle.Controller = word.Addr;
        cycle.Parameters.Clear();
        bool nativeBlock = word.Addr is not null;
        foreach (Word parameter in context.Block.Words)
        {
            if (IsParameter(parameter, nativeBlock))
            {
                cycle.Parameters.Add(parameter);
            }
        }

        Word? axis = context.Block.Find("AXIS");
        cycle.Axis = axis is null ? CycleState.ToolAxisOf(context.State.Frame.Workplane) : axis.Value.ToCanonical();
    }

    // The parameter words of a cycle block: the cycle words of language 4.7 but CYCLE, AXIS and CYCLE_CALL, and in a
    // CYCLE:controller=n block every key the catalog does not know, a native parameter (D94).
    private static bool IsParameter(Word word, bool nativeBlock)
    {
        if (word.Definition is null)
        {
            return nativeBlock;
        }

        return word.Definition.Group == WordKind.Cycle && word.Key is not ("CYCLE" or "AXIS" or "CYCLE_CALL");
    }
}
