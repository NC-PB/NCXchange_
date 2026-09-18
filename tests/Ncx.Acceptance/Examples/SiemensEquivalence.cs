using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The comparison rules of phase 3 (implementation 13, P3-07, "Comparison rules") for two SINUMERIK programs: block
/// numbers, comments and blank lines left out, whitespace trimmed, every number compared by its value (70. equals 70
/// and -.534 equals -0.534), the empty positions at the end of a call left out; the modal lines before the first line
/// that moves or changes the tool compared as the state they leave per G group of controllers siemens.md 2, since the
/// reader writes the complete header of D34 before them; everything else line by line in order.
/// </summary>
internal static partial class SiemensEquivalence
{
    // The G groups of controllers siemens.md 2 that a header sets, by code.
    private static readonly Dictionary<string, string> s_groups = new(StringComparer.Ordinal)
    {
        ["G17"] = "plane",
        ["G18"] = "plane",
        ["G19"] = "plane",
        ["G40"] = "compensation",
        ["G41"] = "compensation",
        ["G42"] = "compensation",
        ["G70"] = "units",
        ["G71"] = "units",
        ["G700"] = "units",
        ["G710"] = "units",
        ["G90"] = "distance",
        ["G91"] = "distance",
        ["G93"] = "feed",
        ["G94"] = "feed",
        ["G95"] = "feed",
        ["DIAMON"] = "diameter",
        ["DIAMOF"] = "diameter",
        ["DIAM90"] = "diameter",
        ["G0"] = "motion",
        ["G1"] = "motion",
        ["G2"] = "motion",
        ["G3"] = "motion",
    };

    /// <summary>
    /// Compares a source with the program compiled from it.
    /// </summary>
    /// <returns>Null when they are equivalent, else the first difference with the lines around it.</returns>
    public static string? Compare(string source, string compiled)
    {
        List<string> expected = Normalized(source);
        List<string> actual = Normalized(compiled);
        int expectedBody = HeaderEnd(expected);
        int actualBody = HeaderEnd(actual);
        string expectedHeader = HeaderState(expected.GetRange(0, expectedBody));
        string actualHeader = HeaderState(actual.GetRange(0, actualBody));
        if (expectedHeader != actualHeader)
        {
            return $"The headers differ:\n  source:   {expectedHeader}\n  compiled: {actualHeader}";
        }

        int count = Math.Max(expected.Count - expectedBody, actual.Count - actualBody);
        for (int index = 0; index < count; index++)
        {
            string? left = expectedBody + index < expected.Count ? expected[expectedBody + index] : null;
            string? right = actualBody + index < actual.Count ? actual[actualBody + index] : null;
            if (left != right)
            {
                return Around(expected, expectedBody + index, "source") + Around(actual, actualBody + index,
                    "compiled");
            }
        }

        return null;
    }

    // The lines without block numbers, comments and blank lines, every number by its value.
    private static List<string> Normalized(string text)
    {
        var lines = new List<string>();
        foreach (string raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = BlockNumber().Replace(WithoutComment(raw).Trim(), "").Trim();
            if (line.Length == 0)
            {
                continue;
            }

            line = TrailingEmpty().Replace(line, ")");
            lines.Add(Number().Replace(line, match => Value(match.Value)));
        }

        return lines;
    }

    // A ; outside a string starts a comment (controllers siemens.md 1).
    private static string WithoutComment(string line)
    {
        bool inString = false;
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                inString = !inString;
            }
            else if (line[index] == ';' && !inString)
            {
                return line.Substring(0, index);
            }
        }

        return line;
    }

    private static string Value(string number)
    {
        return decimal.TryParse(number.TrimEnd('.'), NumberStyles.Number, CultureInfo.InvariantCulture,
            out decimal value)
            ? (value == 0m ? 0m : value).ToString("0.############################", CultureInfo.InvariantCulture)
            : number;
    }

    // The header ends at the first line that names an axis, a tool or a unit.
    private static int HeaderEnd(List<string> lines)
    {
        int index = 0;
        while (index < lines.Count && (lines[index].StartsWith('%') || IsModal(lines[index])))
        {
            index++;
        }

        return index;
    }

    private static bool IsModal(string line)
    {
        foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!s_groups.ContainsKey(word))
            {
                return false;
            }
        }

        return true;
    }

    // The state the modal lines leave, per group.
    private static string HeaderState(List<string> lines)
    {
        var state = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (s_groups.TryGetValue(word, out string? group))
                {
                    state[group] = word;
                }
            }
        }

        var text = new StringBuilder();
        foreach (KeyValuePair<string, string> entry in state)
        {
            text.Append(entry.Key).Append('=').Append(entry.Value).Append(' ');
        }

        return text.ToString().TrimEnd();
    }

    private static string Around(List<string> lines, int index, string name)
    {
        var text = new StringBuilder($"  {name} line {(index + 1).ToString(CultureInfo.InvariantCulture)}:\n");
        for (int line = Math.Max(0, index - 2); line <= Math.Min(lines.Count - 1, index + 2); line++)
        {
            text.Append(line == index ? "  > " : "    ").Append(lines[line]).Append('\n');
        }

        return text.ToString();
    }

    // N10 in front of a block, after its skip mark (controllers siemens.md 1).
    [GeneratedRegex("^N[0-9]+ ?", RegexOptions.CultureInvariant)]
    private static partial Regex BlockNumber();

    // The empty positions at the end of a call: CYCLE81(11,6,5,-50,) (controllers siemens.md 7).
    [GeneratedRegex(",+\\)", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingEmpty();

    // A number with its sign, its decimal point and the point without digits after it: -.534, 70., 1500, the 01 of G01
    // (D105).
    [GeneratedRegex(@"(?<![0-9.])-?(?:[0-9]+\.?[0-9]*|\.[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Number();
}
