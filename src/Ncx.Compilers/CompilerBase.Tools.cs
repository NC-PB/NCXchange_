using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

// The tool change and the preload with the look-ahead of the STATIC pass (machine-config 3; virtual machine 3.5; D10,
// D52, D91).
public abstract partial class CompilerBase
{
    // TODO(question): D10 and machine-config 1 let {kind} take "the default" for a tool without data and do not name
    // it; the kind ROTARY, the first of the kind_map example of machine-config 3, is written until that is answered.
    private const string DefaultKind = "ROTARY";

    // Tool 0 is the empty spindle (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    /// <summary>
    /// Writes the tool change of the block per [tool_change] (machine-config 3, virtual machine 3.5): unload for
    /// TOOL=0, change_preloaded when the tool is preloaded already and the machine has that template, change for every
    /// other change, a bare TOOL written with the number the virtual machine knows (D91). The placeholders come from
    /// the virtual machine and the look-ahead: {tool}, {name}, {offset}, {rpm}, {axis}, {next} (D52), {kind} from the
    /// tool table (D10), {b}, {c}.
    /// </summary>
    /// <param name="values">Values the controller sets itself, which the change keeps; null for none.</param>
    protected void WriteToolChange(TemplateValues? values = null)
    {
        BlockStep step = Step;
        if (!step.Block.Has("TOOL")
            || step.After.LastHolder is not string holder
            || !step.After.Holders.TryGetValue(holder, out HolderSnapshot? after))
        {
            return;
        }

        ToolChangeConfig? config = Machine.ToolChange;
        TemplateValues filled = values ?? new TemplateValues();

        // TOOL=0 empties the spindle with the unload template (machine-config 3).
        if (after.SpindleTool == s_emptySpindle)
        {
            WriteTemplate(config?.Unload, "[tool_change] unload (machine-config 3)", filled);
            return;
        }

        // change_preloaded is written instead of change when the tool is preloaded already, as for a bare TOOL; change
        // for every other change (machine-config 3, virtual machine 3.5).
        bool wasPreloaded = step.Before.Holders.TryGetValue(holder, out HolderSnapshot? before)
            && before.Preloaded == after.SpindleTool;
        bool asPreloaded = wasPreloaded && config?.ChangePreloaded is not null;
        string? text = asPreloaded ? config?.ChangePreloaded : config?.Change;
        if (TemplateOf(text) is Template template)
        {
            FillToolValues(filled, template, step, holder, after);
        }

        string what = asPreloaded ? "[tool_change] change_preloaded" : "[tool_change] change";
        WriteTemplate(text, what + " (machine-config 3)", filled);
    }

    /// <summary>
    /// Writes the PRELOAD of the block with the preload template (machine-config 3), unless a change command took it
    /// up with {next}; on a machine without a preload template PRELOAD is dropped with a WARNING.
    /// </summary>
    /// <param name="values">Values the controller sets itself; null for none.</param>
    protected void WritePreload(TemplateValues? values = null)
    {
        // A PRELOAD folded into the change command is not written again (machine-config 3).
        BlockStep step = Step;
        if (LookAhead.IsFolded(step))
        {
            return;
        }

        foreach (VmEvent vmEvent in step.Events)
        {
            if (vmEvent is PreloadEvent preload)
            {
                WritePreloadOf(preload.Tool, values);
            }
        }
    }

    // auto_preload: after each change the compiler inserts the preload of the next tool of the same program from the
    // STATIC look-ahead, where the program has no PRELOAD words of its own and the change command does not carry
    // {next}, after the change or before the first motion after it, per preload_position (machine-config 3, virtual
    // machine 3.5).
    private void WriteAutoPreload(BlockStep step)
    {
        ToolChangeConfig? config = Machine.ToolChange;
        if (config is not { AutoPreload: true, Preload: not null }
            || LookAhead.ProgramHasPreload(step)
            || HasPlaceholder(config.Change, "next")
            || !step.Block.Has("TOOL")
            || step.After.LastHolder is not string holder
            || !step.After.Holders.TryGetValue(holder, out HolderSnapshot? after)
            || after.SpindleTool == s_emptySpindle
            || LookAhead.NextTool(step, holder) is not ToolRef next)
        {
            return;
        }

        // preload_position = "before_first_motion": the preload waits for the first motion after the change, or for the
        // next change of the holder when that comes first, and is dropped when that change brings the tool it names
        // (machine-config 3; virtual machine 3.5, 5); a change block that moves is its own first motion.
        if (config.PreloadPosition == PreloadPosition.BeforeFirstMotion && !step.IsMotion)
        {
            if (LookAhead.BeforeFirstMotion(step, holder, next) is not BlockStep before)
            {
                return;
            }

            if (before.Index != step.Index)
            {
                _pendingPreloads.Add(new PendingPreload(next, before.Index));
                return;
            }
        }

        WritePreloadOf(next, null);
    }

    // A preload of preload_position = "before_first_motion" that waits for this block stands before its lines
    // (machine-config 3).
    private void WritePendingPreload(BlockStep step)
    {
        List<PendingPreload> due = _pendingPreloads.FindAll(pending => pending.Before == step.Index);
        _pendingPreloads.RemoveAll(pending => pending.Before == step.Index);
        foreach (PendingPreload pending in due)
        {
            WritePreloadOf(pending.Tool, null);
        }
    }

    // PRELOAD=n writes the preload template; on a machine without one PRELOAD is dropped with a WARNING
    // (machine-config 3).
    // TODO(question): D150: machine-config defines {tool} as the tool in the spindle after the block and {next} as
    // the preloaded next tool, while its preload templates write {tool} and those of the Nakamura {next}; both carry
    // the tool the block preloads, as D150 recommends, until it is answered.
    private void WritePreloadOf(ToolRef tool, TemplateValues? values)
    {
        if (Machine.ToolChange?.Preload is not string text)
        {
            Diagnostics.Warning(Step.Block, DiagnosticCodes.PreloadDropped,
                $"PRELOAD={tool} is dropped: the machine \"{Machine.Machine.Name}\" has no [tool_change] preload, so "
                + "it prepares no tool (machine-config 3).");
            return;
        }

        TemplateValues filled = values ?? new TemplateValues();
        SetTool(filled, "tool", tool);
        SetTool(filled, "next", tool);
        if (tool.Name is string name && !filled.Has("name"))
        {
            filled.Set("name", name);
        }

        WriteTemplate(text, "[tool_change] preload (machine-config 3)", filled);
    }

    // The placeholders of a change command that the virtual machine and the look-ahead give (machine-config
    // introduction, 3; D52); a value the controller set itself stays.
    private void FillToolValues(TemplateValues values, Template template, BlockStep step, string holder,
        HolderSnapshot after)
    {
        foreach (Placeholder placeholder in template.Placeholders)
        {
            if (values.Has(placeholder.Name))
            {
                continue;
            }

            switch (placeholder.Name)
            {
                case "tool":
                    SetTool(values, "tool", after.SpindleTool);
                    break;
                case "name" when after.SpindleTool.Name is string name:
                    values.Set("name", name);
                    break;
                case "offset":
                    values.Set("offset", after.OffsetCombined);
                    break;
                case "next" when NextOf(step, holder, after) is ToolRef next:
                    SetTool(values, "next", next);
                    break;
                case "kind" when KindOf(after.SpindleTool) is string kind:
                    values.Set("kind", kind);
                    break;
                case "b" when LookAhead.NextB(step) is decimal angle:
                    values.Set("b", Numbers.Format("B", angle, step.Block));
                    break;
                case "c" when LookAhead.NextToolOrientation(step) is decimal orientation:
                    values.Set("c", Numbers.Format("C", orientation, step.Block));
                    break;
                case "rpm" when RpmOf(step, holder) is decimal rpm:
                    values.Set("rpm", Numbers.FormatSpeed(rpm, step.Block));
                    break;
                case "axis":
                    values.Set("axis", ToolAxisLetter(step.After));
                    break;
            }
        }
    }

    // {next} of a change command (machine-config 3, D52): a PRELOAD that follows the TOOL block before the next motion,
    // in the same program, is folded into the command and not written again; with auto_preload, in a program without
    // PRELOAD words, the next tool of the program from the STATIC look-ahead; otherwise the preloaded next tool
    // (machine-config introduction).
    // TODO(question): with auto_preload the last change of a program has no next tool, and no document gives its
    // {next}; it is 0, the PRELOAD=0 that clears a pending preload (language 4.4), until that is answered.
    private ToolRef? NextOf(BlockStep step, string holder, HolderSnapshot after)
    {
        if (LookAhead.FollowingPreload(step, holder) is BlockStep following
            && LookAhead.PreloadOf(following, holder) is ToolRef folded)
        {
            LookAhead.Fold(following);
            return folded;
        }

        if (Machine.ToolChange is { AutoPreload: true } && !LookAhead.ProgramHasPreload(step))
        {
            return LookAhead.NextTool(step, holder) ?? s_emptySpindle;
        }

        return after.Preloaded;
    }

    // {kind}: the kind of the tool from the tool table, mapped per machine by kind_map; without the table, or for a
    // tool it does not describe, the default kind, and the tool is named in the warning block of the program
    // (machine-config 1, 3; D10, D52).
    private string? KindOf(ToolRef tool)
    {
        string? kind = Options.ToolTable?.Find(tool)?.Kind;
        if (kind is null)
        {
            List<ToolRef> tools = CurrentContext?.Program?.ToolsWithoutData ?? _toolsWithoutDataOutsidePrograms;
            if (!tools.Contains(tool))
            {
                tools.Add(tool);
            }

            kind = DefaultKind;
        }

        return Machine.ToolChange is ToolChangeConfig config && config.KindMap.TryGetValue(kind, out string? value)
            ? value
            : null;
    }

    // {rpm}: the speed of the spindle of the holder after the block, of the default spindle for a holder without one
    // (virtual machine 2.4, 3.8 rule 2).
    private decimal? RpmOf(BlockStep step, string holder)
    {
        string? spindle = Machine.FindResource(holder)?.Spindle ?? Machine.ResolveDefaultSpindle()?.Id;
        return spindle is not null && step.After.Spindles.TryGetValue(spindle, out SpindleSnapshot? state)
            ? state.Rpm
            : null;
    }

    // {axis}: the tool axis of the working plane after the block in the machine's address form (language 4.2,
    // machine-config introduction and 4).
    private string ToolAxisLetter(ChannelSnapshot after)
    {
        string toolAxis = after.Frame.Workplane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
        return Machine.ResolveAxis(toolAxis)?.Letter ?? toolAxis;
    }

    // A tool by number is the number of its placeholder; a tool by name has no number to write.
    // TODO(question): D172: a template with {tool} or {next} for a tool a program calls by name has no value to write;
    // the placeholder stays without one, which leaves the template unusable (machine-config introduction), until it is
    // answered.
    private static void SetTool(TemplateValues values, string name, ToolRef tool)
    {
        if (tool.Number is int number && !values.Has(name))
        {
            values.Set(name, number);
        }
    }
}
