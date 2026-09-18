using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// One NCX block as the Fanuc compiler writes it: the block with its state before and after (architecture 8), the
/// machine, what the target control has active, the main line being composed, and the words written so far, so that a
/// word no concern writes is reported and never dropped silently (language 2 rule 8).
/// </summary>
internal sealed class FanucBlock
{
    // The addresses of Fanuc that take whole numbers: P (the dwell in milliseconds, program and point numbers), L and
    // K (repeat counts), S (the speed), and the registers, tools and codes H, D, T, M, G, N, O (controllers fanuc.md 1,
    // 4, 5, 6); every other address takes a real number (fanuc 2).
    private static readonly string[] s_wholeNumberAddresses = ["P", "L", "K", "S", "H", "D", "T", "M", "G", "N", "O"];

    // What the target state holds under a key whose value on the control is not known: no code or number is written
    // as "?", so the next value is written whatever it is. A key that is removed instead would be taken over after the
    // return of a subprogram as the caller left it (TargetState.TakeOver; virtual machine 3.9, D99), so an unknown that
    // the walk of a subprogram leaves stands as a value.
    private const string UnknownValue = "?";

    // What the target state holds under a modal key whose value on the control is the value of NCX on the path the
    // control came along, which differs by path (a LABEL that a jump reaches, the block after a skipped one; virtual
    // machine 1, D53), or which an expression set that the STATIC walk does not evaluate: F, the modal words of
    // language 4.3, keeps its value on the control as in NCX, so nothing stands for it again until a block states it.
    private const string HeldValue = "=";

    // What the target state holds under a modal key whose value of NCX differs by path or comes from an expression,
    // where the control does not hold it: a block that takes the value from the state before it cannot be written right
    // on every path.
    private const string LostValue = "!";

    // The words the concerns have written, by key and address.
    private readonly HashSet<string> _written = new(StringComparer.Ordinal);

    /// <summary>
    /// The block with its state and events, as the STATIC walk executed it.
    /// </summary>
    public required BlockStep Step { get; init; }

    /// <summary>
    /// Every step of the run in walk order, the look-ahead of the STATIC pass (architecture 8).
    /// </summary>
    public required IReadOnlyList<BlockStep> Steps { get; init; }

    /// <summary>
    /// The machine the program is compiled for.
    /// </summary>
    public required MachineConfig Machine { get; init; }

    /// <summary>
    /// What the target control has active in the program or walk being written (phase 3, P3-03).
    /// </summary>
    public required TargetState Target { get; init; }

    /// <summary>
    /// The numbers as [format] writes them (machine-config 2).
    /// </summary>
    public required NumberFormatter Numbers { get; init; }

    /// <summary>
    /// The templates of the machine (architecture 6).
    /// </summary>
    public required TemplateSet Templates { get; init; }

    /// <summary>
    /// Where the diagnostics of the compile go (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// The labels of the program or subprogram of the block with their block numbers.
    /// </summary>
    public required FanucLabels Labels { get; init; }

    /// <summary>
    /// Writes one line of the block, CompilerBase.Line.
    /// </summary>
    public required Action<string> Writer { get; init; }

    /// <summary>
    /// Writes a comment text in the charset of the machine, the transliteration of CompilerBase.CommentText.
    /// </summary>
    public required Func<string, string> CommentText { get; init; }

    /// <summary>
    /// The line of the block being composed: the modal codes, the motion, the functions (controllers fanuc.md 1).
    /// </summary>
    public FanucLine Main { get; } = new();

    /// <summary>
    /// The block.
    /// </summary>
    public Block Block => Step.Block;

    /// <summary>
    /// The state of the channel before the block.
    /// </summary>
    public ChannelSnapshot Before => Step.Before;

    /// <summary>
    /// The state of the channel after the block.
    /// </summary>
    public ChannelSnapshot After => Step.After;

    /// <summary>
    /// The G-code system of a lathe; null for a mill (machine-config 1, gcode_system).
    /// </summary>
    public GcodeSystem? System => Machine.Machine.GcodeSystem;

    /// <summary>
    /// True on a Fanuc lathe, whose G-code system the machine file names (machine-config 1).
    /// </summary>
    public bool IsLathe => System is not null;

    /// <summary>
    /// The optional block skip of the block, / or /n in front of each of its lines (controller-mapping 1, SKIP).
    /// </summary>
    public string SkipMark
    {
        get
        {
            if (!Block.Skip)
            {
                return "";
            }

            return Block.SkipNumber is int number ? "/" + number.ToString(CultureInfo.InvariantCulture) : "/";
        }
    }

    /// <summary>
    /// Marks the words of a key, whatever their address, as written.
    /// </summary>
    /// <param name="key">The key, "RPM".</param>
    public void Written(string key)
    {
        foreach (Word word in Block.Words)
        {
            if (word.Key == key)
            {
                _written.Add(NameOf(word));
            }
        }
    }

    /// <summary>
    /// Marks one word as written.
    /// </summary>
    public void Written(Word word)
    {
        _written.Add(NameOf(word));
    }

    /// <summary>
    /// Marks every word of the block as written, after an ERROR that stops the block, so that the ERROR stands alone.
    /// </summary>
    public void WrittenAll()
    {
        foreach (Word word in Block.Words)
        {
            _written.Add(NameOf(word));
        }
    }

    /// <summary>
    /// Writes a line of the block with its skip mark (controller-mapping 1, SKIP).
    /// </summary>
    /// <param name="text">The line without block number.</param>
    public void Write(string text)
    {
        if (text.Length > 0)
        {
            Writer(SkipMark + text);
        }
    }

    /// <summary>
    /// The lines that follow the main line: the further turns of an arc beyond 360 degrees (language 4.3, ANGLE).
    /// </summary>
    public List<string> Following { get; } = [];

    /// <summary>
    /// True once the offsets of the block are written, so that they stand once.
    /// </summary>
    public bool OffsetsAdded { get; set; }

    /// <summary>
    /// The spindles, by resource id, whose M code stands already in a spindle line of the block, G96 S140 M3, so that
    /// it stands once.
    /// </summary>
    public HashSet<string> SpindleCodesWritten { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes the main line when anything stands in it, then the lines that follow it.
    /// </summary>
    public void WriteMain()
    {
        if (!Main.IsEmpty)
        {
            Write(Main.Text());
        }

        foreach (string line in Following)
        {
            Write(line);
        }
    }

    /// <summary>
    /// Renders a template of the machine for the block (machine-config introduction): null, with the ERROR of the
    /// framework, when the machine file does not give it, or with the ERROR of the template when a placeholder has no
    /// value.
    /// </summary>
    /// <param name="text">The template as the record keeps it; null when the file has none.</param>
    /// <param name="what">The template as a message names it: "[coolant] STANDARD ON (machine-config 5)".</param>
    /// <param name="values">The values of its placeholders.</param>
    public string? Render(string? text, string what, TemplateValues values)
    {
        if (text is null)
        {
            Diagnostics.Error(Block, DiagnosticCodes.TemplateMissing,
                $"The machine \"{Machine.Machine.Name}\" has no {what}, which {WordsText()} needs, so nothing is "
                + "written for it.");
            return null;
        }

        return TemplateOf(text).Render(values, Block, Diagnostics);
    }

    /// <summary>
    /// A template of the machine as the set holds it, with its placeholders (architecture 6).
    /// </summary>
    /// <param name="text">The template as the machine file writes it.</param>
    public Template TemplateOf(string text)
    {
        return Templates.For(text) ?? new Template(text, 1, new Diagnostics(Machine.Machine.Name));
    }

    /// <summary>
    /// Makes the value of a key unknown to the target state, also for the caller of the subprogram being written, so
    /// that the next value under it is written whatever it is.
    /// </summary>
    /// <param name="key">The key: "G01".</param>
    public void MakeUnknown(string key)
    {
        Target.Set(key, UnknownValue);
    }

    /// <summary>
    /// Makes every value the target state knows unknown, where the control may arrive from another block than the one
    /// before (virtual machine 1: the STATIC walk does not follow JUMP). The length offset that waits for the tool axis
    /// is no value of the control, and it stands before such a block already.
    /// </summary>
    public void MakeTargetUnknown()
    {
        foreach (string key in Target.Active.Keys.ToList())
        {
            if (key != FanucToolWords.PendingLength)
            {
                MakeUnknown(key);
            }
        }
    }

    /// <summary>
    /// Records that the control holds the value of NCX under a modal key on every path it may come along, a value the
    /// compiler cannot name as one: it differs by path, or an expression set it (language 4.3, F is modal; virtual
    /// machine 1).
    /// </summary>
    /// <param name="key">The key: "F".</param>
    public void Hold(string key)
    {
        Target.Set(key, HeldValue);
    }

    /// <summary>
    /// Records that the control does not hold the value of NCX under a modal key on every path, and that the compiler
    /// cannot name it: a block that takes it from the state before it is CMP309.
    /// </summary>
    /// <param name="key">The key: "F".</param>
    public void Lose(string key)
    {
        Target.Set(key, LostValue);
    }

    /// <summary>
    /// True when the control holds, under a modal key, the value of NCX of the path it came along (Hold).
    /// </summary>
    public bool Holds(string key)
    {
        return IsHeld(Target.ActiveOf(key));
    }

    /// <summary>
    /// True for the value of a target state that says the control holds the value of NCX of its path (Hold).
    /// </summary>
    /// <param name="active">What a target state holds under a key; null for nothing.</param>
    public static bool IsHeld(string? active)
    {
        return active == HeldValue;
    }

    /// <summary>
    /// True when the value of NCX under a modal key differs by path or comes from an expression (Hold, Lose).
    /// </summary>
    public bool DependsOnThePath(string key)
    {
        return IsPathValue(Target.ActiveOf(key));
    }

    /// <summary>
    /// True for the value of a target state that says the value of NCX under its key differs by path or comes from an
    /// expression (Hold, Lose).
    /// </summary>
    /// <param name="active">What a target state holds under a key; null for nothing.</param>
    public static bool IsPathValue(string? active)
    {
        return active is HeldValue or LostValue;
    }

    /// <summary>
    /// Tells whether a modal value that the block takes from the state before it, without stating it, must be written,
    /// and records it: a code or value is written only on change (controllers fanuc.md 10 rule 1), nothing where the
    /// control holds the value of NCX of each path (Hold), and CMP309 where it holds none of them (Lose), since the
    /// value of the STATIC walk is the value of one path only (virtual machine 1; D53).
    /// </summary>
    /// <param name="key">The key: "F".</param>
    /// <param name="value">The value of the STATIC walk as written: "100.".</param>
    /// <param name="word">The NCX word the value belongs to, for the message: "F".</param>
    public bool NeedsValueOfTheWalk(string key, string value, string word)
    {
        string? active = Target.ActiveOf(key);
        if (active == HeldValue)
        {
            return false;
        }

        if (active == LostValue)
        {
            ReportNotHeld(word);
            return false;
        }

        return Target.Changes(key, value);
    }

    /// <summary>
    /// A modal word the block states and the compiler writes in a later block (F of a RAPID, WORKPLANE under
    /// plane_with_first_motion, the RPM of a standing spindle): the value of NCX is the same on every path from here
    /// on, so the next use writes it; one from an expression is not written anywhere and cannot be named.
    /// </summary>
    /// <param name="key">The key: "F".</param>
    /// <param name="known">False for a value from an expression (virtual machine 1).</param>
    public void StatesForLater(string key, bool known)
    {
        if (!known)
        {
            Lose(key);
        }
        else if (DependsOnThePath(key))
        {
            MakeUnknown(key);
        }
    }

    /// <summary>
    /// The compiler wrote under a modal key a value that is not the value of NCX, the F of a canned cycle (D218) or
    /// the cutting speed of G96 S (controllers fanuc.md 4): where the value of NCX differs by path the control holds
    /// none of them any more.
    /// </summary>
    /// <param name="key">The key: "F".</param>
    /// <param name="written">The value written, a number; null for an expression, which the compiler cannot
    /// name.</param>
    public void WroteAnotherValue(string key, string? written)
    {
        if (DependsOnThePath(key))
        {
            Lose(key);
        }
        else if (written is null)
        {
            MakeUnknown(key);
        }
        else
        {
            Target.Set(key, written);
        }
    }

    /// <summary>
    /// CMP309 for a modal value that the block takes from the state before it and that the control does not hold on
    /// every path.
    /// </summary>
    /// <param name="word">The NCX word, "F".</param>
    public void ReportNotHeld(string word)
    {
        Error(DiagnosticCodes.FanucModalValueNotHeld,
            $"The block takes {word} from the state before it, and that value differs by the path the control arrives "
            + "on (a LABEL that a jump reaches, the block after a skipped one) or comes from an expression, while the "
            + "control does not hold it on every path; state it in the block (virtual machine 1; D53; language 4.3).");
    }

    /// <summary>
    /// Reports an ERROR on the block (D98).
    /// </summary>
    public void Error(string code, string message)
    {
        Diagnostics.Error(Block, code, message);
    }

    /// <summary>
    /// Reports a WARNING on the block (D98).
    /// </summary>
    public void Warning(string code, string message)
    {
        Diagnostics.Warning(Block, code, message);
    }

    /// <summary>
    /// Every word of the block that no concern wrote is an ERROR: the compiler never drops a word silently (language 2
    /// rule 8, code-guidelines 6).
    /// </summary>
    public void ReportUnwritten()
    {
        foreach (Word word in Block.Words)
        {
            if (!_written.Contains(NameOf(word)))
            {
                Error(DiagnosticCodes.FanucWordNotWritten,
                    $"{word.ToCanonical()} has no Fanuc form the compiler writes, so the block cannot be compiled "
                    + "without losing it (language 2 rule 8; controllers fanuc.md 10).");
            }
        }
    }

    /// <summary>
    /// A number of an address as [format] writes it (machine-config 2), a real number with its decimal point whether
    /// [format] decimals lists the address or not, R30. for an angle: without calculator-type input R30 is 0.03
    /// (controllers fanuc.md 2, 10 rule 5).
    /// </summary>
    /// <param name="address">The address whose decimals apply: X, F, S.</param>
    /// <param name="value">The value.</param>
    public string Format(string address, decimal value)
    {
        string text = Numbers.Format(address, value, Block);
        bool whole = s_wholeNumberAddresses.Contains(address) || Numbers.DecimalsOf(address) == 0;
        return whole || text.Contains(Numbers.DecimalSeparator, StringComparison.Ordinal)
            ? text
            : text + Numbers.DecimalSeparator;
    }

    /// <summary>
    /// Sets a real number the compiler gives a template, 30 as 30. with the decimal point of controllers fanuc.md 10
    /// rule 5, as written otherwise (language 2 rule 5); a template that writes the point after the placeholder
    /// itself, {tool:02}. (machine-config 3), takes the number, so that the point stands once.
    /// </summary>
    /// <param name="values">The values of the template.</param>
    /// <param name="name">The placeholder name, "r" for {r}.</param>
    /// <param name="value">The number.</param>
    /// <param name="template">The template as the machine file writes it; null when it has none.</param>
    public void SetReal(TemplateValues values, string name, decimal value, string? template)
    {
        if (template is not null && WritesPointAfter(template, name))
        {
            values.Set(name, value);
            return;
        }

        string text = value.ToString(CultureInfo.InvariantCulture);
        if (!text.Contains('.', StringComparison.Ordinal))
        {
            text += ".";
        }

        values.Set(name, text.Replace(".", Numbers.DecimalSeparator, StringComparison.Ordinal));
    }

    /// <summary>
    /// A number the compiler computed, rounded half away from zero to the decimals of its address before it is
    /// written: a computed coordinate is no number of the program (language 2 rule 5, 4.12 ROUND).
    /// </summary>
    public string FormatComputed(string address, decimal value)
    {
        if (Numbers.DecimalsOf(address) is int decimals)
        {
            value = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }

        return Format(address, value);
    }

    // The words of the block as canonical NCX writes them, for a message.
    private string WordsText()
    {
        var words = new List<string>();
        foreach (Word word in Block.Words)
        {
            words.Add(word.ToCanonical());
        }

        return string.Join(" ", words);
    }

    private static string NameOf(Word word)
    {
        return word.Addr is null ? word.Key : word.Key + ":" + word.Addr;
    }

    // True when the template writes a point right after the placeholder, {b}. or {tool:02}. (machine-config 3).
    private bool WritesPointAfter(string template, string name)
    {
        foreach (Placeholder placeholder in TemplateOf(template).Placeholders)
        {
            string braces = placeholder.ToString();
            if (placeholder.Name == name && template.Contains(braces + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
