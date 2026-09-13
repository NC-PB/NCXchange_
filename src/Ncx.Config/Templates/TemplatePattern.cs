using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ncx.Config.Templates;

/// <summary>
/// The reader side of a template: the regular expression that recognizes the text the template renders and captures
/// each placeholder in a named group (architecture 6, phase 2 P2-02).
/// </summary>
internal static class TemplatePattern
{
    // Blanks between words: any number of blanks or tabs, none included, so that G96 S{value} matches G96S120 and
    // G96  S120 as the sources write them (phase 2, P2-02 and risks).
    private const string Blanks = """[ \t]*""";

    // A line break of the template is a line break of the text, CR LF included, with blanks around it.
    private const string LineBreak = """[ \t]*\r?\n[ \t]*""";

    // The text begins and ends where the template does; blanks before and after it do not count, such as the
    // trailing blank of G411F12. in the Nakamura program (phase 2, risks).
    private const string TextStart = """\A\s*""";
    private const string TextEnd = """\s*\z""";

    // Words: at least one character, as few as reach what the template writes next, on the same line.
    private const string TextGroup = ".+?";

    // The template becomes one pattern over the whole text: its literal text escaped, blanks between words tolerated,
    // M and G codes compared by number, and a named group per placeholder: an integer group for a padded placeholder,
    // words for the placeholders whose values are words, a signed decimal otherwise, with the point and, where the
    // machine reads it, the comma (phase 2, P2-02; architecture 6).
    public static Regex Build(IReadOnlyList<string> literals, IReadOnlyList<Placeholder> placeholders, bool readsComma)
    {
        string numberGroup = NumberGroup(readsComma);
        var pattern = new StringBuilder(TextStart);
        AppendLiteral(pattern, literals[0]);
        for (int index = 0; index < placeholders.Count; index++)
        {
            pattern.Append("(?<").Append(GroupName(index)).Append('>');
            pattern.Append(GroupOf(placeholders[index], numberGroup));
            pattern.Append(')');
            AppendLiteral(pattern, literals[index + 1]);
        }

        pattern.Append(TextEnd);
        return new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
    }

    // The group of the placeholder at this index; the same name may stand twice in a template, the index only once.
    public static string GroupName(int index)
    {
        return "p" + index.ToString(CultureInfo.InvariantCulture);
    }

    // TODO(question): a padded placeholder reads exactly the digits it pads to, so that T{tool:02}{offset:02} splits
    // T0101 into 01 and 01. Whether it also reads a source that leaves the leading zero out (Fanuc T101 for T0101), and
    // what a number wider than its suffix renders to (tool 123 in {tool:02} is written 123, which the pattern does not
    // read back), is open.
    private static string GroupOf(Placeholder placeholder, string numberGroup)
    {
        if (placeholder.IsText)
        {
            return TextGroup;
        }

        if (placeholder.Width > 0)
        {
            return "[0-9]{" + placeholder.Width.ToString(CultureInfo.InvariantCulture) + "}";
        }

        return numberGroup;
    }

    // A number as the controls write it: an optional sign, digits with or without a decimal point, or the point
    // first: 70., -.534, +10, 1592 (controllers fanuc.md 2, heidenhain.md 2). On a machine that reads the comma, the
    // decimal separator of Klartext, a number reads with the comma as well as with the point (controllers
    // heidenhain.md 7 rule 8). Fanuc and Siemens write the dot (controllers differences.md, Numbers), and there the
    // comma separates the arguments of a call, CYCLE832(0.01,1,0.1), and is no number.
    private static string NumberGroup(bool readsComma)
    {
        string separator = readsComma ? """[.,]""" : """\.""";
        return "[+-]?(?:[0-9]+(?:" + separator + "[0-9]*)?|" + separator + "[0-9]+)";
    }

    // Literal text is matched as written, except the blanks between words, the line breaks, and the M and G codes.
    private static void AppendLiteral(StringBuilder pattern, string literal)
    {
        int position = 0;
        while (position < literal.Length)
        {
            char character = literal[position];
            if (character == '\n')
            {
                pattern.Append(LineBreak);
                position++;
            }
            else if (IsBlank(character))
            {
                // A run of blanks is one gap between words, of any width in the text.
                while (position < literal.Length && IsBlank(literal[position]))
                {
                    position++;
                }

                pattern.Append(Blanks);
            }
            else if (IsCode(literal, position))
            {
                position = AppendCode(pattern, literal, position);
            }
            else
            {
                pattern.Append(Regex.Escape(literal.Substring(position, 1)));
                position++;
            }
        }
    }

    private static bool IsBlank(char character)
    {
        return character is ' ' or '\t' or '\r';
    }

    // An M or G code of the literal text: the letter at the start of a word, not inside a name such as WAITM or RG or
    // after an underscore, with digits after it (machine-config 5, D105).
    private static bool IsCode(string literal, int position)
    {
        if (literal[position] is not ('M' or 'G'))
        {
            return false;
        }

        bool digitFollows = position + 1 < literal.Length && char.IsAsciiDigit(literal[position + 1]);
        bool wordStarts = position == 0
            || !(char.IsAsciiLetter(literal[position - 1]) || literal[position - 1] == '_');
        return digitFollows && wordStarts;
    }

    // An M or G code is matched by number: the letter, any number of leading zeros, then the number without its own,
    // so that the normalized M8 of the table matches a source M08 and M008, and G1 matches G01 (machine-config 5,
    // D105). A G code with a decimal part, G7.1 or G68.2, keeps the decimal part as written. Returns the position after
    // the code.
    private static int AppendCode(StringBuilder pattern, string literal, int position)
    {
        int end = position + 1;
        while (end < literal.Length && char.IsAsciiDigit(literal[end]))
        {
            end++;
        }

        string number = literal.Substring(position + 1, end - position - 1).TrimStart('0');
        pattern.Append(literal[position]).Append("0*").Append(number.Length == 0 ? "0" : number);

        bool decimalPart = end + 1 < literal.Length && literal[end] == '.' && char.IsAsciiDigit(literal[end + 1]);
        if (decimalPart)
        {
            int fractionEnd = end + 1;
            while (fractionEnd < literal.Length && char.IsAsciiDigit(literal[fractionEnd]))
            {
                fractionEnd++;
            }

            pattern.Append("""\.""").Append(literal, end + 1, fractionEnd - end - 1);
            end = fractionEnd;
        }

        return end;
    }
}
