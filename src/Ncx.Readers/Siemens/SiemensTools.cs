using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The tool words of a SINUMERIK block (controllers siemens.md 5, 11 rule 3; controller-mapping 3; language 4.4;
/// virtual machine 3.5): T1, T=1 and T="NAME" select a tool, which on a mill that changes with M6 is the preload and M6
/// the change, and on a turret the change itself; T2=5 selects for spindle 2; D1 is the cutting edge, OFFSET=1, and D0
/// cancels it, OFFSET=0, where the source writes it (D7).
/// </summary>
internal static class SiemensTools
{
    /// <summary>
    /// Reads the tool selection, the change and the cutting edge of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadChange(block);
        if (!block.Draft.IsRaw)
        {
            ReadEdge(block);
        }
    }

    // On a mill with a magazine M6 changes the tool T selected (manual 2.4.2), so T alone is the preload and T with M6
    // the change; on a turret T changes directly (manual 2.4.1); the machine file says which by the magazine of the
    // default holder (controller-mapping 3, PRELOAD and TOOL).
    private static void ReadChange(SiemensBlock block)
    {
        SourceWord? change = block.FindCode("M6");
        SourceWord? tool = block.Find("T");
        SourceWord? spindleTool = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address.Length > 1 && word.Address[0] == 'T' && SiemensNumbers.WholeNumber(word.Address[1..]) is
                not null && word.Text.Length > 0)
            {
                spindleTool = word;
            }
        }

        if (change is null && tool is null && spindleTool is null)
        {
            return;
        }

        string? holder = null;
        if (spindleTool is not null)
        {
            holder = HolderOfSpindle(block, int.Parse(spindleTool.Address[1..],
                System.Globalization.CultureInfo.InvariantCulture));
            tool = spindleTool;
            if (holder is null)
            {
                block.MarkRead(spindleTool);
                block.Draft.KeepAsRaw($"{spindleTool.Address}= selects for a spindle whose holder the machine file "
                    + "names "
                    + "no role for (controller-mapping 3, PRELOAD)");
                return;
            }
        }

        ToolRef? selected = null;
        if (tool is not null)
        {
            block.MarkRead(tool);
            selected = ToolOf(tool);
            if (selected is null)
            {
                block.Draft.KeepAsRaw($"T{tool.Text} names no tool by number or name (controllers siemens.md 5)");
                return;
            }
        }

        if (change is not null)
        {
            block.MarkRead(change);
        }

        if (!Changes(block))
        {
            Write(block, "TOOL", holder, selected);
            return;
        }

        if (change is null)
        {
            Preload(block, holder, selected!.Value);
            return;
        }

        if (selected is ToolRef named)
        {
            Write(block, "TOOL", holder, named);
            return;
        }

        ChangeToPreload(block, holder);
    }

    // M6 alone changes to the tool T selected before, TOOL=n from the source-side state; where the reader does not
    // know it, a caller's T in a subprogram, the bare TOOL changes to the tool the virtual machine holds preloaded
    // (controller-mapping 3, TOOL; virtual machine 3.5).
    private static void ChangeToPreload(SiemensBlock block, string? holder)
    {
        SiemensFacts facts = block.Facts;
        if (!facts.IsUnknown(SiemensFacts.Preload) && facts.PreloadedTool is ToolRef preloaded)
        {
            Write(block, "TOOL", holder, preloaded);
            return;
        }

        block.Draft.AddState("TOOL", holder, NoValue.Instance);
        facts.PreloadedTool = null;
        facts.Unknown.Remove(SiemensFacts.Preload);
        block.State.Preloaded = null;
    }

    // T alone on a mill is the preload (controllers siemens.md 5). T0 unloads: T0 and T=0 select the empty place,
    // PRELOAD=0, which clears a pending preload (language 4.4), and the reader remembers tool 0 as the selection, so
    // that the M6 after it, the two-line form of every change in the corpus, changes to it, TOOL=0 (controller-mapping
    // 3, TOOL=4 and TOOL=0). Where the program ends after it without an M6 or another T, T0 is the unload itself,
    // TOOL=0, as controller-mapping 3 reads the T=0 the Hermle writes before M30.
    // TODO(question): the documents read T0 M6 and the Hermle's T=0 before M30 as TOOL=0 (controller-mapping 3) and T
    // alone on a mill that changes with M6 as the preload (controllers siemens.md 5), but do not say what a T0 alone
    // does that another T or the return of a subprogram follows; it is PRELOAD=0 there.
    private static void Preload(SiemensBlock block, string? holder, ToolRef tool)
    {
        if (tool.Number == 0 && EndsBeforeAChange(block))
        {
            Write(block, "TOOL", holder, tool);
            return;
        }

        block.Draft.AddState("PRELOAD", holder, ValueOf(tool));
        block.Facts.PreloadedTool = tool;
        block.Facts.Unknown.Remove(SiemensFacts.Preload);
        block.State.Preloaded = tool.Number == 0 ? null : tool;
    }

    // The program ends after the block, M30, M2, or M17 in a main program (controllers siemens.md 1), before an M6 or
    // another T changes or selects a tool.
    private static bool EndsBeforeAChange(SiemensBlock block)
    {
        SiemensUnit? unit = block.Unit;
        int index = unit?.Blocks.IndexOf(block.Source) ?? -1;
        if (unit is null || unit.Kind != SiemensUnitKind.Program || index < 0)
        {
            return false;
        }

        for (int next = index; next < unit.Blocks.Count; next++)
        {
            foreach (SourceWord word in unit.Blocks[next].Words)
            {
                string? code = SiemensBlock.CodeOf(word);
                if (code is "M30" or "M2" or "M17")
                {
                    return true;
                }

                if (next > index && (code == "M6" || IsToolWord(word)))
                {
                    return false;
                }
            }
        }

        return false;
    }

    // T, T=, T="NAME" and T2= select a tool (controllers siemens.md 5).
    private static bool IsToolWord(SourceWord word)
    {
        return word.Address == "T" || (word.Address.Length > 1 && word.Address[0] == 'T'
            && SiemensNumbers.WholeNumber(word.Address[1..]) is not null && word.Text.Length > 0);
    }

    // TOOL=n puts n into the spindle; a preload of n is consumed (virtual machine 3.5).
    // TODO(question): D132, whether TOOL=n consumes a preload of another tool; as the virtual machine keeps it, the
    // reader does.
    private static void Write(SiemensBlock block, string key, string? holder, ToolRef? tool)
    {
        if (tool is not ToolRef selected)
        {
            return;
        }

        block.Draft.AddState(key, holder, ValueOf(selected));
        SiemensFacts facts = block.Facts;
        if (facts.PreloadedTool is ToolRef preloaded && preloaded == selected)
        {
            facts.PreloadedTool = null;
            block.State.Preloaded = null;
        }
    }

    // D1 to D8 select the cutting edge of the active tool with its length and radius, one register, OFFSET=n; D0
    // cancels the offsets, written in the retract blocks as OFFSET=0 (controller-mapping 3, OFFSET; D7).
    private static void ReadEdge(SiemensBlock block)
    {
        if (block.Take("D") is not SourceWord edge)
        {
            return;
        }

        if (edge.Number is not decimal number || number < 0 || number != decimal.Truncate(number)
            || edge.Expression is not null)
        {
            block.Draft.KeepAsRaw($"D{edge.Text} names no cutting edge by number (controller-mapping 3, OFFSET)");
            return;
        }

        block.Draft.AddState("OFFSET", null, new IntegerValue((long)number, edge.Text.TrimStart('0').PadLeft(1, '0')));
    }

    // A turret changes with T itself; a holder with a magazine changes with M6 (manual 2.4.1 and 2.4.2).
    private static bool Changes(SiemensBlock block)
    {
        return block.Machine.ResolveDefaultHolder()?.Magazine ?? true;
    }

    // The holder whose spindle T2= selects for: the holder role of the holder of spindle 2 (controller-mapping 3,
    // PRELOAD).
    private static string? HolderOfSpindle(SiemensBlock block, int spindle)
    {
        MachineConfig machine = block.Machine;
        string? spindleRole = SiemensSpindles.RoleOf(block, spindle);
        string? spindleId = spindleRole is null ? null : machine.ResolveRole(spindleRole)?.Id;
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            ResourceDef? holder = machine.FindResource(role.Value);
            if (holder is { Type: ResourceType.ToolHolder } && holder.Spindle == spindleId && spindleId is not null)
            {
                return role.Key;
            }
        }

        return null;
    }

    // T1, T=1, T="NAME": a tool by number, or by name, which keeps its case (controllers siemens.md 1).
    private static ToolRef? ToolOf(SourceWord word)
    {
        if (SiemensArguments.StringOf(word.Text) is string name && name.Length > 0)
        {
            return new ToolRef(name);
        }

        return word.Number is decimal number && number >= 0 && number == decimal.Truncate(number)
            && number <= int.MaxValue
            ? new ToolRef((int)number)
            : null;
    }

    private static Value ValueOf(ToolRef tool)
    {
        if (tool.Name is string name)
        {
            return new StringValue(name);
        }

        int number = tool.Number ?? 0;
        return new IntegerValue(number, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
