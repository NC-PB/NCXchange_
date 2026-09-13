using Ncx.Config.Templates;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Matches a template of the machine against the words of a block (architecture 6, templates both ways): the words the
/// template names, its codes by number and every other word by its address, in the template's order, are the native
/// text the template must match. A builder's tool change G340 T0101. A02. matches "G340 T{tool:02}{offset:02}.
/// A{next:02}." wherever the words stand in the block.
/// </summary>
internal static class FanucTemplates
{
    /// <summary>
    /// Matches a template against the unread words of a block and reads them when it matches.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="text">The template text as the machine record keeps it; null or empty matches nothing.</param>
    /// <param name="values">The values of its placeholders when it matches.</param>
    /// <returns>True when the words of the block are the template.</returns>
    public static bool TryMatch(FanucBlock block, string? text, out TemplateValues values)
    {
        values = new TemplateValues();
        if (string.IsNullOrEmpty(text) || text.Contains('\n', StringComparison.Ordinal)
            || block.Templates.For(text) is not Template template)
        {
            return false;
        }

        var words = new List<SourceWord>();
        foreach (string token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            SourceWord? word = WordFor(block, token, words);
            if (word is null)
            {
                return false;
            }

            words.Add(word);
        }

        var native = new List<string>();
        foreach (SourceWord word in words)
        {
            native.Add(word.Address + word.Text);
        }

        if (!template.Matches(string.Join(" ", native), out values))
        {
            return false;
        }

        foreach (SourceWord word in words)
        {
            block.MarkRead(word);
        }

        return true;
    }

    // The word of the block a token of the template stands for: a literal G or M code by its number (D105), any other
    // token by its address, a word not taken already.
    private static SourceWord? WordFor(FanucBlock block, string token, List<SourceWord> taken)
    {
        int end = 0;
        while (end < token.Length && char.IsAsciiLetter(token[end]))
        {
            end++;
        }

        string address = token.Substring(0, end);
        string rest = token.Substring(end);
        string? code = null;
        if (address is "G" or "M" && rest.Length > 0 && IsNumber(rest))
        {
            code = NativeCode.Of(new SourceWord { Address = address, Text = rest, Number = 0m });
        }

        foreach (SourceWord word in block.Unread())
        {
            bool fits = word.Address == address && (code is null || NativeCode.Of(word) == code);
            if (fits && !IsTaken(word, taken))
            {
                return word;
            }
        }

        return null;
    }

    // Two words of a block may be equal as records, T01 twice; a word is taken when that very word is.
    private static bool IsTaken(SourceWord word, List<SourceWord> taken)
    {
        foreach (SourceWord other in taken)
        {
            if (ReferenceEquals(other, word))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumber(string text)
    {
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character) && character != '.')
            {
                return false;
            }
        }

        return true;
    }
}
