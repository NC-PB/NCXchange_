using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Machine;

namespace Ncx.Acceptance;

/// <summary>
/// The comparison rules of phase 3 (implementation 13, P3-07, "Comparison rules"; code-guidelines 8), the one
/// comparison of NC programs of the acceptance: two NC programs are equivalent when, after removing block numbers,
/// comments and blank lines, trimming whitespace, and formatting every number of both sides with the target machine's
/// [format] decimals (so 70. and 70.0 and 70 are equal, and -.534 equals -0.534), the remaining lines are equal in
/// order; the header block of the source may carry modal words in another order than the compiled one, so a header line
/// is compared as a set of words; a TOOL CALL or T M6 line is compared as a set of words; the program_end line is not a
/// difference where the source ends in another form or in none (wave-1 question #12, answered by D49). Everything else,
/// including the order of words inside a motion block, must match. A difference the documents cannot settle is an
/// <see cref="NcException"/> grounded in its question and used exactly once; any other difference is shown as a unified
/// diff.
/// </summary>
internal sealed partial class NcComparer
{
    // Lines of context around a difference, as a unified diff shows them.
    private const int Context = 3;

    // How many blocks of each program the diff looks ahead for the two programs to agree again after a difference, and
    // how many of each it shows when they do not agree within that reach.
    private const int LookAhead = 50;
    private const int Unmatched = 10;

    // The codes that end a program in the sources: M30 and M2, and M17 on Siemens (controller-mapping 1, PROGRAM=END).
    private static readonly string[] s_programEnds = ["M30", "M2", "M17"];

    private readonly MachineConfig _machine;
    private readonly bool _parenthesesAreComments;
    private readonly Regex _blockNumber;
    private readonly string _separator;
    private readonly string? _programEnd;

    /// <summary>
    /// The comparison with the [format] of the target machine and the block syntax of its controller: its comments,
    /// its block numbers and its skip mark.
    /// </summary>
    public NcComparer(MachineConfig machine)
    {
        _machine = machine;
        _parenthesesAreComments = machine.Machine.Controller == Controller.Fanuc;
        _blockNumber = BlockNumberOf(machine.Machine.Controller);
        _separator = machine.Format?.DecimalSeparator ?? ".";
        _programEnd = machine.Format?.ProgramEnd is string programEnd ? Normalized(programEnd) : null;
    }

    /// <summary>
    /// The blocks of an NC program as the rules compare them: block numbers (N10, or the 12 of Klartext) removed with
    /// the / or /n of an optional skip kept in front of the words, comments removed (( ) on Fanuc and nothing else, ;
    /// on the other controllers, and the * - structuring blocks of Klartext), the lines a Klartext ~ continues joined,
    /// blank lines left out, runs of blanks made one, and every number formatted with the decimals of its address and
    /// the decimal separator of the machine, without a plus sign and without trailing zeros.
    /// </summary>
    public List<NcBlock> Blocks(string text)
    {
        var blocks = new List<NcBlock>();
        string continued = "";
        int firstLine = 0;
        bool inHeader = true;
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string content = lines[index].Trim();
            bool continues = content.EndsWith('~');
            content = WithoutComment(continues ? content.Substring(0, content.Length - 1) : content);
            if (continued.Length == 0)
            {
                firstLine = index + 1;
                content = WithoutBlockNumber(content);
            }

            string joined = (continued + " " + content).Trim();
            if (continues)
            {
                continued = joined;
                continue;
            }

            continued = "";
            if (joined.Length == 0 || joined.StartsWith('*'))
            {
                continue;
            }

            // The header block is the program start and the lines of modal G codes alone that follow it (comparison
            // rules: the header line is compared as a set of words).
            string normalized = Normalized(joined);
            inHeader = inHeader && (IsProgramStart(normalized) || IsModalCodes(normalized));
            blocks.Add(new NcBlock(firstLine, normalized, inHeader));
        }

        if (continued.Length > 0)
        {
            string normalized = Normalized(continued);
            blocks.Add(new NcBlock(firstLine, normalized,
                inHeader && (IsProgramStart(normalized) || IsModalCodes(normalized))));
        }

        return blocks;
    }

    /// <summary>
    /// Compares a source program with a compiled one under the rules and the exceptions, in their order.
    /// </summary>
    /// <param name="source">The source program, the expected text.</param>
    /// <param name="compiled">The compiled program.</param>
    /// <param name="exceptions">The differences grounded in their questions, in the order they stand in the
    /// programs.</param>
    /// <returns>Null when the programs are equivalent; otherwise the first difference as a unified diff of the blocks
    /// as they are compared, or the exception that was never used.</returns>
    public string? Compare(string source, string compiled, IReadOnlyList<NcException> exceptions)
    {
        List<NcBlock> expected = Blocks(source);
        List<NcBlock> actual = Blocks(compiled);
        int sourceIndex = 0;
        int compiledIndex = 0;
        int next = 0;

        // The blocks that agreed right before the current one, the context a difference is shown with.
        int agreed = 0;
        while (sourceIndex < expected.Count || compiledIndex < actual.Count)
        {
            if (sourceIndex < expected.Count && compiledIndex < actual.Count
                && Same(expected[sourceIndex], actual[compiledIndex]))
            {
                sourceIndex++;
                compiledIndex++;
                agreed++;
                continue;
            }

            // Wave-1 question #12, answered by D49: the compiler writes program_end, and the source's end form is not
            // kept, so the program_end line is not a difference where the source ends in another form or in none.
            if (compiledIndex < actual.Count && actual[compiledIndex].Text == _programEnd)
            {
                bool otherForm = sourceIndex < expected.Count && IsProgramEnd(expected[sourceIndex].Text);
                if (otherForm || EndsWithoutEnd(expected, sourceIndex))
                {
                    sourceIndex += otherForm ? 1 : 0;
                    compiledIndex++;
                    agreed = 0;
                    continue;
                }
            }

            // Phase 3: a difference the documents cannot settle is an exception grounded in its question, taken in
            // the order the exceptions stand, each once.
            if (next < exceptions.Count && Matches(exceptions[next].Source, expected, sourceIndex)
                && Matches(exceptions[next].Compiled, actual, compiledIndex))
            {
                sourceIndex += exceptions[next].Source.Count;
                compiledIndex += exceptions[next].Compiled.Count;
                next++;
                agreed = 0;
                continue;
            }

            return Difference(expected, actual, sourceIndex, compiledIndex, Math.Min(agreed, Context));
        }

        return next < exceptions.Count
            ? $"The exception grounded in {exceptions[next].Grounds} matches no difference of the two programs."
            : null;
    }

    // A block that ends a program in one of the forms of controller-mapping 1, PROGRAM=END.
    private static bool IsProgramEnd(string text)
    {
        return s_programEnds.Contains(text);
    }

    // The source has no end of its program here: the block is the close of the program, END PGM of Klartext or the %
    // of a Fanuc file, or the source has no block left.
    private static bool EndsWithoutEnd(List<NcBlock> expected, int sourceIndex)
    {
        return sourceIndex == expected.Count || expected[sourceIndex].Text == "%"
            || expected[sourceIndex].Text.StartsWith("END PGM", StringComparison.Ordinal);
    }

    // Two blocks are equal as text, or, for two header lines and for two tool changes, as a set of words (comparison
    // rules).
    private static bool Same(NcBlock expected, NcBlock actual)
    {
        if (expected.Text == actual.Text)
        {
            return true;
        }

        bool asSet = (expected.IsHeader && actual.IsHeader)
            || (IsToolChange(expected.Text) && IsToolChange(actual.Text));
        return asSet && SameWords(expected.Text, actual.Text);
    }

    // The program start: the % of a Fanuc file, the O line with the program number, BEGIN PGM of Klartext.
    private static bool IsProgramStart(string text)
    {
        return text.StartsWith('%') || text.StartsWith("BEGIN PGM", StringComparison.Ordinal) || ProgramNumber()
            .IsMatch(text);
    }

    // A line of modal G codes alone, G0 G40 or G80 G90 G94 G98.
    private static bool IsModalCodes(string text)
    {
        foreach (string word in text.Split(' '))
        {
            if (!GCode().IsMatch(word))
            {
                return false;
            }
        }

        return true;
    }

    // TOOL CALL of Klartext, or a line with a T word and M6 (T1 M6, T="DRILL" M6).
    private static bool IsToolChange(string text)
    {
        if (text.StartsWith("TOOL CALL", StringComparison.Ordinal))
        {
            return true;
        }

        List<string> words = [.. text.Split(' ')];
        bool toolWord = words.Exists(word => word.Length > 1 && word[0] == 'T' && (char.IsAsciiDigit(word[1])
            || word[1] == '='));
        return toolWord && words.Contains("M6");
    }

    private static bool SameWords(string expected, string actual)
    {
        var expectedWords = new List<string>(expected.Split(' '));
        var actualWords = new List<string>(actual.Split(' '));
        expectedWords.Sort(StringComparer.Ordinal);
        actualWords.Sort(StringComparer.Ordinal);
        return expectedWords.SequenceEqual(actualWords);
    }

    // The lines of an exception stand at this place of the blocks, each normalized as a block is.
    private bool Matches(IReadOnlyList<string> lines, List<NcBlock> blocks, int start)
    {
        if (start + lines.Count > blocks.Count)
        {
            return false;
        }

        for (int index = 0; index < lines.Count; index++)
        {
            if (Normalized(lines[index]) != blocks[start + index].Text)
            {
                return false;
            }
        }

        return true;
    }

    // The first difference as one hunk of a unified diff: the blocks that agreed before it as context, the blocks of
    // the source that differ as -, those of the compiled program as +, up to where the two agree again for the next
    // lines, and those lines as context. The header of the hunk counts the blocks as they are compared and names the
    // lines of the files where the difference starts.
    private static string Difference(List<NcBlock> expected, List<NcBlock> actual, int sourceIndex,
        int compiledIndex, int before)
    {
        Hunk hunk = FindAgreement(expected, actual, sourceIndex, compiledIndex);
        int sourceCount = before + hunk.Removed + hunk.After;
        int compiledCount = before + hunk.Added + hunk.After;
        int sourceStart = sourceIndex - before + (sourceCount > 0 ? 1 : 0);
        int compiledStart = compiledIndex - before + (compiledCount > 0 ? 1 : 0);

        var text = new StringBuilder("--- source\n+++ compiled\n");
        text.Append(CultureInfo.InvariantCulture,
            $"@@ -{sourceStart},{sourceCount} +{compiledStart},{compiledCount} @@ source line ")
            .Append(LineOf(expected, sourceIndex)).Append(", compiled line ").Append(LineOf(actual, compiledIndex))
            .Append('\n');
        AppendLines(text, ' ', expected, sourceIndex - before, before);
        AppendLines(text, '-', expected, sourceIndex, hunk.Removed);
        AppendLines(text, '+', actual, compiledIndex, hunk.Added);
        AppendLines(text, ' ', expected, sourceIndex + hunk.Removed, hunk.After);
        return text.ToString();
    }

    // Where the two programs agree again after a difference: the fewest blocks of the two together that, left out,
    // let the next lines of both agree, up to the end of both; the lines that then agree are the context after it.
    private static Hunk FindAgreement(List<NcBlock> expected, List<NcBlock> actual, int sourceIndex,
        int compiledIndex)
    {
        for (int skipped = 1; skipped <= 2 * LookAhead; skipped++)
        {
            for (int fromSource = Math.Min(skipped, LookAhead); fromSource >= 0 && skipped - fromSource <= LookAhead;
                fromSource--)
            {
                int agreeing = Agreeing(expected, actual, sourceIndex + fromSource,
                    compiledIndex + skipped - fromSource);
                if (agreeing >= 0)
                {
                    return new Hunk(fromSource, skipped - fromSource, agreeing);
                }
            }
        }

        return new Hunk(Math.Min(Unmatched, expected.Count - sourceIndex),
            Math.Min(Unmatched, actual.Count - compiledIndex), 0);
    }

    // The number of lines that agree from these places on, up to the context, when all the lines up to the context or
    // to the end of both programs agree; -1 when they do not agree there.
    private static int Agreeing(List<NcBlock> expected, List<NcBlock> actual, int sourceIndex, int compiledIndex)
    {
        if (sourceIndex > expected.Count || compiledIndex > actual.Count)
        {
            return -1;
        }

        int remaining = Math.Min(expected.Count - sourceIndex, actual.Count - compiledIndex);
        bool bothEnd = expected.Count - sourceIndex == actual.Count - compiledIndex;
        if (remaining == 0 && !bothEnd)
        {
            return -1;
        }

        int wanted = Math.Min(Context, remaining);
        for (int index = 0; index < wanted; index++)
        {
            if (!Same(expected[sourceIndex + index], actual[compiledIndex + index]))
            {
                return -1;
            }
        }

        return wanted;
    }

    private static void AppendLines(StringBuilder text, char mark, List<NcBlock> blocks, int start, int count)
    {
        for (int index = start; index < start + count; index++)
        {
            text.Append(mark).Append(blocks[index].Text).Append('\n');
        }
    }

    // The line of the file a block starts on, or the end when the program has no block there.
    private static string LineOf(List<NcBlock> blocks, int index)
    {
        return index < blocks.Count ? blocks[index].Line.ToString(CultureInfo.InvariantCulture) : "at the end";
    }

    // A line with its runs of blanks made one and every number formatted: the address before the number gives its
    // decimals, X of X+50,4 and of IX-5, none for Q206=565,487.
    private string Normalized(string line)
    {
        string blanks = Blanks().Replace(line.Trim(), " ");
        return NumberWord().Replace(blanks, match => match.Groups["address"].Value + Number(
            match.Groups["address"].Value, match.Groups["number"].Value));
    }

    // A number with the decimals [format] gives its address, rounded as the machine rounds it, without trailing zeros
    // and with the decimal separator of the machine (machine-config 2).
    private string Number(string address, string text)
    {
        decimal value = decimal.Parse(text.Replace(',', '.'), NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        string axis = address.Length > 1 && address[0] == 'I' ? address.Substring(1) : address;
        if (_machine.Format is OutputFormat format && format.Decimals.TryGetValue(axis, out int decimals))
        {
            value = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }

        string written = value == 0m
            ? "0"
            : value.ToString("0.############################", CultureInfo.InvariantCulture);
        return written.Replace(".", _separator, StringComparison.Ordinal);
    }

    // The line without its comments, by the controller of the machine. On Fanuc ( ... ) is a comment, anywhere in the
    // block, and nothing else is (controllers fanuc.md 1): the ( ) comments go, an unclosed one to the end of the line
    // as the Fanuc reader takes it, and a ; stays part of the block, inside a comment or outside. On Klartext and
    // Siemens ; starts a comment at the end of a block or a comment block (controllers heidenhain.md 1, siemens.md 1):
    // the text before the first ; outside quotes.
    private string WithoutComment(string line)
    {
        if (_parenthesesAreComments)
        {
            return FanucComment().Replace(line, "").Trim();
        }

        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            quoted ^= line[index] == '"';
            if (!quoted && line[index] == ';')
            {
                return line.Substring(0, index).Trim();
            }
        }

        return line.Trim();
    }

    // The block number removed, N10 or the 12 of Klartext. The skip mark, / or /n at the block start, is no block
    // number and is kept in front of the words without a blank, so that /2 G0 and /2G0 are one block, /1 and /2 two,
    // and a skipped block never equals an unskipped one (controller-mapping 1, SKIP); the mark after the block number,
    // where the Klartext compiler writes it (12 /L), is the same skip.
    private string WithoutBlockNumber(string line)
    {
        Match match = _blockNumber.Match(line);
        string rest = line.Substring(match.Length);
        return match.Groups["before"].Value + match.Groups["after"].Value + rest;
    }

    // The skip mark and the block number of the controller, the mark taken first so that the n of its switch is never
    // read as a block number (controller-mapping 1, SKIP): on Fanuc / or /n with a switch of 1 to 9 and the block
    // number N10 (controllers fanuc.md 1), on Klartext / and the leading number (heidenhain.md 1), on Siemens / or /0
    // to /9 and N10 (siemens.md 1). The default machine of D103 has no controller and no NC program to compare.
    private static Regex BlockNumberOf(Controller? controller)
    {
        return controller switch
        {
            Controller.Fanuc => FanucBlockNumber(),
            Controller.Heidenhain => KlartextBlockNumber(),
            Controller.Siemens => SiemensBlockNumber(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(controller), controller, "Not a controller family of machine-config 1."),
        };
    }

    [GeneratedRegex(@"^(?<before>/[1-9]?)?\s*(?:N[0-9]+\s*(?<after>/[1-9]?)?)?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex FanucBlockNumber();

    [GeneratedRegex(@"^(?<before>/)?\s*(?:[0-9]+(?=\s|/|$)\s*(?<after>/)?)?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex KlartextBlockNumber();

    [GeneratedRegex(@"^(?<before>/[0-9]?)?\s*(?:N[0-9]+\s*(?<after>/[0-9]?)?)?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SiemensBlockNumber();

    [GeneratedRegex(@"\([^)]*\)?", RegexOptions.CultureInvariant)]
    private static partial Regex FanucComment();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Blanks();

    [GeneratedRegex(@"^O[0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex ProgramNumber();

    [GeneratedRegex(@"^G[0-9]+(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex GCode();

    // A number and the letters of the address in front of it, with an = between them where Klartext writes one.
    [GeneratedRegex(@"(?<address>[A-Z]*=?)(?<number>[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+))(?![0-9])",
        RegexOptions.CultureInvariant)]
    private static partial Regex NumberWord();

    // One hunk of the diff: the blocks of the source that differ, those of the compiled program, and the blocks after
    // them that agree again.
    private sealed record Hunk(int Removed, int Added, int After);
}
