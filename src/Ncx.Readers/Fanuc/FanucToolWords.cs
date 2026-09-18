using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The tool and spindle words of a Fanuc block (controllers fanuc.md 5, 9 rule 2; controller-mapping 3, 4; language
/// 4.4, 4.5; virtual machine 3.5): on a mill T alone is PRELOAD, M6 is TOOL with the preloaded tool, T4 M6 is TOOL=4;
/// on a lathe T0656 is TOOL=6 OFFSET=56; a builder's change macro comes through the templates of [tool_change]; G43 H
/// is OFFSET:LEN, G49 OFFSET:LEN=0, D OFFSET:RAD, where the source has them (D7); S is RPM, or VC under G96, of the
/// spindle of the M code in the same block, else of the spindle selected last (the S binding rule).
/// </summary>
internal static class FanucToolWords
{
    private static readonly IdentValue s_on = new("ON");
    private static readonly IdentValue s_off = new("OFF");

    /// <summary>
    /// Reads the tool change, the offsets and the speed of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (!ReadBuilderChange(block))
        {
            ReadChange(block);
        }

        if (!block.Draft.IsRaw)
        {
            ReadOffsets(block);
        }

        if (!block.Draft.IsRaw)
        {
            ReadSpeed(block);
        }
    }

    // A builder that wraps the change in a G macro, Nakamura G340 T0101. A02. and G341 T02., Mori Seiki G361 B0 D1., is
    // read through the templates of [tool_change]: {tool} and {offset} are the change, {next} the preload, and a change
    // without {tool} takes the preloaded tool (controller-mapping 3; language 4.4; D91).
    private static bool ReadBuilderChange(FanucBlock block)
    {
        ToolChangeConfig? config = block.Machine.ToolChange;
        if (IsBuilderMacro(config?.Change) && FanucTemplates.TryMatch(block, config!.Change, out TemplateValues change))
        {
            if (change.TryGetNumber("tool", out decimal tool))
            {
                Change(block, decimal.ToInt32(tool));
            }
            else
            {
                ChangeToPreload(block);
            }

            if (change.TryGetNumber("offset", out decimal offset))
            {
                block.Draft.AddState("OFFSET", null, Integer(decimal.ToInt32(offset)));
            }

            if (change.TryGetNumber("next", out decimal next))
            {
                Preload(block, decimal.ToInt32(next));
            }

            return true;
        }

        if (IsBuilderMacro(config?.Preload)
            && FanucTemplates.TryMatch(block, config!.Preload, out TemplateValues preload)
            && (preload.TryGetNumber("next", out decimal preloaded) || preload.TryGetNumber("tool", out preloaded)))
        {
            Preload(block, decimal.ToInt32(preloaded));
            return true;
        }

        return false;
    }

    // A template that begins with a G code is a builder's macro; the plain T and M6 forms are the Fanuc words below.
    private static bool IsBuilderMacro(string? template)
    {
        return template is not null && template.Length > 1 && template[0] == 'G' && char.IsAsciiDigit(template[1]);
    }

    // T and M6 per machine kind (controllers fanuc.md 5, controller-mapping 3).
    private static void ReadChange(FanucBlock block)
    {
        SourceWord? change = block.FindCode("M6");
        SourceWord? tool = block.Find("T");
        if (change is null && tool is null)
        {
            return;
        }

        // T5 after the M6 in the same block is illegal on most controls; the reader reports it (controller-mapping 3).
        foreach (SourceWord word in block.Unread())
        {
            if (change is not null && word.Address == "T" && block.IndexOf(word) > block.IndexOf(change))
            {
                block.Diagnostics.Error(block.Line, DiagnosticCodes.FanucToolAfterChange,
                    $"T{word.Text} after M6 in the same block is illegal on most controls; the block is kept as RAW "
                    + "(controller-mapping 3).");
                block.Draft.KeepAsRaw("a T word after M6 in the same block");
                return;
            }
        }

        string? digits = tool is null ? null : ToolDigits(tool);
        if (tool is not null && digits is null)
        {
            block.Draft.KeepAsRaw($"T{tool.Text} names no tool number");
            return;
        }

        if (change is not null)
        {
            block.MarkRead(change);
            if (tool is null)
            {
                ChangeToPreload(block);
                return;
            }
        }

        block.MarkRead(tool!);
        if (block.IsLathe)
        {
            Turret(block, digits!);
        }
        else if (change is not null)
        {
            Change(block, int.Parse(digits!, NumberStyles.None, CultureInfo.InvariantCulture));
        }
        else
        {
            Preload(block, int.Parse(digits!, NumberStyles.None, CultureInfo.InvariantCulture));
        }
    }

    // On a lathe with a turret T0404 indexes station 4 and activates offset 4 in one word, T0656 is station 6 with
    // offset 56, T0100 cancels the offset of station 1; six digits T001001 on 120-tool ATCs. The reader emits the
    // offset change and no motion (controllers fanuc.md 5; controller-mapping 3; language 4.4).
    // TODO(question): on a lathe the T word is the turret form (fanuc 5), while the Mori Seiki tool spindle takes T9001
    // as PRELOAD=9001 (controller-mapping 3, D91); the documents do not say how the reader tells the two apart, so
    // every T word of a lathe is the turret form until D219 is answered. A T word shorter than four digits is padded,
    // T101 as T0101 (D153).
    private static void Turret(FanucBlock block, string digits)
    {
        int width = digits.Length <= 4 ? 4 : 6;
        if (digits.Length > width)
        {
            block.Draft.KeepAsRaw($"T{digits} is no turret station with its offset");
            return;
        }

        int whole = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        int split = width == 4 ? 100 : 1000;
        int station = whole / split;
        int offset = whole % split;
        block.Draft.AddState("TOOL", null, Integer(station));
        block.Draft.AddState("OFFSET", null, Integer(offset));
    }

    // TOOL=n puts n into the spindle; a preload of n is consumed (virtual machine 3.5).
    private static void Change(FanucBlock block, int tool)
    {
        block.Draft.AddState("TOOL", null, Integer(tool));
        if (block.State.Preloaded is ToolRef preloaded && preloaded.Number == tool)
        {
            block.State.Preloaded = null;
        }
    }

    // M6 alone changes to the preloaded tool, TOOL=n with n from the source-side state; M6 without any preload is an
    // ERROR in the source (language 4.4; controller-mapping 3), and the bare TOOL is written as the source has it. A
    // subprogram changes to the tool its caller preloaded, a caller to the one its subprogram preloaded (virtual
    // machine 3.9): where the reader does not know it (FanucCallerState), the bare TOOL changes to the tool the virtual
    // machine holds preloaded there (virtual machine 3.5).
    private static void ChangeToPreload(FanucBlock block)
    {
        if (block.Fanuc.Unknowns.Preload)
        {
            block.Draft.AddState("TOOL", null, NoValue.Instance);
            block.State.Preloaded = null;
            block.Fanuc.Unknowns.Preload = false;
            return;
        }

        if (block.State.Preloaded is ToolRef preloaded && preloaded.Number is int number)
        {
            block.Draft.AddState("TOOL", null, Integer(number));
            block.State.Preloaded = null;
            return;
        }

        block.Diagnostics.Error(block.Line, DiagnosticCodes.FanucChangeWithoutPreload,
            "M6 changes to the preloaded tool and no tool is preloaded (controller-mapping 3).");
        block.Draft.AddState("TOOL", null, NoValue.Instance);
    }

    // T5 alone prepares tool 5, PRELOAD=5; T0 clears the preload (controllers fanuc.md 5; language 4.4).
    private static void Preload(FanucBlock block, int tool)
    {
        block.Draft.AddState("PRELOAD", null, Integer(tool));
        block.State.Preloaded = tool == 0 ? null : new ToolRef(tool);
        block.Fanuc.Unknowns.Preload = false;
    }

    // G43 H is the length offset, G49 cancels it, D is the radius offset; the words stay where the source has them (D7;
    // controller-mapping 3). G44, the length offset subtracted, has no NCX word and stays RAW. On a lathe of system A H
    // is the incremental C and no offset.
    private static void ReadOffsets(FanucBlock block)
    {
        if (block.TakeCode("G44"))
        {
            block.Draft.KeepAsRaw("G44, the length offset subtracted, has no NCX word (controller-mapping 3)");
            return;
        }

        block.TakeCode("G43");
        if (block.TakeCode("G49"))
        {
            block.Draft.AddState("OFFSET", "LEN", Integer(0));
        }

        if (block.Find("H") is SourceWord length && FanucAxes.Of(length, block) is null)
        {
            block.MarkRead(length);
            Register(block, length, "LEN");
        }

        if (block.Find("D") is SourceWord radius)
        {
            block.MarkRead(radius);
            Register(block, radius, "RAD");
        }
    }

    private static void Register(FanucBlock block, SourceWord word, string kind)
    {
        if (word.Number is decimal number && word.Expression is null && number >= 0 && number <= int.MaxValue
            && number == decimal.Truncate(number))
        {
            block.Draft.AddState("OFFSET", kind, Integer(decimal.ToInt32(number)));
            return;
        }

        block.Draft.KeepAsRaw($"{word.Address}{word.Text} names no offset register");
    }

    // Fanuc has one S word: it belongs to the spindle whose M code stands in the same block, otherwise to the spindle
    // selected last; under G96 it is the cutting speed VC, else the speed RPM; G96 and G97 switch CSS (controllers
    // fanuc.md 4, 5; controller-mapping 4, the S binding rule; language 4.5, 4.11). With M19 the S is the angle of the
    // orientation, which FanucBuilder reads.
    private static void ReadSpeed(FanucBlock block)
    {
        bool constant = block.TakeCode("G96");
        bool revolutions = block.TakeCode("G97");
        SourceWord? speed = SpindleFunction(block, "ORIENT") is null ? block.Take("S") : null;
        if (!constant && !revolutions && speed is null)
        {
            return;
        }

        // The spindle of a bare S and whether S is a speed or a cutting speed are the caller's in a subprogram and the
        // ones its subprogram left in a caller (virtual machine 3.9); where the reader does not know them, the block
        // stays RAW (FanucCallerState).
        FanucFunction? spindle = SpindleFunction(block, "SPINDLE");
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        if (unknown.Spindle && spindle is null)
        {
            block.Draft.KeepAsRaw(
                FanucUnknowns.Reason("the spindle selected last, which a bare S belongs to (controller-mapping 4)"));
            return;
        }

        if (unknown.Css && speed is not null && !constant && !revolutions)
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("G96 or G97, which make S a cutting speed or a speed"));
            return;
        }

        string? role = spindle?.SpindleRole ?? block.State.LastSpindle;
        string? address = FanucFunctions.SpindleAddress(block, role);
        if (constant || revolutions)
        {
            block.Fanuc.CssOn = constant;
            unknown.Css = false;
            block.Draft.AddState("CSS", address, constant ? s_on : s_off);
        }

        if (speed is null || FanucMacro.ValueOf(block, speed) is not Value value)
        {
            return;
        }

        block.Draft.AddState(block.Fanuc.CssOn ? "VC" : "RPM", address, value);
        if (!block.Fanuc.CssOn && FanucNumbers.NumberOf(value) is decimal rpm)
        {
            block.Fanuc.Rpm[role ?? ""] = rpm;
        }
    }

    // The function of a spindle M code of the block with this key, SPINDLE or ORIENT; null when the block has none.
    private static FanucFunction? SpindleFunction(FanucBlock block, string key)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "M" && FanucFunctions.Of(block, word, out _) is FanucFunction function
                && function.Key == key)
            {
                return function;
            }
        }

        return null;
    }

    // The digits of a T word, T0101. as 0101; null for a T word that is no whole number.
    private static string? ToolDigits(SourceWord word)
    {
        string text = word.Text.TrimEnd('.');
        if (word.Expression is not null || text.Length == 0)
        {
            return null;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return null;
            }
        }

        return text;
    }

    private static IntegerValue Integer(int number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }
}
