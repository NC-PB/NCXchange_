using System.Text;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// TODO(question): machine-config 2 names one comment_charset, ASCII, whose umlauts are transliterated before writing.
// No document says what a machine file without comment_charset, or with another value, writes, nor what becomes of a
// character outside ASCII that is no umlaut (Ø, °, é). Without ASCII the comment is written as it is; under ASCII
// the umlauts become ae, oe, ue and ss, and any other character outside ASCII becomes ? with a WARNING that names
// it, until that is answered.

/// <summary>
/// comment_charset of [format]: the characters a comment is written in, the umlauts transliterated before writing
/// (machine-config 2; controllers fanuc.md 10 rule 5).
/// </summary>
public static class CommentCharset
{
    /// <summary>
    /// comment_charset = "ASCII" (machine-config 2).
    /// </summary>
    public const string Ascii = "ASCII";

    // What stands for a character that ASCII cannot write and that has no transliteration.
    private const char Unwritable = '?';

    // The first character that is no ASCII.
    private const char FirstBeyondAscii = '';

    // The umlauts and their transliteration (machine-config 2), a capital as a capital followed by a small letter.
    private static readonly Dictionary<char, string> s_umlauts = new()
    {
        ['ä'] = "ae",
        ['ö'] = "oe",
        ['ü'] = "ue",
        ['ß'] = "ss",
        ['Ä'] = "Ae",
        ['Ö'] = "Oe",
        ['Ü'] = "Ue",
    };

    /// <summary>
    /// A comment text in the charset of the machine: under ASCII the umlauts transliterated, ÄNDERUNG as AENDERUNG and
    /// Änderung as Aenderung (machine-config 2).
    /// </summary>
    /// <param name="text">The comment as the program writes it.</param>
    /// <param name="charset">comment_charset of the machine; null when the file leaves it out.</param>
    /// <param name="block">The block of the comment, which a WARNING cites (D98).</param>
    /// <param name="diagnostics">Where the WARNING about a character without transliteration goes.</param>
    public static string Transliterate(string text, string? charset, Block block, Diagnostics diagnostics)
    {
        if (charset != Ascii)
        {
            return text;
        }

        var written = new StringBuilder();
        var unwritable = new List<char>();
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character < FirstBeyondAscii)
            {
                written.Append(character);
            }
            else if (s_umlauts.TryGetValue(character, out string? umlaut))
            {
                written.Append(InCaseOfItsWord(umlaut, text, index));
            }
            else
            {
                written.Append(Unwritable);
                if (!unwritable.Contains(character))
                {
                    unwritable.Add(character);
                }
            }
        }

        if (unwritable.Count > 0)
        {
            diagnostics.Warning(block, DiagnosticCodes.CommentCharacterOutsideCharset,
                $"The comment \"{text}\" has {string.Join(", ", unwritable)}, which comment_charset = \"ASCII\" does "
                + "not hold and which has no transliteration, so ? is written for it (machine-config 2).");
        }

        return written.ToString();
    }

    // A capital umlaut in a word of capitals is written in capitals: before a capital, or after one at the end of a
    // word.
    private static string InCaseOfItsWord(string umlaut, string text, int index)
    {
        if (!char.IsUpper(umlaut[0]))
        {
            return umlaut;
        }

        bool capitalAfter = index + 1 < text.Length && char.IsUpper(text[index + 1]);
        bool endOfWord = index + 1 >= text.Length || !char.IsLetter(text[index + 1]);
        bool capitalBefore = index > 0 && char.IsUpper(text[index - 1]);
        return capitalAfter || (endOfWord && capitalBefore) ? umlaut.ToUpperInvariant() : umlaut;
    }
}
