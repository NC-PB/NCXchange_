using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Machine;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The comparison rules of phase 3 (docs/implementation/13-phase-3-readers-compilers.md, "Comparison rules"): two NC
/// programs are equivalent when, after removing block numbers, comments and blank lines, trimming whitespace, and
/// formatting every number of both sides with the target machine's [format] decimals (so 70. and 70.0 and 70 are
/// equal, and -.534 equals -0.534), the remaining lines are equal in order; the header line and a TOOL CALL or T M6
/// line are compared as a set of words; everything else, the order of the words inside a motion block included, must
/// match. A difference the documents cannot settle is an exception grounded in its question, used exactly once. Written
/// for the Heidenhain compiler (P3-04), for the round trips of P3-07 to adopt.
/// </summary>
internal sealed partial class NcEquivalence
{
    // Lines of context in a difference.
    private const int Context = 3;

    private readonly MachineConfig _machine;
    private readonly string _separator;

    /// <summary>
    /// The comparison with the [format] of the target machine.
    /// </summary>
    public NcEquivalence(MachineConfig machine)
    {
        _machine = machine;
        _separator = machine.Format?.DecimalSeparator ?? ".";
    }

    /// <summary>
    /// The blocks of an NC program as the rules compare them: block numbers, comments (; to the end of the line, ( ) on
    /// Fanuc, the * - structuring blocks of Klartext) and blank lines removed, the continued lines of a Klartext block
    /// (~) joined, whitespace trimmed and runs of it made one blank, every number formatted with the decimals of its
    /// address and the decimal separator of the machine, without a plus sign and without trailing zeros.
    /// </summary>
    public List<string> Blocks(string text)
    {
        var blocks = new List<string>();
        string continued = "";
        foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string content = line.Trim();
            bool continues = content.EndsWith('~');
            content = WithoutComment(continues ? content.Substring(0, content.Length - 1) : content);
            content = continued.Length == 0 ? BlockNumber().Replace(content, "${skip}") : content;
            string joined = (continued + " " + content).Trim();
            if (continues)
            {
                continued = joined;
                continue;
            }

            continued = "";
            if (joined.Length > 0 && !joined.StartsWith('*'))
            {
                blocks.Add(Normalized(joined));
            }
        }

        if (continued.Length > 0)
        {
            blocks.Add(Normalized(continued));
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
    /// <returns>Null when the programs are equivalent; otherwise the first difference as a unified diff, or the
    /// exception that was never used.</returns>
    public string? Compare(string source, string compiled, IReadOnlyList<NcException> exceptions)
    {
        List<string> expected = Blocks(source);
        List<string> actual = Blocks(compiled);
        int sourceIndex = 0;
        int compiledIndex = 0;
        int next = 0;
        while (sourceIndex < expected.Count || compiledIndex < actual.Count)
        {
            if (sourceIndex < expected.Count && compiledIndex < actual.Count
                && Same(expected[sourceIndex], actual[compiledIndex], sourceIndex == 0 && compiledIndex == 0))
            {
                sourceIndex++;
                compiledIndex++;
                continue;
            }

            if (next < exceptions.Count && Matches(exceptions[next].Source, expected, sourceIndex)
                && Matches(exceptions[next].Compiled, actual, compiledIndex))
            {
                sourceIndex += exceptions[next].Source.Count;
                compiledIndex += exceptions[next].Compiled.Count;
                next++;
                continue;
            }

            return Difference(expected, actual, sourceIndex, compiledIndex);
        }

        return next < exceptions.Count
            ? $"The exception grounded in {exceptions[next].Grounds} matches no difference of the two programs."
            : null;
    }

    // Two lines are equal as text, or, for the header line and a tool change, as a set of words (phase 3, comparison
    // rules).
    private static bool Same(string expected, string actual, bool header)
    {
        if (expected == actual)
        {
            return true;
        }

        bool asSet = header || (IsToolChange(expected) && IsToolChange(actual));
        return asSet && SameWords(expected, actual);
    }

    private static bool IsToolChange(string line)
    {
        List<string> words = [.. line.Split(' ')];
        bool toolWord = words.Exists(word => word.Length > 1 && word[0] == 'T' && char.IsAsciiDigit(word[1]));
        return line.StartsWith("TOOL CALL", StringComparison.Ordinal) || (toolWord && words.Contains("M6"));
    }

    private static bool SameWords(string expected, string actual)
    {
        var expectedWords = new List<string>(expected.Split(' '));
        var actualWords = new List<string>(actual.Split(' '));
        expectedWords.Sort(StringComparer.Ordinal);
        actualWords.Sort(StringComparer.Ordinal);
        return expectedWords.SequenceEqual(actualWords);
    }

    private bool Matches(IReadOnlyList<string> lines, List<string> blocks, int start)
    {
        if (start + lines.Count > blocks.Count)
        {
            return false;
        }

        for (int index = 0; index < lines.Count; index++)
        {
            if (Normalized(lines[index]) != blocks[start + index])
            {
                return false;
            }
        }

        return true;
    }

    // The first difference with the lines around it, the source as - and the compiled program as +.
    private static string Difference(List<string> expected, List<string> actual, int sourceIndex, int compiledIndex)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"@@ block {sourceIndex + 1} of the source, block {compiledIndex + 1} of the compiled program @@\n");
        for (int index = Math.Max(0, sourceIndex - Context); index < sourceIndex; index++)
        {
            text.Append("  ").Append(expected[index]).Append('\n');
        }

        for (int index = sourceIndex; index < Math.Min(expected.Count, sourceIndex + Context); index++)
        {
            text.Append("- ").Append(expected[index]).Append('\n');
        }

        for (int index = compiledIndex; index < Math.Min(actual.Count, compiledIndex + Context); index++)
        {
            text.Append("+ ").Append(actual[index]).Append('\n');
        }

        return text.ToString();
    }

    // A line with its runs of blanks made one and every number formatted: the address before the number gives its
    // decimals, X of X+50,4 and of IX-5, none for Q206=565,487.
    private string Normalized(string line)
    {
        string blanks = Blanks().Replace(line.Trim(), " ");
        return NumberWord().Replace(blanks, match => match.Groups["address"].Value + Number(
            match.Groups["address"].Value, match.Groups["number"].Value));
    }

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

    // The text before the first ; outside quotes, and without the ( ) comments of Fanuc.
    private static string WithoutComment(string line)
    {
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            quoted ^= line[index] == '"';
            if (!quoted && line[index] == ';')
            {
                return FanucComment().Replace(line.Substring(0, index), "").Trim();
            }
        }

        return FanucComment().Replace(line, "").Trim();
    }

    // The block number, N10 or 12, with the / of the optional skip in front of it or after it kept at the start.
    [GeneratedRegex(@"^(?<skip>/?)\s*N?[0-9]+(?=\s|/|$)\s*(?:(?<=\s)/|/)?", RegexOptions.CultureInvariant)]
    private static partial Regex BlockNumber();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex FanucComment();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Blanks();

    // A number and the letters of the address in front of it, with an = between them where Klartext writes one.
    [GeneratedRegex(@"(?<address>[A-Z]*=?)(?<number>[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+))(?![0-9])",
        RegexOptions.CultureInvariant)]
    private static partial Regex NumberWord();
}
