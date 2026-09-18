using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The spindle words of a SINUMERIK block (controllers siemens.md 5, 12 rule 3; controller-mapping 4; machine-config 5;
/// language 4.5, 4.11) through the templates of [spindle.ROLE], [spindle_mode.ROLE] and [spindle_sync]: S and M3 of the
/// master spindle, S2= and M2=3 of the others; SETMS(n) in a block of its own where a template addresses the master
/// spindle and the master spindle changes, G96 among them, which acts on the master spindle; COUPON and COUPOF.
/// </summary>
internal static partial class SiemensSpindles
{
    // The target key of the master spindle, the number SETMS selected (controllers siemens.md 5).
    private const string Master = "SETMS";

    // The master spindle the control is configured with, which SETMS alone selects again (controllers siemens.md 5).
    private const string Configured = "";

    // The spindle words of language 4.5 and 4.11 that the tables of a role write.
    private static readonly string[] s_spindleWords = ["SPINDLE", "RPM", "CSS", "VC", "RPM_MAX", "ORIENT"];

    /// <summary>
    /// Writes the spindle words of the block: SETMS where it is needed in a block of its own, the procedures of the
    /// templates in blocks of their own, the other words into the main line.
    /// </summary>
    public static void Write(SiemensBlock write)
    {
        var texts = new List<SpindleText>();
        foreach (string key in s_spindleWords)
        {
            foreach (Word word in write.Block.Words)
            {
                if (word.Key == key)
                {
                    write.Written(word);
                    AddWord(write, word, texts);
                }
            }
        }

        foreach (Word word in write.Block.Words)
        {
            if (word.Key == "SPINDLE_MODE")
            {
                write.Written(word);
                AddMode(write, word, texts);
            }
        }

        AddSync(write, texts);
        WriteTexts(write, texts);
    }

    /// <summary>
    /// The number n of a spindle as S{n}= and M{n}=3 address it: the extension its templates write, else the digits of
    /// its resource id (controllers siemens.md 1, 5); null when neither gives one.
    /// </summary>
    // TODO(question): machine-config names no number for the spindle whose templates write the plain S and M3, the
    // configured master spindle, which SETMS(n) needs (the P5-01 question of the reader); the number is taken from its
    // resource id S{n}, as the example files name their spindles and as the reader does, until that is answered.
    public static int? NumberOf(SiemensBlock write, string role)
    {
        if (write.Machine.SpindleTables.TryGetValue(role, out FunctionTable? table))
        {
            foreach (string template in table.States.Values)
            {
                Match extension = Extension().Match(template);
                if (extension.Success)
                {
                    return int.Parse(extension.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }
        }

        string? id = write.Machine.ResolveRole(role)?.Id;
        return id is not null && Digits().Match(id) is { Success: true } digits
            ? int.Parse(digits.Value, CultureInfo.InvariantCulture)
            : null;
    }

    // One spindle word through the table of its role.
    private static void AddWord(SiemensBlock write, Word word, List<SpindleText> texts)
    {
        string? role = RoleOf(write, word.Addr);
        FunctionTable? table = role is null ? null : write.Machine.SpindleTables.GetValueOrDefault(role);
        string what = $"[spindle.{role ?? "?"}]";
        SpindleSnapshot? spindle = StateOf(write, role);
        var values = new TemplateValues();
        switch (word.Key)
        {
            case "SPINDLE":
                string state = word.Value.ToCanonical();
                Add(write, texts, role, table?.States.GetValueOrDefault(state), $"{what} {state}", values);
                break;
            case "RPM":
                // Under CSS the spindle follows VC and RPM is ignored (language 4.11): on the control S would be the
                // cutting speed, so the speed is written when CSS goes off.
                if (spindle is { Css: true })
                {
                    break;
                }

                SetNumber(write, values, "rpm", word.Value, "S");
                Add(write, texts, role, table?.States.GetValueOrDefault("RPM"), what + " RPM", values);
                break;
            case "CSS":
                AddSurfaceSpeed(write, texts, word, role, table, spindle);
                break;
            case "VC" when spindle is { Css: true } && !write.Block.Has("CSS"):
                SetNumber(write, values, "value", word.Value, "S");
                Add(write, texts, role, table?.States.GetValueOrDefault("VC"), what + " VC", values);
                break;
            case "RPM_MAX":
                SetNumber(write, values, "value", word.Value, "S");
                Add(write, texts, role, table?.States.GetValueOrDefault("RPM_MAX"), what + " RPM_MAX", values);
                break;
            case "ORIENT":
                SetNumber(write, values, "angle", word.Value, "C");
                Add(write, texts, role, table?.States.GetValueOrDefault("ORIENT"), what + " ORIENT", values);
                break;
        }
    }

    // CSS=ON is the VC template, G96 S with the cutting speed; CSS=OFF the CSS_OFF template, G97, and the speed of RPM
    // the spindle turns at again (language 4.11; controller-mapping 4, CSS).
    private static void AddSurfaceSpeed(SiemensBlock write, List<SpindleText> texts, Word css, string? role,
        FunctionTable? table, SpindleSnapshot? spindle)
    {
        string what = $"[spindle.{role ?? "?"}]";
        var values = new TemplateValues();
        if (spindle is { Css: true })
        {
            if (write.Block.Find("VC", css.Addr) is Word vc)
            {
                SetNumber(write, values, "value", vc.Value, "S");
            }
            else if (spindle.Vc is decimal known)
            {
                values.Set("value", write.Numbers.FormatSpeed(known, write.Block));
            }

            Add(write, texts, role, table?.States.GetValueOrDefault("VC"), what + " VC", values);
            return;
        }

        Add(write, texts, role, table?.States.GetValueOrDefault("CSS_OFF"), what + " CSS_OFF", values);
        if (spindle is { Rpm: > 0m } && table?.States.GetValueOrDefault("RPM") is string rpm)
        {
            var speed = new TemplateValues();
            speed.Set("rpm", write.Numbers.FormatSpeed(spindle.Rpm, write.Block));
            Add(write, texts, role, rpm, what + " RPM", speed);
        }
    }

    // SPINDLE_MODE:role=AXIS or SPINDLE through [spindle_mode.role] (language 4.5; controller-mapping 4).
    private static void AddMode(SiemensBlock write, Word word, List<SpindleText> texts)
    {
        string? role = RoleOf(write, word.Addr);
        FunctionTable? table = role is null ? null : write.Machine.SpindleModeTables.GetValueOrDefault(role);
        string state = word.Value.ToCanonical();
        Add(write, texts, role, table?.States.GetValueOrDefault(state), $"[spindle_mode.{role ?? "?"}] {state}",
            new TemplateValues());
    }

    // SPINDLE_SYNC=MAIN,SUB through [spindle_sync]: ON, PHASE with {angle}, OFF (language 4.5; machine-config 5;
    // controller-mapping 4, SPINDLE_SYNC).
    // TODO(question): D154: [spindle_sync] names no pair; the template couples the default spindle, leading, with the
    // other work spindle, as D154 recommends, and a SPINDLE_SYNC of another pair is an ERROR, until D154 is answered.
    private static void AddSync(SiemensBlock write, List<SpindleText> texts)
    {
        if (write.Block.Find("SPINDLE_SYNC") is not Word sync)
        {
            return;
        }

        write.Written(sync);
        write.Written("PHASE");
        FunctionTable? table = write.Machine.SpindleSync;
        if (sync.Value.ToCanonical() == "OFF")
        {
            Add(write, texts, null, table?.States.GetValueOrDefault("OFF"), "[spindle_sync] OFF", new TemplateValues());
            return;
        }

        if (!IsDefaultPair(write, sync.Value))
        {
            write.Error(DiagnosticCodes.SiemensSpindlePairNotWritable,
                $"{sync.ToCanonical()} couples another pair than [spindle_sync] of the machine, which couples the "
                + "default spindle with the other work spindle (machine-config 5; language 4.5; D154).");
            return;
        }

        var values = new TemplateValues();
        if (write.Block.Find("PHASE") is Word phase)
        {
            SetNumber(write, values, "angle", phase.Value, "C");
            Add(write, texts, null, table?.States.GetValueOrDefault("PHASE"), "[spindle_sync] PHASE", values);
            return;
        }

        Add(write, texts, null, table?.States.GetValueOrDefault("ON"), "[spindle_sync] ON", values);
    }

    // The first role is the default spindle and the second another work spindle (D154).
    private static bool IsDefaultPair(SiemensBlock write, Value pair)
    {
        if (pair is not ListValue list || list.Items.Count != 2)
        {
            return false;
        }

        MachineConfig machine = write.Machine;
        ResourceDef? leading = machine.ResolveRole(list.Items[0]);
        ResourceDef? following = machine.ResolveRole(list.Items[1]);
        return leading is not null && leading.Id == machine.ResolveDefaultSpindle()?.Id
            && following is { Type: ResourceType.WorkSpindle } && following.Id != leading.Id;
    }

    // A rendered template with the master spindle it needs: n where it addresses the master spindle, a plain S, M3,
    // SPOS=, LIMS= or G96, else none.
    private static void Add(SiemensBlock write, List<SpindleText> texts, string? role, string? template, string what,
        TemplateValues values)
    {
        if (write.Render(template, what + " (machine-config 5)", values) is not string text || text.Length == 0)
        {
            return;
        }

        // The configured master spindle needs no number; another spindle is selected by its number with SETMS(n).
        string? master = null;
        if (role is not null && AddressesMaster(text))
        {
            int? number = role == ConfiguredRole(write) ? null : NumberOf(write, role);
            if (role != ConfiguredRole(write) && number is null)
            {
                write.Error(DiagnosticCodes.SiemensSpindleNumberUnknown,
                    $"{text} acts on the master spindle, and neither the templates of the role {role} nor its resource "
                    + "give its number for SETMS (controllers siemens.md 5, 12 rule 3).");
                return;
            }

            master = number?.ToString(CultureInfo.InvariantCulture) ?? Configured;
        }

        texts.Add(new SpindleText(text, master));
    }

    // SETMS(n) in a block of its own where the master spindle changes (controllers siemens.md 5, 12 rule 3), then the
    // procedures in blocks of their own and the rest in the main line.
    private static void WriteTexts(SiemensBlock write, List<SpindleText> texts)
    {
        string? needed = null;
        foreach (SpindleText text in texts)
        {
            if (text.Master is not string master)
            {
                continue;
            }

            if (needed is string other && other != master)
            {
                write.Error(DiagnosticCodes.SiemensSpindleNumberUnknown,
                    "The block needs two master spindles at once, and SETMS selects one in a block of its own "
                    + "(controllers siemens.md 5, 12 rule 3).");
                return;
            }

            needed = master;
        }

        if (needed is string spindle)
        {
            SelectMaster(write, spindle);
        }

        foreach (SpindleText text in texts)
        {
            if (text.Text.Contains('(', StringComparison.Ordinal) || text.Text.Contains('\n', StringComparison.Ordinal))
            {
                write.Write(text.Text);
                continue;
            }

            AddToMain(write, text.Text);
        }
    }

    /// <summary>
    /// The words of a template in the main line: a G code with the codes, an M function with the functions, the other
    /// words with the spindle words; G96 switches G95 on, G961 the feed per minute, G97 the surface speed off
    /// (controllers siemens.md 2, 5).
    /// </summary>
    // TODO(question): machine-config 5 gives CSS=ON the VC template, G96 S{value} on the example machines, and G96
    // switches G95 on, while controller-mapping 4 gives G961 for the constant surface speed with the feed per minute
    // and G94 would switch it off (siemens 2, group 15); no document says whether the compiler may write G961 for the
    // G96 of a template. G96 and G961 of a template are written as the code of the feed type the block leaves, G96 per
    // revolution and G961 per minute, and a FEED_MODE under the surface speed as the other code alone, until that is
    // answered.
    public static void AddToMain(SiemensBlock write, string text)
    {
        foreach (string template in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string word = template is "G96" or "G961" ? SiemensMotion.SurfaceSpeedCode(write) : template;
            if (write.Main.Holds(word))
            {
                continue;
            }

            if (word.Length > 1 && word[0] == 'G' && char.IsAsciiDigit(word[1]))
            {
                write.Main.Code(SiemensLine.KeywordRank, word);
                if (SiemensMotion.IsFeedTypeCode(word))
                {
                    SiemensMotion.FeedTypeSwitched(write, word);
                }
            }
            else if (word.Length > 1 && word[0] == 'M' && char.IsAsciiDigit(word[1]))
            {
                write.Main.Function(word);
            }
            else
            {
                write.Main.Word(SiemensLine.SpindleRank, word);
            }
        }
    }

    /// <summary>
    /// A program starts with the configured master spindle, which SETMS alone selects again (controllers siemens.md 5);
    /// a subprogram is written from an unknown master spindle (D99).
    /// </summary>
    public static void BeginProgram(SiemensBlock write)
    {
        write.Target.Set(Master, Configured);
    }

    // SETMS(n) makes spindle n the master spindle, SETMS alone returns to the configured one; in a block of its own
    // (controllers siemens.md 5, 12 rule 3). Where the master spindle is unknown, at the start of a subprogram (D99),
    // after a RAW or after a label, it is selected again, whichever it may be. A machine with one spindle has no other
    // master spindle (machine-config 4, [[resource]]), so its configured one is never selected again.
    private static void SelectMaster(SiemensBlock write, string wanted)
    {
        if (write.ActiveOf(Master) == wanted)
        {
            return;
        }

        if (wanted == Configured && SpindleCount(write.Machine) <= 1)
        {
            write.Target.Set(Master, Configured);
            return;
        }

        write.Write(wanted == Configured ? "SETMS" : "SETMS(" + wanted + ")");
        write.Target.Set(Master, wanted);
    }

    // The work spindles and tool spindles of the machine (machine-config 4).
    private static int SpindleCount(MachineConfig machine)
    {
        int count = 0;
        foreach (ResourceDef resource in machine.Resources)
        {
            if (resource.Type is ResourceType.WorkSpindle or ResourceType.ToolSpindle)
            {
                count++;
            }
        }

        return count;
    }

    // The role of the configured master spindle, the one whose templates write the plain M3, else the role of the
    // default spindle (controllers siemens.md 5; controller-mapping 4).
    private static string? ConfiguredRole(SiemensBlock write)
    {
        MachineConfig machine = write.Machine;
        foreach (KeyValuePair<string, FunctionTable> table in machine.SpindleTables)
        {
            if (table.Value.States.GetValueOrDefault("CW") == "M3")
            {
                return table.Key;
            }
        }

        return RoleOf(write, null);
    }

    // A template addresses the master spindle when it writes S, an M function, SPOS= or LIMS= without the number of a
    // spindle, or switches the constant surface speed (controllers siemens.md 5).
    private static bool AddressesMaster(string text)
    {
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (MasterWord().IsMatch(word))
            {
                return true;
            }
        }

        return false;
    }

    // The role of a spindle word: its address, else the role of the default spindle (virtual machine 3.8 rule 2).
    private static string? RoleOf(SiemensBlock write, string? address)
    {
        if (address is not null)
        {
            return address;
        }

        string? id = write.Machine.ResolveDefaultSpindle()?.Id;
        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == id)
            {
                return role.Key;
            }
        }

        return null;
    }

    private static SpindleSnapshot? StateOf(SiemensBlock write, string? role)
    {
        string? id = role is null ? write.Machine.ResolveDefaultSpindle()?.Id : write.Machine.ResolveRole(role)?.Id;
        return id is not null && write.After.Spindles.TryGetValue(id, out SpindleSnapshot? spindle) ? spindle : null;
    }

    // A number of a word for a placeholder, formatted for its address, or an expression as its SINUMERIK text, which
    // takes the equals sign after the plain S of S{rpm} (controllers siemens.md 1).
    private static void SetNumber(SiemensBlock write, TemplateValues values, string name, Value value, string address)
    {
        if (SiemensExpressions.ValueText(write, value, address) is string text)
        {
            values.Set(name, value is ExprValue ? SiemensBlock.ExpressionValue(text) : text);
        }
    }

    // S2=, M2=: the extension of a spindle address (controllers siemens.md 1).
    [GeneratedRegex(@"\b[SM]([0-9]+)=", RegexOptions.CultureInvariant)]
    private static partial Regex Extension();

    [GeneratedRegex("[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    // S1500, M3, SPOS=90, LIMS=3000, G96, G97: a word of the master spindle.
    [GeneratedRegex(@"^(S[-.0-9]+|S=.*|M[0-9]+|SPOSA?=.*|LIMS=.*|G9[67][0-9]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex MasterWord();

    // A rendered template and the master spindle it needs: its number, empty for the configured one, null for none.
    private sealed record SpindleText(string Text, string? Master);
}
