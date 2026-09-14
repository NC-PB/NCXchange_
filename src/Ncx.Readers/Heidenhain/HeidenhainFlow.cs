using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The labels, calls and jumps of a Klartext block (controllers heidenhain.md 1, 6, 7 rule 3; controller-mapping 6;
/// language 4.9): LBL n, whose SUB section or LABEL the structure pass writes; CALL LBL n as CALL=n; CALL LBL n REP k
/// as REPEAT=n TIMES=k; CALL PGM name as CALL="name"; FN 9 to FN 12 IF ... GOTO LBL n as JUMP=n with IF.
/// </summary>
internal static partial class HeidenhainFlow
{
    // The comparisons of FN 9 to FN 12: equal, unequal, greater, less (controllers heidenhain.md 6).
    private static readonly string[] s_comparisons = ["==", "!=", ">", "<"];

    /// <summary>
    /// The NCX value of a label or subprogram name: an integer, LBL 5, or an identifier, LBL "DRILL_ROW"; null for a
    /// name NCX cannot write as a label (language 4.9, LABEL; language 3, identifier).
    /// </summary>
    /// <param name="name">The label as the source names it.</param>
    public static Value? NameValue(string name)
    {
        if (long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
        {
            return new IntegerValue(number, name);
        }

        if (name.Length == 0 || !char.IsAsciiLetterUpper(name[0]))
        {
            return null;
        }

        foreach (char character in name)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return null;
            }
        }

        return new IdentValue(name);
    }

    /// <summary>
    /// Reads an LBL block: the structure pass writes the SUB=BEGIN of a subprogram, the SUB=END of its LBL 0 and the
    /// LABEL of a label that a REP or an FN jump uses (heidenhain 7 rule 3). A label that a jump or a repeat enters is
    /// reached with a state the blocks before it do not tell (virtual machine 3.6). Any other LBL stays RAW, so that a
    /// RAW block that names it, the contour of CYCL DEF 14, still finds it (D5).
    /// </summary>
    /// <param name="block">The block being read, whose first word is LBL.</param>
    public static void ReadLabel(HeidenhainBlock block)
    {
        HeidenhainLabels labels = block.Heidenhain.Labels;
        string? label = HeidenhainLabels.LabelOf(block.Source);
        block.MarkAllRead();
        if (labels.SubBegins.ContainsKey(block.Line) || labels.SubEnds.Contains(block.Line)
            || (label is not null && labels.EndTargets.Contains(label) && !labels.Targets.Contains(label)))
        {
            return;
        }

        if (label is not null && label != "0" && labels.Targets.Contains(label) && NameValue(label) is not null)
        {
            HeidenhainState state = block.Heidenhain;
            state.ForgetFrame();
            state.CycleOn = state.CycleOn == false ? false : null;
            return;
        }

        block.Draft.KeepAsRaw(label == "0"
            ? "LBL 0 closes no subprogram of the file (controllers heidenhain.md 7 rule 3)"
            : $"LBL {label} is neither a subprogram nor the target of a REP or an FN jump (controllers heidenhain.md 7 "
                + "rule 3)");
    }

    /// <summary>
    /// Reads CALL LBL n, CALL LBL n REP k and CALL PGM name.
    /// </summary>
    /// <param name="block">The block being read, whose first word is CALL.</param>
    public static void ReadCall(HeidenhainBlock block)
    {
        if (block.Keyword(1) == "PGM")
        {
            ReadProgramCall(block);
        }
        else if (HeidenhainLabels.IsRepeat(block.Source))
        {
            ReadRepeat(block);
        }
        else if (HeidenhainLabels.IsLabelCall(block.Source))
        {
            ReadLabelCall(block);
        }
        else
        {
            block.Draft.KeepAsRaw("the CALL names neither LBL nor PGM");
        }
    }

    /// <summary>
    /// Reads FN 9 to FN 12: IF a EQU b GOTO LBL n jumps to the label n when a equals b, FN 10 when they differ, FN 11
    /// when a is greater, FN 12 when a is less (controllers heidenhain.md 6); JUMP=END where the end of the program
    /// follows the label (controller-mapping 1).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="function">The number of the FN function, 9 to 12.</param>
    public static void ReadJump(HeidenhainBlock block, int function)
    {
        block.MarkAllRead();
        Match jump = Jump().Match(block.Content);
        string? first = jump.Success ? HeidenhainExpression.ToText(jump.Groups[1].Value, out string? problem) : null;
        problem = jump.Success ? null : $"FN {function} is not written IF a <comparison> b GOTO LBL n";
        string? second = first is null ? null : HeidenhainExpression.ToText(jump.Groups[3].Value, out problem);
        string label = HeidenhainLabels.LabelOf(block.Source) ?? "";
        Value? target = block.Heidenhain.Labels.JumpsToTheEnd.Contains(block.Line)
            ? new IdentValue("END")
            : NameValue(label);
        Value? condition = second is null ? null : HeidenhainExpression.Parse(
            first + " " + s_comparisons[function - 9] + " " + second, block.Line, block.Diagnostics.File, out problem);
        if (target is null || condition is null)
        {
            block.Draft.KeepAsRaw(problem ?? $"LBL {label} is no label NCX can write (language 4.9)");
            return;
        }

        block.Draft.Main.Add("JUMP", target).Add("IF", condition);
    }

    // CALL LBL n calls the subprogram LBL n ... LBL 0 once (controllers heidenhain.md 1; controller-mapping 6, CALL).
    // The subprogram runs with the state of its caller, which the reader records, and the caller continues with what
    // the subprogram may change unknown (virtual machine 3.9).
    private static void ReadLabelCall(HeidenhainBlock block)
    {
        block.MarkAllRead();
        HeidenhainState state = block.Heidenhain;
        string? label = HeidenhainLabels.LabelOf(block.Source);
        Value? name = label is null ? null : NameValue(label);
        if (label is null || name is null || !state.Labels.Subs.Contains(label))
        {
            block.Draft.KeepAsRaw($"LBL {label} is no subprogram of the file: none is called without REP and closed by "
                + "LBL 0 after the M30 of the program (controllers heidenhain.md 1, 7 rule 3)");
            return;
        }

        block.Draft.Main.Add("CALL", name);
        state.Calls.Record(label, HeidenhainCallerState.Of(state));
        state.Calls.ChangesOf(label, state).ApplyTo(state);
    }

    // CALL LBL n REP k repeats the part between LBL n and the call k more times (controllers heidenhain.md 1;
    // controller-mapping 6, REPEAT): REPEAT=n TIMES=k. The editor writes the count as k or k/k.
    private static void ReadRepeat(HeidenhainBlock block)
    {
        block.MarkAllRead();
        string? label = HeidenhainLabels.LabelOf(block.Source);
        Value? name = label is null ? null : NameValue(label);
        SourceWord? count = null;
        for (int index = 0; index < block.Source.Words.Count; index++)
        {
            SourceWord word = block.Source.Words[index];
            if (word.Address == "REP")
            {
                count = word.Text.Length > 0 || index + 1 >= block.Source.Words.Count
                    ? word
                    : block.Source.Words[index + 1];
            }
        }

        string times = count?.Text.Split('/')[0] ?? "";
        if (name is null || !long.TryParse(times, NumberStyles.None, CultureInfo.InvariantCulture, out long passes))
        {
            block.Draft.KeepAsRaw("CALL LBL REP names no label NCX can write, or no count (language 4.9)");
            return;
        }

        block.Draft.Main.Add("REPEAT", name).Add("TIMES", new IntegerValue(passes, times));
        block.Heidenhain.ForgetFrame();
        block.Heidenhain.CycleOn = block.Heidenhain.CycleOn == false ? false : null;
    }

    // CALL PGM name calls another file, with the Q parameters set before the call (controllers heidenhain.md 1;
    // controller-mapping 6, CALL and ARG): CALL="name", an external program (language 4.9). Where the tool stands
    // afterwards is unknown (virtual machine 1), and so is the pole, which the called file may set.
    private static void ReadProgramCall(HeidenhainBlock block)
    {
        block.MarkAllRead();
        string name = Regex.Replace(block.Content, @"^CALL\s+PGM\s*", "", RegexOptions.IgnoreCase).Trim().Trim('"');
        if (name.Length == 0)
        {
            block.Draft.KeepAsRaw("CALL PGM names no program");
            return;
        }

        block.Draft.Main.Add("CALL", new StringValue(name));
        block.Heidenhain.ForgetFrame();
    }

    // FN 9: IF +Q1 EQU +Q3 GOTO LBL 5 (controllers heidenhain.md 6).
    [GeneratedRegex(@"^FN\s*[0-9]+\s*:\s*IF\s+(\S+)\s+(\S+)\s+(\S+)\s+GOTO\s+LBL\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Jump();
}
