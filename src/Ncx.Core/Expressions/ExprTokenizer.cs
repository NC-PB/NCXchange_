using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Expressions;

/// <summary>
/// Splits the text between the braces of an expression into numbers, names and symbols, the parts the grammar of
/// language 4.12 is written in. Whitespace between them is free and leaves no trace.
/// </summary>
internal static class ExprTokenizer
{
    // The operators and delimiters of one character (language 4.12).
    private const string OneCharacterSymbols = "+-*/^()[],$<>";

    // The operators of two characters, tried before those of one, so that <= is one comparison and not < followed by
    // an unknown = (language 4.12, cmpexpr).
    private static readonly string[] s_twoCharacterSymbols = ["==", "!=", "<=", ">="];

    /// <summary>
    /// The parts of the expression in order, ended by an End part; null after the ERROR for a character that begins
    /// no part of the grammar or a malformed number.
    /// </summary>
    /// <param name="text">The expression without its braces.</param>
    /// <param name="line">The line of the block, which the diagnostic carries.</param>
    /// <param name="diagnostics">The diagnostics of the file.</param>
    public static List<ExprToken>? Tokenize(string text, int line, Diagnostics diagnostics)
    {
        var tokens = new List<ExprToken>();
        int index = 0;
        while (index < text.Length)
        {
            char character = text[index];

            // Whitespace inside {} is free (language 4.12).
            if (character == ' ' || character == '\t')
            {
                index++;
                continue;
            }

            // A number is an integer or a decimal of language 3 without its sign; a minus in front of it is the unary
            // minus of the grammar, so that -2 ^ 2 is the minus of the power (language 3, 4.12, unary).
            if (char.IsAsciiDigit(character) || character == '.')
            {
                string? number = ReadNumber(text, index, line, diagnostics);
                if (number is null)
                {
                    return null;
                }

                tokens.Add(new ExprToken(ExprTokenKind.Number, number, index + 1));
                index += number.Length;
                continue;
            }

            // A name: a function, one of the words AND, OR, NOT and MOD, or after $ a variable name, which follows
            // ADDR (language 4.9, 4.12). Keys, addresses and identifiers are uppercase and a parser may accept
            // lowercase and normalize (language 3, Case), so a variable name in lowercase is normalized.
            // TODO(question): language 3, Case, does not say whether function names and the words AND, OR, NOT and
            // MOD count as identifiers; they are normalized like the variable names until that is settled.
            if (char.IsAsciiLetter(character))
            {
                int end = index + 1;
                while (end < text.Length && IsNameCharacter(text[end]))
                {
                    end++;
                }

                string name = text.Substring(index, end - index).ToUpperInvariant();
                tokens.Add(new ExprToken(ExprTokenKind.Name, name, index + 1));
                index = end;
                continue;
            }

            // An operator or a delimiter of the grammar; any other character is not part of an expression
            // (language 4.12).
            string? symbol = SymbolAt(text, index);
            if (symbol is null)
            {
                diagnostics.Error(line, DiagnosticCodes.ExpressionUnexpectedCharacter,
                    $"The character {character} {Place(index + 1, text)} of {{{text}}} is not part of an expression "
                    + "(language 4.12).");
                return null;
            }

            tokens.Add(new ExprToken(ExprTokenKind.Symbol, symbol, index + 1));
            index += symbol.Length;
        }

        tokens.Add(new ExprToken(ExprTokenKind.End, "", text.Length + 1));
        return tokens;
    }

    /// <summary>
    /// Where a part stands, as a message says it: "at position 5", or "at the end" for one past the last character.
    /// </summary>
    public static string Place(int position, string text)
    {
        if (position > text.Length)
        {
            return "at the end";
        }

        return "at position " + position.ToString(CultureInfo.InvariantCulture);
    }

    // A decimal has digits on both sides of its point; NCX writes no trailing dot and no leading one (language 2
    // rule 5, language 3, decimal).
    private static string? ReadNumber(string text, int start, int line, Diagnostics diagnostics)
    {
        int end = DigitsEnd(text, start);
        if (end == start)
        {
            diagnostics.Error(line, DiagnosticCodes.ExpressionMalformedNumber,
                $"The number {Place(start + 1, text)} of {{{text}}} needs a digit before the decimal point "
                + "(language 3, decimal).");
            return null;
        }

        if (end < text.Length && text[end] == '.')
        {
            int fractionStart = end + 1;
            end = DigitsEnd(text, fractionStart);
            if (end == fractionStart)
            {
                diagnostics.Error(line, DiagnosticCodes.ExpressionMalformedNumber,
                    $"The number {Place(start + 1, text)} of {{{text}}} needs a digit after the decimal point "
                    + "(language 3, decimal).");
                return null;
            }
        }

        return text.Substring(start, end - start);
    }

    // The index after the digits that start at an index.
    private static int DigitsEnd(string text, int start)
    {
        int end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return end;
    }

    // A name continues with letters, digits and underscores, as ADDR does (language 3, ADDR).
    private static bool IsNameCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character == '_';
    }

    // The operator or delimiter that starts at an index; null when none does.
    private static string? SymbolAt(string text, int index)
    {
        if (index + 1 < text.Length)
        {
            string twoCharacters = text.Substring(index, 2);
            foreach (string symbol in s_twoCharacterSymbols)
            {
                if (twoCharacters == symbol)
                {
                    return symbol;
                }
            }
        }

        string oneCharacter = text.Substring(index, 1);
        if (OneCharacterSymbols.Contains(oneCharacter, StringComparison.Ordinal))
        {
            return oneCharacter;
        }

        return null;
    }
}
