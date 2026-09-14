namespace Ncx.Readers.Siemens;

/// <summary>
/// The lexical pieces of a SINUMERIK block (controllers siemens.md 1, 8): identifiers, numbers, strings, the groups in
/// parentheses and brackets, and the extent of a value after an equals sign, which may be an expression (X=R1*2,
/// Z=R40, I=AC(50)). Each method takes a position in the text and gives the position after the piece, -1 where the
/// piece is not there.
/// </summary>
internal static class SiemensScanner
{
    // The operators between two operands of an expression (controllers siemens.md 8): the symbols, longest first, and
    // the operator words.
    private static readonly string[] s_symbols = ["==", "<>", "<=", ">=", "<<", "+", "-", "*", "/", "<", ">"];
    private static readonly string[] s_words = ["AND", "OR", "XOR", "DIV", "MOD", "B_AND", "B_OR", "B_XOR"];

    /// <summary>
    /// The position of the first character after blanks and tabs.
    /// </summary>
    public static int SkipBlanks(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    /// <summary>
    /// True for the first character of an identifier: a letter, the underscore, or $ of a system variable.
    /// </summary>
    public static bool StartsIdentifier(char character)
    {
        return char.IsAsciiLetter(character) || character is '_' or '$';
    }

    /// <summary>
    /// The end of the identifier at the position: letters, digits and underscores, $P_UIFR, GC1_XDREMI, CYCLE81.
    /// </summary>
    public static int ReadIdentifier(string text, int position)
    {
        if (position >= text.Length || !StartsIdentifier(text[position]))
        {
            return -1;
        }

        position++;
        while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] == '_'))
        {
            position++;
        }

        return position;
    }

    /// <summary>
    /// The end of the number at the position: an optional sign, digits with an optional decimal point, at least one
    /// digit, -.534, 70., .08.
    /// </summary>
    public static int ReadNumber(string text, int position)
    {
        int index = position;
        if (index < text.Length && text[index] is '+' or '-')
        {
            index++;
        }

        int digits = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
            digits++;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
                digits++;
            }
        }

        return digits > 0 ? index : -1;
    }

    /// <summary>
    /// The end of the string at the position, after its closing quote.
    /// </summary>
    public static int ReadString(string text, int position)
    {
        if (position >= text.Length || text[position] != '"')
        {
            return -1;
        }

        int end = text.IndexOf('"', position + 1);
        return end < 0 ? -1 : end + 1;
    }

    /// <summary>
    /// The end of the group at the position that opens with ( or [, after the bracket that closes it; strings and
    /// nested groups inside it are skipped.
    /// </summary>
    public static int ReadGroup(string text, int position)
    {
        if (position >= text.Length || text[position] is not ('(' or '['))
        {
            return -1;
        }

        int depth = 0;
        int index = position;
        while (index < text.Length)
        {
            char character = text[index];
            if (character == '"')
            {
                index = ReadString(text, index);
                if (index < 0)
                {
                    return -1;
                }

                continue;
            }

            if (character is '(' or '[')
            {
                depth++;
            }
            else if (character is ')' or ']')
            {
                depth--;
                if (depth == 0)
                {
                    return index + 1;
                }
            }

            index++;
        }

        return -1;
    }

    /// <summary>
    /// The end of the value that follows an equals sign: one operand, and more operands joined by the operators of an
    /// expression, blanks allowed around an operator; the value ends before the next word of the block.
    /// </summary>
    public static int ReadValue(string text, int position)
    {
        int end = ReadOperand(text, position);
        while (end > 0)
        {
            int next = SkipBlanks(text, end);
            int length = OperatorAt(text, next);
            if (length == 0)
            {
                return end;
            }

            int operand = ReadOperand(text, SkipBlanks(text, next + length));
            if (operand < 0)
            {
                return end;
            }

            end = operand;
        }

        return end;
    }

    /// <summary>
    /// The length of the operator between two operands at the position; 0 where none stands there.
    /// </summary>
    public static int OperatorAt(string text, int position)
    {
        foreach (string symbol in s_symbols)
        {
            if (string.CompareOrdinal(text, position, symbol, 0, symbol.Length) == 0)
            {
                return symbol.Length;
            }
        }

        foreach (string word in s_words)
        {
            int end = position + word.Length;
            if (end <= text.Length && string.Compare(text, position, word, 0, word.Length,
                StringComparison.OrdinalIgnoreCase) == 0 && (end == text.Length || !IsIdentifierPart(text[end])))
            {
                return word.Length;
            }
        }

        return 0;
    }

    // One operand: a sign or NOT before an operand, a number, a string, a group in parentheses, or an identifier with
    // its index in brackets and its arguments in parentheses, SIN(30), AC(50), $AA_IW[X], R[1].
    private static int ReadOperand(string text, int position)
    {
        position = SkipBlanks(text, position);
        if (position >= text.Length)
        {
            return -1;
        }

        char character = text[position];
        if (character is '-' or '+')
        {
            int number = ReadNumber(text, position);
            return number > 0 ? number : ReadOperand(text, position + 1);
        }

        if (string.Compare(text, position, "NOT", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
            && position + 3 < text.Length && !IsIdentifierPart(text[position + 3]))
        {
            return ReadOperand(text, position + 3);
        }

        if (char.IsAsciiDigit(character) || character == '.')
        {
            return ReadNumber(text, position);
        }

        if (character == '"')
        {
            return ReadString(text, position);
        }

        if (character == '(')
        {
            return ReadGroup(text, position);
        }

        int end = ReadIdentifier(text, position);
        while (end > 0 && end < text.Length && text[end] is '[' or '(')
        {
            end = ReadGroup(text, end);
        }

        return end;
    }

    private static bool IsIdentifierPart(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character == '_';
    }
}
