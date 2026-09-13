using System.Globalization;
using System.Text;
using Ncx.Core.Machine;

namespace Ncx.Core.Expander;

/// <summary>
/// The placeholder {position:NAME} of a pre or post block, which stands for the axis words of that entry of the
/// machine's [positions] table and is replaced before the block is parsed (machine-config 5a, architecture 5.5, D100).
/// </summary>
internal static class PositionPlaceholder
{
    private const string Opening = "{position:";

    /// <summary>
    /// The first name of a {position:NAME} of the text that [positions] does not have; null when every one names an
    /// entry, or the text has none.
    /// </summary>
    public static string? UnknownName(string text, PositionsTable positions)
    {
        var unknownNames = new List<string>();
        Replace(text, positions, unknownNames);
        return unknownNames.Count > 0 ? unknownNames[0] : null;
    }

    /// <summary>
    /// The text with every {position:NAME} replaced by the axis words of its entry in the order of the file: RAPID
    /// {position:tool_change} FRAME=MACHINE with tool_change = { X = 0, Z = -120 } is RAPID X=0 Z=-120 FRAME=MACHINE
    /// (D100). A name [positions] does not have stays as written; UnknownName finds it first.
    /// </summary>
    public static string Expand(string text, PositionsTable positions)
    {
        return Replace(text, positions, []);
    }

    // Every {position:NAME} of the text: a known name becomes the axis words of its entry, an unknown one stays as
    // written and is collected. A brace that is never closed is left to the parser.
    private static string Replace(string text, PositionsTable positions, List<string> unknownNames)
    {
        var replaced = new StringBuilder();
        int from = 0;
        int opening = text.IndexOf(Opening, StringComparison.Ordinal);
        while (opening >= 0)
        {
            int closing = text.IndexOf('}', opening + Opening.Length);
            if (closing < 0)
            {
                break;
            }

            string name = text.Substring(opening + Opening.Length, closing - opening - Opening.Length);
            replaced.Append(text, from, opening - from);
            if (positions.Find(name) is IReadOnlyDictionary<string, decimal> axes)
            {
                replaced.Append(AxisWords(axes));
            }
            else
            {
                unknownNames.Add(name);
                replaced.Append(text, opening, closing + 1 - opening);
            }

            from = closing + 1;
            opening = text.IndexOf(Opening, from, StringComparison.Ordinal);
        }

        replaced.Append(text, from, text.Length - from);
        return replaced.ToString();
    }

    // The axis words of a position, X=0 Z=-120, with the values in machine coordinates as the machine file writes them
    // (machine-config 4, D100). Whether the X of a diameter-programmed axis is a diameter or a radius is wave-1
    // question #4; the value is copied as written, and the virtual machine reads it as it reads every X word (D60).
    private static string AxisWords(IReadOnlyDictionary<string, decimal> axes)
    {
        var words = new List<string>();
        foreach (KeyValuePair<string, decimal> axis in axes)
        {
            words.Add(axis.Key + "=" + axis.Value.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(' ', words);
    }
}
