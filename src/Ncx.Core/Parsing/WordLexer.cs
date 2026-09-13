using System.Globalization;
using System.Text;
using Ncx.Core.Catalog;
using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// Reads one word, KEY, KEY:ADDR, KEY=VALUE or KEY:ADDR=VALUE, and tells the value type of its value by its form
/// (language 3, Word, KEY, ADDR, VALUE, value types). Keys, addresses and identifiers are uppercase, and lowercase is
/// normalized (language 3, Case). A word that breaks the rules is a PAR ERROR with its column and is left out.
/// </summary>
internal static class WordLexer
{
    // The first character of a pseudo-word of a generated block, @SAVE and @RESTORE (language 3, KEY; D95).
    private const char PseudoWordMark = '@';

    /// <summary>
    /// Reads a word; null after the ERROR for one that breaks the lexical rules.
    /// </summary>
    /// <param name="token">The word as it stands in the line, between whitespace.</param>
    /// <param name="column">The 1-based column where it starts.</param>
    /// <param name="line">The 1-based line, which the diagnostic carries.</param>
    /// <param name="options">Whether pseudo-words are lexed (D95).</param>
    /// <param name="diagnostics">Where an ERROR goes.</param>
    public static LexedWord? Lex(string token, int column, int line, ParserOptions options, Diagnostics diagnostics)
    {
        // A key starting with @ is a pseudo-word of a generated block, lexed only under the option the expander uses
        // for generated text; in a user file it is the ERROR "pseudo-word in a user file", recognized here and not
        // rejected as an unknown key (language 3, KEY; D95).
        bool isPseudoWord = token[0] == PseudoWordMark;
        if (isPseudoWord && !options.AllowPseudoWords)
        {
            diagnostics.Error(line, DiagnosticCodes.PseudoWordInUserFile, string.Create(CultureInfo.InvariantCulture,
                $"{token} at column {column}: pseudo-word in a user file; @SAVE and @RESTORE stand in generated "
                + $"blocks only (language 3, KEY; 4.15; D95)."));
            return null;
        }

        // KEY and ADDR are [A-Z][A-Z0-9_]* (language 3, KEY, ADDR).
        int keyStart = isPseudoWord ? 1 : 0;
        int keyEnd = NameEnd(token, keyStart);
        if (keyEnd == keyStart)
        {
            return Malformed(token, column + keyStart, "a word starts with its key, a letter", line, diagnostics);
        }

        string key = token.Substring(0, keyEnd).ToUpperInvariant();
        string? addr = null;
        int index = keyEnd;
        if (index < token.Length && token[index] == ':')
        {
            int addrEnd = NameEnd(token, index + 1);
            if (addrEnd == index + 1)
            {
                return Malformed(token, column + index + 1, "the address after the colon starts with a letter", line,
                    diagnostics);
            }

            addr = token.Substring(index + 1, addrEnd - index - 1).ToUpperInvariant();
            index = addrEnd;
        }

        if (index == token.Length)
        {
            return new LexedWord(key, addr, ValueKinds.Bare, "", column);
        }

        if (token[index] != '=')
        {
            return Malformed(token, column + index, $"the key or the address is followed by : or =, not {token[index]}",
                line, diagnostics);
        }

        string value = token.Substring(index + 1);
        var word = new LexedWord(key, addr, ValueKinds.Bare, value, column);
        int valueColumn = column + index + 1;
        return isPseudoWord
            ? LexStateKey(word, valueColumn, line, diagnostics)
            : LexValue(word, valueColumn, line, diagnostics);
    }

    // A value is an integer, a decimal, an identifier, a list, a string or an expression, told apart by its form
    // (language 3, VALUE, value types). The word comes with the value text as written.
    private static LexedWord? LexValue(LexedWord word, int column, int line, Diagnostics diagnostics)
    {
        string value = word.ValueText;
        if (value.Length == 0)
        {
            return MalformedValue(word, column, "an equals sign is followed by the value", line, diagnostics);
        }

        if (value[0] == '"')
        {
            return LexString(word, column, line, diagnostics);
        }

        if (value[0] == '{')
        {
            return LexExpression(word, column, line, diagnostics);
        }

        // A list: identifiers or integers separated by commas without spaces (language 3, list).
        if (value.Contains(','))
        {
            foreach (string item in value.Split(','))
            {
                if (!IsIdentifier(item) && !IsInteger(item))
                {
                    return MalformedValue(word, column,
                        "the items of a list are identifiers or integers separated by commas without spaces", line,
                        diagnostics);
                }
            }

            return word with { Form = ValueKinds.List, ValueText = value.ToUpperInvariant() };
        }

        // A number is written with a digit before and after its decimal point and without a plus sign, and is kept as
        // written (language 2 rule 5; language 3, integer, decimal).
        if (IsInteger(value))
        {
            return word with { Form = ValueKinds.Integer };
        }

        if (IsDecimal(value))
        {
            return word with { Form = ValueKinds.Decimal };
        }

        if (IsIdentifier(value))
        {
            return word with { Form = ValueKinds.Ident, ValueText = value.ToUpperInvariant() };
        }

        return MalformedValue(word, column,
            "a value is an integer, a decimal, an identifier, a list, a string or an expression", line, diagnostics);
    }

    // string = '"' { char | '\"' | '\\' } '"': the content between the quotes with its two escapes resolved, and
    // nothing after the closing quote (language 3, string).
    private static LexedWord? LexString(LexedWord word, int column, int line, Diagnostics diagnostics)
    {
        string value = word.ValueText;
        var content = new StringBuilder();
        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\\')
            {
                if (index + 1 < value.Length && value[index + 1] is '"' or '\\')
                {
                    content.Append(value[index + 1]);
                    index++;
                    continue;
                }

                diagnostics.Error(line, DiagnosticCodes.InvalidStringEscape, string.Create(CultureInfo.InvariantCulture,
                    $"The backslash at column {column + index} in the string of {word.Key} escapes neither a quote "
                    + $"nor a backslash; a string writes \\\" for a quote and \\\\ for a backslash "
                    + $"(language 3, string)."));
                return null;
            }

            if (character == '"')
            {
                if (index != value.Length - 1)
                {
                    return MalformedValue(word, column, "nothing follows the closing quote of a string", line,
                        diagnostics);
                }

                return word with { Form = ValueKinds.String, ValueText = content.ToString() };
            }

            content.Append(character);
        }

        diagnostics.Error(line, DiagnosticCodes.UnclosedString, string.Create(CultureInfo.InvariantCulture,
            $"The string of {word.Key} at column {column} has no closing quote (language 3, string)."));
        return null;
    }

    // expression = "{" expr "}": the text between the braces goes to the expression parser, and nothing follows the
    // closing brace (language 3, expression; 4.12).
    private static LexedWord? LexExpression(LexedWord word, int column, int line, Diagnostics diagnostics)
    {
        string value = word.ValueText;
        int closingBrace = value.IndexOf('}');
        if (closingBrace < 0)
        {
            diagnostics.Error(line, DiagnosticCodes.UnclosedExpression, string.Create(CultureInfo.InvariantCulture,
                $"The expression of {word.Key} at column {column} has no closing brace (language 3, expression)."));
            return null;
        }

        if (closingBrace != value.Length - 1)
        {
            return MalformedValue(word, column, "nothing follows the closing brace of an expression", line,
                diagnostics);
        }

        return word with { Form = ValueKinds.Expr, ValueText = value.Substring(1, closingBrace - 1) };
    }

    // The value of a pseudo-word is a state key KEY[:ADDR] that names a state variable of the channel by the key that
    // sets it, and nothing else (language 3, state key; virtual machine 3.10; D95).
    private static LexedWord? LexStateKey(LexedWord word, int column, int line, Diagnostics diagnostics)
    {
        string value = word.ValueText;
        int keyEnd = NameEnd(value, 0);
        bool isStateKey = keyEnd > 0
            && (keyEnd == value.Length
                || (value[keyEnd] == ':' && NameEnd(value, keyEnd + 1) == value.Length && keyEnd + 1 < value.Length));
        if (!isStateKey)
        {
            return MalformedValue(word, column, "a pseudo-word takes a state key, KEY or KEY:ADDR (D95)", line,
                diagnostics);
        }

        return word with { Form = ValueKinds.StateKey, ValueText = value.ToUpperInvariant() };
    }

    // The index after the name [A-Z][A-Z0-9_]* that starts at an index, lowercase accepted (language 3, KEY, ADDR,
    // identifier, Case); the start itself when no name starts there.
    private static int NameEnd(string text, int start)
    {
        if (start >= text.Length || !char.IsAsciiLetter(text[start]))
        {
            return start;
        }

        int end = start + 1;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
        {
            end++;
        }

        return end;
    }

    // identifier = [A-Z][A-Z0-9_]* (language 3).
    private static bool IsIdentifier(string text)
    {
        return text.Length > 0 && NameEnd(text, 0) == text.Length;
    }

    // integer = -?[0-9]+ (language 3).
    private static bool IsInteger(string text)
    {
        int digitsStart = text.StartsWith('-') ? 1 : 0;
        return DigitsEnd(text, digitsStart) == text.Length && text.Length > digitsStart;
    }

    // decimal = -?[0-9]+\.[0-9]+ (language 3).
    private static bool IsDecimal(string text)
    {
        int digitsStart = text.StartsWith('-') ? 1 : 0;
        int point = DigitsEnd(text, digitsStart);
        if (point == digitsStart || point >= text.Length || text[point] != '.')
        {
            return false;
        }

        int fractionEnd = DigitsEnd(text, point + 1);
        return fractionEnd == text.Length && fractionEnd > point + 1;
    }

    private static int DigitsEnd(string text, int start)
    {
        int end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return end;
    }

    // A word that is not KEY, KEY=VALUE or KEY:ADDR=VALUE, with the column where it breaks (language 3, Word).
    private static LexedWord? Malformed(string token, int column, string rule, int line, Diagnostics diagnostics)
    {
        diagnostics.Error(line, DiagnosticCodes.MalformedWord, string.Create(CultureInfo.InvariantCulture,
            $"The word {token} breaks at column {column}: {rule}; a word is KEY, KEY=VALUE or KEY:ADDR=VALUE "
            + $"(language 3, Word)."));
        return null;
    }

    // A value that is none of the value types of language 3, with the column where it starts.
    private static LexedWord? MalformedValue(LexedWord word, int column, string rule, int line,
        Diagnostics diagnostics)
    {
        diagnostics.Error(line, DiagnosticCodes.MalformedValue, string.Create(CultureInfo.InvariantCulture,
            $"The value {word.ValueText} of {word.Key} at column {column} is no value of language 3: {rule} "
            + $"(language 3, VALUE)."));
        return null;
    }
}
