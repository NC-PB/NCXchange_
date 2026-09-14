using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The tool words of language 4.4 on the holder step 2 resolved (virtual machine 2.3, 3.5, 3.8 rule 2): PRELOAD and
/// TOOL through the tool change rules, the offsets on the holder of the last TOOL, and the compensation.
/// </summary>
internal static class ToolHandlers
{
    /// <summary>
    /// Registers the tool words.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["PRELOAD"] = ApplyPreload;
        handlers["TOOL"] = ApplyTool;
        handlers["OFFSET"] = ApplyOffset;
        handlers["COMP"] = ApplyComp;
    }

    // PRELOAD=n prepares tool n in the magazine of the holder, PRELOAD=0 clears (language 4.4, virtual machine 3.5).
    // The tool rules are suppressed inside a subprogram that no program calls (D99).
    private static void ApplyPreload(Word word, BlockContext context)
    {
        if (!context.ResourceOf.TryGetValue(word, out string? holderId) || ToolOf(word) is not ToolRef tool)
        {
            return;
        }

        ToolChangeRules.ApplyPreload(context.State.Holders[holderId], tool, context.Block,
            context.CallerRuleDiagnostics);
    }

    // TOOL=n, a bare TOOL and TOOL=0 on the holder of the word (language 4.4, virtual machine 3.5); the holder becomes
    // the holder of the last TOOL, which OFFSET addresses (2.3, 3.8 rule 2). The tool rules are suppressed inside a
    // subprogram that no program calls (D99).
    private static void ApplyTool(Word word, BlockContext context)
    {
        if (!context.ResourceOf.TryGetValue(word, out string? holderId))
        {
            return;
        }

        HolderState holder = context.State.Holders[holderId];
        Diagnostics diagnostics = context.CallerRuleDiagnostics;
        ToolChangeRules.CheckCycleAndCompensation(context.State, word, context.Block, diagnostics);
        if (word.Value is NoValue)
        {
            ToolChangeRules.ApplyBareTool(holder, context.Block, diagnostics);
        }
        else if (word.Value is IntegerValue { Number: 0 })
        {
            ToolChangeRules.ApplyEmptySpindle(holder);
        }
        else if (ToolOf(word) is ToolRef tool)
        {
            ToolChangeRules.ApplyTool(holder, tool, context.Block, diagnostics);
        }

        context.State.LastHolder = holderId;
    }

    // OFFSET:LEN, OFFSET:RAD and OFFSET, the combined register, on the holder of the last TOOL (language 4.4, virtual
    // machine 2.3, 3.8 rule 2); 0 cancels.
    private static void ApplyOffset(Word word, BlockContext context)
    {
        if (!context.ResourceOf.TryGetValue(word, out string? holderId)
            || BlockContext.IntegerOf(word) is not int register)
        {
            return;
        }

        HolderState holder = context.State.Holders[holderId];
        switch (word.Addr)
        {
            case "LEN":
                holder.OffsetLen = register;
                break;
            case "RAD":
                holder.OffsetRad = register;
                break;
            default:
                holder.OffsetCombined = register;
                break;
        }
    }

    // COMP=LEFT, RIGHT or OFF: cutter radius compensation from the motion of the same block on
    // (language 4.4, 5 rule 3).
    private static void ApplyComp(Word word, BlockContext context)
    {
        context.State.Motion.Comp = BlockContext.IdentOf(word) switch
        {
            "LEFT" => Compensation.Left,
            "RIGHT" => Compensation.Right,
            _ => Compensation.Off,
        };
    }

    // The tool of TOOL and PRELOAD, an integer or a string (language 4.4); null for a bare TOOL.
    private static ToolRef? ToolOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue => new ToolRef(BlockContext.IntegerOf(word) ?? 0),
            StringValue name => new ToolRef(name.Content),
            _ => null,
        };
    }
}
