using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// One NCX block as the Siemens compiler writes it: the block with its state before and after (architecture 8), the
/// machine, what the target control has active, the main line being composed, the file being written, and the words
/// written so far, so that a word no concern writes is reported and never dropped silently (language 2 rule 8).
/// </summary>
internal sealed partial class SiemensBlock
{
    // The mark in front of the value of a placeholder that is an expression, which Render replaces (controllers
    // siemens.md 1).
    private const string ExpressionMark = "\u0001";

    // What the target state holds under a key whose value on the control is not known: no code or value is written as
    // "?", so the next value under the key is written whatever it is. A key removed instead would be taken over after
    // the return of a subprogram as the caller left it (TargetState.TakeOver; virtual machine 3.9, D99), so an unknown
    // that the walk of a subprogram leaves stands as a value.
    private const string UnknownValue = "?";

    // The words the concerns have written, by key and address.
    private readonly HashSet<string> _written = new(StringComparer.Ordinal);

    /// <summary>
    /// The block with its state and events, as the STATIC walk executed it.
    /// </summary>
    public required BlockStep Step { get; init; }

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
    /// What the file being written knows across its blocks: the units, the labels, the parameters of the subprograms.
    /// </summary>
    public required SiemensFile File { get; init; }

    /// <summary>
    /// Writes one line of the block, CompilerBase.Line.
    /// </summary>
    public required Action<string> Writer { get; init; }

    /// <summary>
    /// A comment text in the charset of the machine, the transliteration of CompilerBase.CommentText.
    /// </summary>
    public required Func<string, string> CommentText { get; init; }

    /// <summary>
    /// The line of the block being composed: the modal codes, the motion, the functions (controllers siemens.md 1).
    /// </summary>
    public SiemensLine Main { get; } = new();

    /// <summary>
    /// The lines that follow the main line: the further turns of an arc on an older 840D (controllers siemens.md 12
    /// rule 2).
    /// </summary>
    public List<string> Following { get; } = [];

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
    /// True for a block of a subprogram, written once for every caller from an unknown target state (D99).
    /// </summary>
    public bool InSubprogram => Step.Section?.Kind == SectionKind.Sub;

    /// <summary>
    /// The optional block skip of the block, / or /n in front of each of its lines: / or /0 skips on the first skip
    /// level, /1 to /9 on the others (controllers siemens.md 1; controller-mapping 1, SKIP).
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
    /// The value the control has active under a key of the target state; null while it is unknown: never written in
    /// the program or walk, or made unknown.
    /// </summary>
    /// <param name="key">The key: "G94", "MCALL".</param>
    public string? ActiveOf(string key)
    {
        string? active = Target.ActiveOf(key);
        return active == UnknownValue ? null : active;
    }

    /// <summary>
    /// Makes the value under a key of the target state unknown, so that the next value under it is written whatever it
    /// is.
    /// </summary>
    /// <param name="key">The key: "G94".</param>
    public void MakeUnknown(string key)
    {
        Target.Set(key, UnknownValue);
    }

    /// <summary>
    /// True where the value under a key was made unknown (MakeUnknown), by a RAW line or a label, and not only never
    /// written in the program or walk.
    /// </summary>
    /// <param name="key">The key: "TRANS".</param>
    public bool IsMadeUnknown(string key)
    {
        return Target.ActiveOf(key) == UnknownValue;
    }

    /// <summary>
    /// Makes every value the target state knows unknown.
    /// </summary>
    public void MakeTargetUnknown()
    {
        foreach (string key in Target.Active.Keys.ToList())
        {
            MakeUnknown(key);
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
    /// Writes a line of the block with its skip mark, a line of its own for each line of a template (controller-mapping
    /// 1, SKIP; machine-config 3).
    /// </summary>
    /// <param name="text">The line without block number.</param>
    public void Write(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            if (line.Length > 0)
            {
                Writer(SkipMark + line);
            }
        }
    }

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

        string? rendered = TemplateOf(text).Render(values, Block, Diagnostics);
        if (rendered is null || !rendered.Contains(ExpressionMark, StringComparison.Ordinal))
        {
            return rendered;
        }

        // X=10 when the value is an expression or a variable, X=R1*2 (controllers siemens.md 1): an expression after a
        // single-letter address of the template, S{rpm}, takes the equals sign; after LIMS=, a comma or a parenthesis
        // it stands as it is.
        return ExpressionAddress().Replace(rendered, "$1=").Replace(ExpressionMark, "", StringComparison.Ordinal);
    }

    /// <summary>
    /// The text of an expression as the value of a placeholder, marked for Render, which writes it after an equals sign
    /// where a single-letter address stands directly in front of it: S=R1*2 for S{rpm} and G96 S=R1/10 for G96 S{value}
    /// (controllers siemens.md 1).
    /// </summary>
    /// <param name="text">The SINUMERIK text of the expression.</param>
    public static string ExpressionValue(string text)
    {
        return ExpressionMark + text;
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
    /// True when a template has a placeholder of this name.
    /// </summary>
    public bool HasPlaceholder(string? text, string name)
    {
        if (text is null)
        {
            return false;
        }

        foreach (Placeholder placeholder in TemplateOf(text).Placeholders)
        {
            if (placeholder.Name == name)
            {
                return true;
            }
        }

        return false;
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
                Error(DiagnosticCodes.SiemensWordNotWritten,
                    $"{word.ToCanonical()} has no SINUMERIK form the compiler writes, so the block cannot be compiled "
                    + "without losing it (language 2 rule 8; controllers siemens.md 12).");
            }
        }
    }

    /// <summary>
    /// A number of an address as [format] writes it (machine-config 2): X42, F0.2.
    /// </summary>
    /// <param name="address">The address whose decimals apply: X, F, S.</param>
    /// <param name="value">The value.</param>
    public string Format(string address, decimal value)
    {
        return Numbers.Format(address, value, Block);
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

    /// <summary>
    /// The words of the block as canonical NCX writes them, for a message.
    /// </summary>
    public string WordsText()
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

    // A single-letter address, no part of a longer name or of an index, in front of a marked expression.
    [GeneratedRegex("(?<![A-Za-z0-9_$\\]])([A-Z])\u0001", RegexOptions.CultureInvariant)]
    private static partial Regex ExpressionAddress();
}
