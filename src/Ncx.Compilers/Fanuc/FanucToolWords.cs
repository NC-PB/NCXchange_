using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The tool and spindle words of a Fanuc block (controllers fanuc.md 4, 5, 10 rule 2; controller-mapping 3, 4;
/// language 4.4, 4.5, 4.11): after the tool change of [tool_change], G43 H and G49 for the length offset, D with G41
/// and G42, M3 with S in one block by the S binding rule, G96 and G97, the limit of RPM_MAX, the spindle modes and the
/// synchronous spindles from the machine's tables.
/// </summary>
internal static class FanucToolWords
{
    /// <summary>
    /// The key of a length offset that waits for the first block that moves the tool axis
    /// (length_offset_with_tool_axis): what the compiler has still to write, no value of the control.
    /// </summary>
    public const string PendingLength = "PENDING:H";

    // The spindle whose M code the control saw last, which a bare S belongs to (controllers fanuc.md 5).
    private const string LastSpindle = "SPINDLE:LAST";

    // Tool 0 is the empty spindle (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    /// <summary>
    /// After the change of [tool_change]: a combined OFFSET stands in the change command where its template has
    /// {offset} (machine-config 3), and under motion_code_after_tool_change the next motion writes its code (the
    /// TODO(question) of OutputFormat).
    /// </summary>
    public static void AfterChange(FanucBlock write)
    {
        // A change without {offset}, T{tool} M6 of a mill, does not write the combined offset, which then has no Fanuc
        // form and is CMP308, never dropped (language 2 rule 8; controller-mapping 3).
        if (write.Block.Find("OFFSET", null) is Word combined && ChangeWritesOffset(write))
        {
            write.Written(combined);
        }

        if (write.Machine.Format?.MotionCodeAfterToolChange == true)
        {
            write.MakeUnknown(FanucCodes.Motion);
        }
    }

    /// <summary>
    /// A length offset that still waits for the tool axis, in a line of its own without a skip mark, before a block
    /// after which a path could run without it: a LABEL, JUMP, CALL or RAW block, a skipped block that moves the tool
    /// axis or sets the length offset, the end of the program or subprogram; and before the line of a SETPOS of the
    /// tool axis, which declares
    /// the position with the offset active (language 4.2, SETPOS; 4.4, OFFSET:LEN is modal; language 2 rule 8; the
    /// TODO(question) of OutputFormat).
    /// </summary>
    public static void WriteWaitingLength(FanucBlock write)
    {
        if (write.Target.ActiveOf(PendingLength) is not string register)
        {
            return;
        }

        write.Target.Forget(PendingLength);
        write.Target.Set(FanucCodes.Length, "G43");
        write.Target.Set("H", register);
        write.Writer("G43 H" + register);
    }

    /// <summary>
    /// After TCPM=OFF the length offset of the holder stands again: G49, the TCPM_OFF of controllers fanuc.md 4, ends
    /// the length offset as well, and the holder keeps OFFSET:LEN until an explicit word (language 4.4; virtual machine
    /// 4). Under length_offset_with_tool_axis G43 H waits for the tool axis, otherwise it stands in the block, after
    /// the line of TCPM_OFF; an OFFSET:LEN of the block itself is written by AddOffsets.
    /// </summary>
    public static void RestoreLength(FanucBlock write)
    {
        int register = HolderOf(write)?.OffsetLen ?? 0;
        if (register == 0 || write.Block.Find("OFFSET", "LEN") is not null)
        {
            return;
        }

        if (write.Machine.Format?.LengthOffsetWithToolAxis == true && !write.Block.Skip)
        {
            write.Target.Set(PendingLength, register.ToString(CultureInfo.InvariantCulture));
            return;
        }

        AddLength(write, register);
    }

    /// <summary>
    /// The offsets of the block (controller-mapping 3, OFFSET:LEN and OFFSET:RAD; controllers fanuc.md 4): G43 H where
    /// the length register changes, G49 for 0, under length_offset_with_tool_axis in the first block that moves the
    /// tool axis; D with the compensation of G41 or G42 where the radius register the control has differs.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="movesToolAxis">True when the block moves the tool axis of its plane.</param>
    public static void AddOffsets(FanucBlock write, bool movesToolAxis)
    {
        if (write.OffsetsAdded)
        {
            return;
        }

        write.OffsetsAdded = true;
        HolderSnapshot? holder = HolderOf(write);
        if (write.Block.Find("OFFSET", "LEN") is Word length)
        {
            write.Written(length);
            int register = holder?.OffsetLen ?? 0;

            // The offset of a skipped block stands in its own lines, which run only where the block runs
            // (controller-mapping 1, SKIP).
            bool waits = write.Machine.Format?.LengthOffsetWithToolAxis == true && !movesToolAxis && register != 0
                && !write.Block.Skip;
            if (waits)
            {
                write.Target.Set(PendingLength, register.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                AddLength(write, register);
            }
        }
        else if (movesToolAxis && write.Target.ActiveOf(PendingLength) is string pending)
        {
            AddLength(write, int.Parse(pending, CultureInfo.InvariantCulture));
        }

        // SETPOS declares the position of the tool axis with the length offset active, the offset of an earlier block
        // and the one its own OFFSET:LEN sets first (language 4.2, SETPOS; 4.4, OFFSET:LEN is modal; 5 rule 3), so an
        // offset that waits for the tool axis stands in a line of its own before the line of G92 (the TODO(question)
        // of OutputFormat).
        if (FanucMotion.DeclaresToolAxis(write))
        {
            WriteWaitingLength(write);
        }

        if (write.Block.Find("OFFSET", "RAD") is Word radius)
        {
            write.Written(radius);
        }

        // D stands with G41 or G42 (controller-mapping 2, COMP: G41/G42/G40 with D; controllers fanuc.md 4).
        if (write.After.Motion.Comp != Compensation.Off && holder is not null && holder.OffsetRad != 0)
        {
            string register = holder.OffsetRad.ToString(CultureInfo.InvariantCulture);
            if (write.Target.Changes("D", register))
            {
                write.Main.Word("D" + register);
            }
        }
    }

    /// <summary>
    /// TCPM with its H applies the length register, and G49 ends both, so a length offset that waits for the tool axis
    /// is taken by it (controllers fanuc.md 4, G43.4 H1).
    /// </summary>
    public static void TakeWaitingLength(FanucBlock write)
    {
        write.Target.Forget(PendingLength);
        write.Target.Forget("H");
        write.Target.Set(FanucCodes.Length, write.Block.Has("TCPM", null, "ON") ? "G43.4" : "G49");
    }

    /// <summary>
    /// The settings of the spindles in lines before the main line: G96 S and G97 for CSS and VC, the limit of RPM_MAX,
    /// SPINDLE_MODE, SPINDLE_SYNC with PHASE, each from the machine's table of its role (controllers fanuc.md 4;
    /// controller-mapping 4; machine-config 5; language 4.5, 4.11).
    /// </summary>
    // TODO(question): the reader takes G96 and G97 as codes of the control for any spindle, while CSS is written from
    // VC and CSS_OFF of the role's table (machine-config 5, D154), which [spindle.SUB] of nakamura-ntjx.toml lacks; a
    // table without them is CMP010, until it is answered whether the compiler falls back to G96 S and G97.
    public static void WriteSpindleLines(FanucBlock write)
    {
        Block block = write.Block;
        foreach (Word word in block.Words)
        {
            switch (word.Key)
            {
                case "CSS":
                    Line(write, word, word.Value.ToCanonical() == "ON" ? "VC" : "CSS_OFF", VcOf(write, word.Addr));
                    write.Written(word);
                    break;
                case "VC" when !block.Has("CSS"):
                    Line(write, word, "VC", VcOf(write, word.Addr));
                    write.Written(word);
                    break;
                case "VC":
                    write.Written(word);
                    break;
                case "RPM_MAX":
                    Line(write, word, "RPM_MAX", word.Value);
                    write.Written(word);
                    break;
                case "SPINDLE_MODE":
                    WriteState(write, word, write.Machine.SpindleModeTables, "[spindle_mode." + word.Addr + "]");
                    break;
                case "SPINDLE_SYNC":
                    WriteSync(write, word);
                    break;
            }
        }
    }

    /// <summary>
    /// M3, M4, M5 with S in one block, the S of the spindle whose M code stands with it (controllers fanuc.md 5, 10
    /// rule 2): a speed written while the spindle stands waits for its start, a speed of a running spindle is written
    /// with the M code of that spindle where another spindle was selected last; ORIENT from the machine's table.
    /// </summary>
    public static void AddSpindleFunctions(FanucBlock write)
    {
        var roles = new List<string?>();
        foreach (Word word in write.Block.Words)
        {
            if (word.Key is "SPINDLE" or "RPM" or "ORIENT" && !roles.Contains(word.Addr))
            {
                roles.Add(word.Addr);
            }
        }

        foreach (string? role in roles)
        {
            AddSpindle(write, role);
        }
    }

    // The words of one spindle: S and its M code (controllers fanuc.md 5; controller-mapping 4, SPINDLE and RPM).
    private static void AddSpindle(FanucBlock write, string? role)
    {
        Block block = write.Block;
        string? id = role is null ? write.Machine.ResolveDefaultSpindle()?.Id : write.Machine.ResolveRole(role)?.Id;
        string? tableRole = TableRoleOf(write, role, id);
        FunctionTable? table = tableRole is not null && write.Machine.SpindleTables.TryGetValue(tableRole,
            out FunctionTable? found) ? found : null;
        if (id is null || !write.After.Spindles.TryGetValue(id, out SpindleSnapshot? spindle))
        {
            return;
        }

        Word? state = block.Find("SPINDLE", role);
        Word? rpm = block.Find("RPM", role);
        Word? orient = block.Find("ORIENT", role);
        foreach (Word? word in new[] { state, rpm, orient })
        {
            if (word is not null)
            {
                write.Written(word);
            }
        }

        var parts = new List<string>();
        string what = $"[spindle.{tableRole}]";
        bool runs = spindle.Direction != SpindleDirection.Off;
        string? speed = spindle.Css ? null : SpeedOf(write, rpm, spindle, table, what);
        string? direction = runs ? Code(write, table, spindle.Direction == SpindleDirection.Clockwise ? "CW" : "CCW",
            what) : null;
        if (state is not null && runs)
        {
            // A start writes its S unless the control has that speed and the block writes none (fanuc 10 rule 2).
            if (speed is not null && (rpm is not null || write.Target.ActiveOf("S:" + id) != speed))
            {
                parts.Add(speed);
            }

            AddIfWritten(parts, direction);
        }
        else if (state is not null)
        {
            AddIfWritten(parts, Code(write, table, "OFF", what));
        }
        else if (rpm is not null && runs && speed is not null)
        {
            // S binds to the spindle of the M code in the same block, otherwise to the spindle selected last
            // (controllers fanuc.md 5).
            parts.Add(speed);
            if (write.Target.ActiveOf(LastSpindle) != id)
            {
                AddIfWritten(parts, direction);
            }
        }

        if (parts.Count > 0)
        {
            FanucFunctions.Add(write, string.Join(" ", parts));
            write.Target.Set(LastSpindle, id);
            if (speed is not null && runs)
            {
                write.Target.Set("S:" + id, speed);
            }
        }

        if (orient is not null)
        {
            // The angle is a real number with its point (controllers fanuc.md 10 rule 5).
            string? template = table?.States.GetValueOrDefault("ORIENT");
            var values = new TemplateValues();
            if (orient.Value is IntegerValue or DecimalValue)
            {
                write.SetReal(values, "angle", decimal.Parse(orient.Value.ToCanonical(), CultureInfo.InvariantCulture),
                    template);
            }

            if (write.Render(template, what + " ORIENT (machine-config 5)", values) is string text)
            {
                FanucFunctions.Add(write, text);
            }
        }
    }

    // S of the RPM template of the spindle's table, "S{rpm}", or S itself where the table has none: Fanuc has one S
    // address (controllers fanuc.md 5).
    private static string? SpeedOf(FanucBlock write, Word? rpm, SpindleSnapshot spindle, FunctionTable? table,
        string what)
    {
        if (rpm?.Value is ExprValue)
        {
            return FanucExpressions.WordValue(write, "S", rpm.Value, 1m) is string expression ? "S" + expression : null;
        }

        string? template = table?.States.GetValueOrDefault("RPM");
        if (template is null)
        {
            return "S" + write.Numbers.FormatSpeed(spindle.Rpm, write.Block);
        }

        var values = new TemplateValues();
        values.Set("rpm", spindle.Rpm);
        return write.Render(template, what + " RPM (machine-config 5)", values);
    }

    // A state of the spindle's table, M3 for CW (machine-config 5).
    private static string? Code(FanucBlock write, FunctionTable? table, string state, string what)
    {
        return write.Render(table?.States.GetValueOrDefault(state), $"{what} {state} (machine-config 5)",
            new TemplateValues());
    }

    // A line of a state of the spindle's table with its {value}: G96 S{value}, G97, G92 S{value} (controllers fanuc.md
    // 4; machine-config 5).
    private static void Line(FanucBlock write, Word word, string state, Value? value)
    {
        string? tableRole = TableRoleOf(write, word.Addr, word.Addr is null
            ? write.Machine.ResolveDefaultSpindle()?.Id
            : write.Machine.ResolveRole(word.Addr)?.Id);
        FunctionTable? table = tableRole is not null
            && write.Machine.SpindleTables.TryGetValue(tableRole, out FunctionTable? found) ? found : null;
        var values = new TemplateValues();
        if (value is IntegerValue or DecimalValue)
        {
            values.Set("value", decimal.Parse(value.ToCanonical(), CultureInfo.InvariantCulture));
        }

        if (write.Render(table?.States.GetValueOrDefault(state), $"[spindle.{tableRole}] {state} (machine-config 5)",
            values) is string text)
        {
            write.Write(text);
        }
    }

    // The cutting speed of a spindle after the block: the VC word or the one the virtual machine holds.
    private static Value? VcOf(FanucBlock write, string? role)
    {
        if (write.Block.Find("VC", role) is Word vc)
        {
            return vc.Value;
        }

        string? id = role is null ? write.Machine.ResolveDefaultSpindle()?.Id : write.Machine.ResolveRole(role)?.Id;
        return id is not null && write.After.Spindles.TryGetValue(id, out SpindleSnapshot? spindle)
            && spindle.Vc is decimal speed
            ? new DecimalValue(speed, speed.ToString(CultureInfo.InvariantCulture))
            : null;
    }

    // SPINDLE_MODE:role=AXIS from [spindle_mode.ROLE] (machine-config 5, controller-mapping 4).
    private static void WriteState(FanucBlock write, Word word, IReadOnlyDictionary<string, FunctionTable> tables,
        string what)
    {
        write.Written(word);
        FunctionTable? table = word.Addr is not null && tables.TryGetValue(word.Addr, out FunctionTable? found)
            ? found
            : null;
        string state = word.Value.ToCanonical();
        if (write.Render(table?.States.GetValueOrDefault(state), $"{what} {state} (machine-config 5)",
            new TemplateValues()) is string text)
        {
            write.Write(text);
        }
    }

    // SPINDLE_SYNC from [spindle_sync]: ON for two spindles, PHASE with its angle, OFF (machine-config 5;
    // controller-mapping 4; language 4.5).
    // TODO(question): D154: the words of the function-table states and the pair of [spindle_sync] are open; the ON,
    // PHASE and OFF templates are written with {angle} from PHASE, until D154 is answered.
    private static void WriteSync(FanucBlock write, Word word)
    {
        write.Written(word);
        write.Written("PHASE");
        Word? phase = write.Block.Find("PHASE");
        string state = word.Value.ToCanonical() == "OFF" ? "OFF" : phase is null ? "ON" : "PHASE";
        string? template = write.Machine.SpindleSync?.States.GetValueOrDefault(state);
        var values = new TemplateValues();
        if (phase?.Value is IntegerValue or DecimalValue)
        {
            write.SetReal(values, "angle", decimal.Parse(phase.Value.ToCanonical(), CultureInfo.InvariantCulture),
                template);
        }

        if (write.Render(template, $"[spindle_sync] {state} (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // G43 H with the register, G49 for register 0 (controller-mapping 3; controllers fanuc.md 4).
    private static void AddLength(FanucBlock write, int register)
    {
        write.Target.Forget(PendingLength);
        if (register == 0)
        {
            FanucMotion.Change(write, FanucCodes.Length, "G49");
            write.Target.Forget("H");
            return;
        }

        write.Main.Code(FanucCodes.RankOf(FanucCodes.Length), "G43");
        write.Target.Set(FanucCodes.Length, "G43");
        string text = register.ToString(CultureInfo.InvariantCulture);
        write.Target.Set("H", text);
        write.Main.Word("H" + text);
    }

    // The holder called last, whose offsets OFFSET sets (virtual machine 2.3, 3.8 rule 2).
    private static HolderSnapshot? HolderOf(FanucBlock write)
    {
        return write.After.LastHolder is string id && write.After.Holders.TryGetValue(id, out HolderSnapshot? found)
            ? found
            : null;
    }

    // The template the framework writes the change of the block with (machine-config 3; CompilerBase.WriteToolChange):
    // unload for TOOL=0, change_preloaded for a tool preloaded already where the machine has it, change otherwise;
    // true when it has {offset}.
    private static bool ChangeWritesOffset(FanucBlock write)
    {
        ToolChangeConfig? config = write.Machine.ToolChange;
        if (config is null || write.After.LastHolder is not string id
            || !write.After.Holders.TryGetValue(id, out HolderSnapshot? after))
        {
            return false;
        }

        bool preloaded = write.Before.Holders.TryGetValue(id, out HolderSnapshot? before)
            && before.Preloaded == after.SpindleTool;
        string? text = after.SpindleTool == s_emptySpindle ? config.Unload
            : preloaded && config.ChangePreloaded is not null ? config.ChangePreloaded
            : config.Change;
        return text is not null
            && write.TemplateOf(text).Placeholders.Any(placeholder => placeholder.Name == "offset");
    }

    // The role of the table of a spindle: the role the word names, else the role of the default spindle (virtual
    // machine 3.8 rule 2).
    private static string? TableRoleOf(FanucBlock write, string? role, string? id)
    {
        if (role is not null)
        {
            return role;
        }

        foreach (KeyValuePair<string, string> named in write.Machine.Roles)
        {
            if (named.Value == id && write.Machine.SpindleTables.ContainsKey(named.Key))
            {
                return named.Key;
            }
        }

        return null;
    }

    private static void AddIfWritten(List<string> parts, string? text)
    {
        if (text is not null)
        {
            parts.Add(text);
        }
    }
}
