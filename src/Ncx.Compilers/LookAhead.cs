using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The look-ahead of a compile (architecture 8; machine-config 3; D52, virtual machine 3.5): the STATIC pass records
/// every block with its state before any line is written, so that a template finds what comes after its block: the
/// PRELOAD that follows a TOOL, the next tool, the index angle of the next positioning block, the next orientation of
/// the tool spindle, and the state the header of a program needs. Every search stays inside the program of its block,
/// the walks of the subprograms it calls included: PROGRAM=END is the executed end of a program, where the control
/// rewinds (language 4.1, 4.13; virtual machine 1, 3.9).
/// </summary>
public sealed class LookAhead
{
    // The role of the tool spindle, whose orientation {c} is (machine-config introduction, language 4.10).
    private const string ToolRole = "TOOL";

    // The axis of the tool carrier's index angle, {b} (machine-config introduction).
    private const string IndexAxis = "B";

    // Tool 0 is the empty spindle (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    private readonly IReadOnlyList<BlockStep> _steps;
    private readonly MachineConfig _machine;

    // The index of the last step of the program each step belongs to, by the index of the step.
    private readonly int[] _programEnds;

    // Whether the program each step belongs to has PRELOAD words, by the index of the step (virtual machine 3.5).
    private readonly bool[] _programHasPreload;

    // The steps whose PRELOAD a change command took up (machine-config 3).
    private readonly List<int> _folded = [];

    internal LookAhead(IReadOnlyList<BlockStep> steps, MachineConfig machine)
    {
        _steps = steps;
        _machine = machine;
        _programEnds = new int[steps.Count];
        _programHasPreload = new bool[steps.Count];
        FindPrograms();
    }

    /// <summary>
    /// Every step of the run in walk order.
    /// </summary>
    public IReadOnlyList<BlockStep> Steps => _steps;

    /// <summary>
    /// True when the program the step belongs to writes PRELOAD words of its own; auto_preload leaves such a program
    /// alone (virtual machine 3.5). A subprogram that no program calls is a program of its own here.
    /// </summary>
    /// <param name="step">A step of the program.</param>
    public bool ProgramHasPreload(BlockStep step)
    {
        return _programHasPreload[step.Index];
    }

    /// <summary>
    /// The step whose PRELOAD follows a TOOL block before the next motion, for the same holder: the TOOL block itself
    /// when it carries one (machine-config 3, language 4.4: a PRELOAD that follows the TOOL block before the next
    /// motion is folded into a change command with {next}); null when none does before the end of the program.
    /// </summary>
    /// <param name="toolStep">The step of the TOOL block.</param>
    /// <param name="holder">The resource id of the holder of the change.</param>
    public BlockStep? FollowingPreload(BlockStep toolStep, string holder)
    {
        int end = ProgramEnd(toolStep);
        for (int index = toolStep.Index; index <= end; index++)
        {
            BlockStep step = _steps[index];

            // The next change of the holder takes up its own preload.
            if (index > toolStep.Index && step.Block.Has("TOOL") && step.After.LastHolder == holder)
            {
                return null;
            }

            if (PreloadOf(step, holder) is not null)
            {
                return step;
            }

            // A motion ends the search; its own PRELOAD acts before it (language 5 rule 3) and counted above.
            if (step.IsMotion)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The tool a step preloads into a holder; null when it preloads nothing there (virtual machine 3.5, 7).
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="holder">The resource id of the holder.</param>
    public static ToolRef? PreloadOf(BlockStep step, string holder)
    {
        foreach (VmEvent vmEvent in step.Events)
        {
            if (vmEvent is PreloadEvent preload && preload.Holder == holder)
            {
                return preload.Tool;
            }
        }

        return null;
    }

    /// <summary>
    /// The next tool of the holder after a change, the next change to another tool in walk order in the same program:
    /// the preload of auto_preload and the {next} it fills (machine-config 3, virtual machine 3.5); null after the
    /// last change of the program.
    /// </summary>
    /// <param name="toolStep">The step of the TOOL block.</param>
    /// <param name="holder">The resource id of the holder.</param>
    public ToolRef? NextTool(BlockStep toolStep, string holder)
    {
        if (!toolStep.After.Holders.TryGetValue(holder, out HolderSnapshot? now))
        {
            return null;
        }

        int end = ProgramEnd(toolStep);
        for (int index = toolStep.Index + 1; index <= end; index++)
        {
            BlockStep step = _steps[index];
            if (!step.Block.Has("TOOL")
                || step.After.LastHolder != holder
                || !step.After.Holders.TryGetValue(holder, out HolderSnapshot? later))
            {
                continue;
            }

            if (later.SpindleTool != now.SpindleTool && later.SpindleTool != s_emptySpindle)
            {
                return later.SpindleTool;
            }
        }

        return null;
    }

    // Where the preload of the next tool that auto_preload inserts after the change of toolStep stands under
    // preload_position = "before_first_motion" (machine-config 3, virtual machine 3.5): before the lines of the step
    // returned, the first motion after the change, or the next change of the holder when that comes first; toolStep
    // itself when the preload stands right after the change; null when the next change of the holder brings the tool
    // the preload names before any motion, since the preload would then be one of the tool already in the spindle
    // (virtual machine 5). The preload is written into the part (program or walk of a subprogram) of the change, or
    // into the caller that part returns to: the shallowest part the walk order reaches from the change to that step,
    // SUB=BEGIN and SUB=END standing inside their walk (virtual machine 3.9, D99).
    // TODO(question): machine-config 3 puts the preload before the first motion after the change and does not say
    // where it stands when that motion lies in the walk of a subprogram, whose section is written once for every
    // caller (virtual machine 3.9, D99): before the line of the CALL that enters the walk, or right after the change
    // where no line of its part stands between the change and that walk, until that is answered.
    internal BlockStep? BeforeFirstMotion(BlockStep toolStep, string holder, ToolRef tool)
    {
        int depth = 0;
        int shallowest = 0;
        BlockStep lastOfShallowest = toolStep;
        int end = ProgramEnd(toolStep);
        for (int index = toolStep.Index + 1; index <= end; index++)
        {
            BlockStep step = _steps[index];
            if (step.Block.Has("SUB", null, "BEGIN"))
            {
                depth++;
            }

            if (depth == shallowest)
            {
                lastOfShallowest = step;
            }

            bool change = step.Block.Has("TOOL") && step.After.LastHolder == holder;
            if (change && step.After.Holders.TryGetValue(holder, out HolderSnapshot? after)
                && after.SpindleTool == tool)
            {
                return null;
            }

            if (change || step.IsMotion)
            {
                return lastOfShallowest;
            }

            // The walk of the change returns to its caller, where no line stands yet before the next one.
            if (step.Block.Has("SUB", null, "END") && --depth < shallowest)
            {
                shallowest = depth;
                lastOfShallowest = toolStep;
            }
        }

        return toolStep;
    }

    /// <summary>
    /// {b}: the index angle of the tool carrier from the next positioning block of the program, the B position after
    /// the next motion that names B (machine-config introduction, D52); null when none follows or its B is unknown.
    /// </summary>
    /// <param name="step">The step the template is written for.</param>
    public decimal? NextB(BlockStep step)
    {
        int end = ProgramEnd(step);
        for (int index = step.Index + 1; index <= end; index++)
        {
            BlockStep later = _steps[index];
            if (!later.IsMotion || (!later.Block.Has(IndexAxis) && !later.Block.Has("I" + IndexAxis)))
            {
                continue;
            }

            return later.After.Motion.Position.TryGetValue(IndexAxis, out AxisPosition position) && position.Known
                ? position.Value
                : null;
        }

        return null;
    }

    /// <summary>
    /// {c}: the orientation of the tool spindle from the next ORIENT:TOOL of the program (machine-config introduction,
    /// D52); null when none follows or its angle is unknown.
    /// </summary>
    /// <param name="step">The step the template is written for.</param>
    public decimal? NextToolOrientation(BlockStep step)
    {
        string? toolSpindle = _machine.ResolveRole(ToolRole)?.Id;
        int end = ProgramEnd(step);
        for (int index = step.Index + 1; index <= end; index++)
        {
            BlockStep later = _steps[index];
            if (later.Block.Find("ORIENT", ToolRole) is not Word orient)
            {
                continue;
            }

            if (orient.Value is IntegerValue integer)
            {
                return integer.Number;
            }

            if (orient.Value is DecimalValue angle)
            {
                return angle.Number;
            }

            if (toolSpindle is null || !later.After.Spindles.TryGetValue(toolSpindle, out SpindleSnapshot? spindle))
            {
                return null;
            }

            return spindle.Orientation;
        }

        return null;
    }

    /// <summary>
    /// The state the header of a program is written from: after its first block with a verb, where a writer's
    /// complete header stands (D34), else before its PROGRAM=END.
    /// </summary>
    /// <param name="program">The program section.</param>
    public ChannelSnapshot HeaderState(Section program)
    {
        BlockStep? end = null;
        foreach (BlockStep step in _steps)
        {
            if (step.Section != program)
            {
                continue;
            }

            if (step.Block.Verb is not null)
            {
                return step.After;
            }

            if (step.Block.Has("PROGRAM", null, "END"))
            {
                end = step;
            }
        }

        return end?.Before ?? throw new InvalidOperationException("A program of the run has no PROGRAM=END.");
    }

    /// <summary>
    /// True when a change command took up the PRELOAD of the step, so that it is not written again (machine-config
    /// 3).
    /// </summary>
    public bool IsFolded(BlockStep step)
    {
        return _folded.Contains(step.Index);
    }

    // A change command with {next} took up the PRELOAD of the step (machine-config 3).
    internal void Fold(BlockStep step)
    {
        if (!_folded.Contains(step.Index))
        {
            _folded.Add(step.Index);
        }
    }

    // The index of the last step of the program a step belongs to.
    private int ProgramEnd(BlockStep step)
    {
        return _programEnds[step.Index];
    }

    // The programs of the run (virtual machine 1, 3.9; language 4.1, 4.13): the STATIC run walks the programs of the
    // file one after another, each with the walks of the subprograms it calls, then every subprogram that no program
    // calls, once each. A program runs from its PROGRAM=BEGIN to its PROGRAM=END, the executed end where the control
    // rewinds, so that no look-ahead reaches into the next program. A subprogram that no program calls runs from its
    // SUB=BEGIN to its SUB=END, with the walks it enters. FILE=BEGIN and FILE=END stand alone.
    private void FindPrograms()
    {
        int first = 0;
        bool inProgram = false;
        int walkDepth = 0;
        for (int index = 0; index < _steps.Count; index++)
        {
            Block block = _steps[index].Block;
            if (!inProgram && walkDepth == 0)
            {
                first = index;
                inProgram = block.Has("PROGRAM", null, "BEGIN");
            }

            if (!inProgram && block.Has("SUB", null, "BEGIN"))
            {
                walkDepth++;
            }
            else if (!inProgram && block.Has("SUB", null, "END"))
            {
                walkDepth--;
            }

            bool ends = inProgram ? block.Has("PROGRAM", null, "END") : walkDepth <= 0;
            if (ends || index == _steps.Count - 1)
            {
                MarkProgram(first, index);
                inProgram = false;
                walkDepth = 0;
            }
        }
    }

    // The steps from first to last are one program of the run, which has PRELOAD words when one of them has
    // (virtual machine 3.5).
    // TODO(question): virtual machine 3.5 leaves alone "a program that has its own PRELOAD words" and does not say
    // whether the PRELOAD words of a subprogram the program calls are its own; they count, so that auto_preload
    // inserts no preload beside those the run of the program writes, until that is answered.
    private void MarkProgram(int first, int last)
    {
        bool hasPreload = false;
        for (int index = first; index <= last; index++)
        {
            hasPreload |= _steps[index].Block.Has("PRELOAD");
        }

        for (int index = first; index <= last; index++)
        {
            _programEnds[index] = last;
            _programHasPreload[index] = hasPreload;
        }
    }
}
