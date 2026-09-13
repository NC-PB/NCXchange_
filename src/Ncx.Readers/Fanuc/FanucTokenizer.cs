using System.Globalization;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Cuts a Fanuc or ISO program into source blocks, one per line, CR LF or LF (controllers fanuc.md 1, 2, 7): the % of
/// the tape; O, N and the addresses with their values, several G and up to three M in a block; comments in parentheses
/// anywhere; / and /n at the block start; numbers with a leading or a trailing dot, #n variables and [ ] expressions as
/// values; the macro statements #n = ..., IF, THEN, GOTO, WHILE, DO and END; ,C and ,R after a motion; and the
/// extended axis names of the 30i with their equals sign, C2= (D30).
/// </summary>
internal sealed class FanucTokenizer : ISourceTokenizer
{
    // The addresses that carry an extended axis name with a number and an equals sign, C2=180. and the incremental
    // H2= (D30, controller-mapping 2).
    private const string ExtendedAxisLetters = "ABCHUVWXYZ";

    // The words of custom macro B that are not an address and a value (controllers fanuc.md 7), longest first, so
    // that no word is read as the start of another.
    private static readonly string[] s_keywords = ["WHILE", "GOTO", "THEN", "END", "IF", "DO"];

    // The line being cut and the position in it.
    private string _line = "";
    private int _position;

    private char Current => _position < _line.Length ? _line[_position] : '\0';

    private bool AtEnd => _position >= _line.Length;

    /// <summary>
    /// Cuts the whole file; every line is a block in file order, a blank or comment-only line a block without words.
    /// </summary>
    /// <param name="text">The whole text, line endings as in the file.</param>
    public IEnumerable<SourceBlock> Tokenize(string text)
    {
        var blocks = new List<SourceBlock>();
        string[] lines = text.Split('\n');
        int count = text.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        for (int index = 0; index < count; index++)
        {
            string line = lines[index];
            if (line.EndsWith('\r'))
            {
                line = line.Substring(0, line.Length - 1);
            }

            blocks.Add(ReadLine(line, index + 1));
        }

        return blocks;
    }

    private SourceBlock ReadLine(string line, int number)
    {
        _line = line;
        _position = 0;
        SkipBlanks();

        // / or /n at the block start is the optional block skip; the block runs unless switch n is on (controllers
        // fanuc.md 1, language 4.1).
        bool blockSkip = false;
        int? skipSwitch = null;
        if (Current == '/')
        {
            blockSkip = true;
            _position++;
            if (Current is >= '1' and <= '9')
            {
                skipSwitch = Current - '0';
                _position++;
            }
        }

        var words = new List<SourceWord>();
        var comments = new List<string>();
        while (!AtEnd)
        {
            char character = Current;
            if (character is ' ' or '\t')
            {
                _position++;
            }
            else if (character == '(')
            {
                // ( ... ) is a comment, anywhere in the block; nothing else is (controllers fanuc.md 1).
                comments.Add(ReadComment());
            }
            else if (character == '%')
            {
                // % opens and closes the tape (controllers fanuc.md 1).
                words.Add(new SourceWord { Address = "%" });
                _position++;
            }
            else if (character == '#')
            {
                words.Add(ReadAssignment());
            }
            else if (character == ',')
            {
                words.Add(ReadCornerWord());
            }
            else if (char.IsAsciiLetter(character))
            {
                words.Add(ReadWord());
            }
            else
            {
                // A character that begins no word of the syntax is kept as a word of its own, so that the reader keeps
                // the block as RAW and nothing is lost (D5).
                words.Add(new SourceWord { Address = character.ToString() });
                _position++;
            }
        }

        // Several comments of one block are one comment, their texts in order (controller-mapping 1).
        string? comment = comments.Count == 0 ? null : string.Join(" ", comments);
        return new SourceBlock
        {
            Line = number,
            Text = line,
            Words = words,
            Comment = comment,
            BlockSkip = blockSkip,
            SkipSwitch = skipSwitch,
        };
    }

    // An address with its value, or a word of custom macro B: IF [...], WHILE [...], GOTO n, DO m, END m, THEN.
    private SourceWord ReadWord()
    {
        string? keyword = KeywordAtPosition();
        if (keyword is not null)
        {
            _position += keyword.Length;
            if (keyword is "IF" or "WHILE")
            {
                SkipBlanks();
                string condition = Current == '[' ? ReadBracket() : "";
                return new SourceWord { Address = keyword, Text = condition, Expression = condition };
            }

            return keyword == "THEN" ? new SourceWord { Address = keyword } : ValueOf(keyword);
        }

        string letter = char.ToUpperInvariant(Current).ToString();
        _position++;

        // An extended axis name is the letter, a number and an equals sign: C2=180. (D30, controller-mapping 2).
        int afterLetter = _position;
        while (char.IsAsciiDigit(Current))
        {
            _position++;
        }

        int afterDigits = _position;
        SkipBlanks();
        if (afterDigits > afterLetter && Current == '='
            && ExtendedAxisLetters.Contains(letter, StringComparison.Ordinal))
        {
            string name = _line.Substring(afterLetter - 1, afterDigits - afterLetter + 1).ToUpperInvariant();
            _position++;
            return ValueOf(name);
        }

        _position = afterLetter;
        return ValueOf(letter);
    }

    // The value after an address: a number with a leading or a trailing dot, a #n variable, a [ ] expression, each
    // with a sign in front (controllers fanuc.md 2, 7).
    private SourceWord ValueOf(string address)
    {
        SkipBlanks();
        int start = _position;
        if (Current is '+' or '-')
        {
            _position++;
            SkipBlanks();
        }

        bool expression = false;
        if (Current == '#')
        {
            expression = true;
            _position++;
            if (Current == '[')
            {
                ReadBracket();
            }
            else
            {
                SkipDigits();
            }
        }
        else if (Current == '[')
        {
            expression = true;
            ReadBracket();
        }
        else
        {
            while (char.IsAsciiDigit(Current) || Current == '.')
            {
                _position++;
            }
        }

        string text = _line.Substring(start, _position - start).Replace(" ", "", StringComparison.Ordinal);
        if (expression)
        {
            return new SourceWord { Address = address, Text = text, Expression = text };
        }

        decimal? number = decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
        return new SourceWord { Address = address, Text = text, Number = number };
    }

    // #n = expression, the assignment of custom macro B; the variable may be indirect, #[#1 + 1] (controllers fanuc.md
    // 7). The word keeps the variable as its text and the right side as its expression.
    private SourceWord ReadAssignment()
    {
        _position++;
        int start = _position;
        if (Current == '[')
        {
            ReadBracket();
        }
        else
        {
            SkipDigits();
        }

        string variable = _line.Substring(start, _position - start);
        SkipBlanks();
        if (Current != '=')
        {
            return new SourceWord { Address = "#", Text = variable };
        }

        _position++;
        int valueStart = _position;
        while (!AtEnd && Current != '(')
        {
            _position++;
        }

        string value = _line.Substring(valueStart, _position - valueStart).Trim();
        return new SourceWord { Address = "#", Text = variable, Expression = value };
    }

    // ,C2 and ,R4 attached to a motion block: chamfer and corner rounding (controllers fanuc.md 4).
    private SourceWord ReadCornerWord()
    {
        _position++;
        SkipBlanks();
        if (char.ToUpperInvariant(Current) is 'C' or 'R')
        {
            string address = "," + char.ToUpperInvariant(Current);
            _position++;
            return ValueOf(address);
        }

        return new SourceWord { Address = "," };
    }

    // The text of a comment without its parentheses, trimmed; a comment without its closing parenthesis runs to the end
    // of the line.
    private string ReadComment()
    {
        _position++;
        int start = _position;
        while (!AtEnd && Current != ')')
        {
            _position++;
        }

        string text = _line.Substring(start, _position - start).Trim();
        if (!AtEnd)
        {
            _position++;
        }

        return text;
    }

    // A [ ] expression with its nested brackets, the brackets included; an unclosed one runs to the end of the line.
    private string ReadBracket()
    {
        int start = _position;
        int depth = 0;
        while (!AtEnd)
        {
            char character = Current;
            _position++;
            if (character == '[')
            {
                depth++;
            }
            else if (character == ']' && --depth == 0)
            {
                break;
            }
        }

        return _line.Substring(start, _position - start);
    }

    // The keyword of custom macro B at the position, when no letter follows it: GOTO9090, IF[, DO1, END1.
    private string? KeywordAtPosition()
    {
        foreach (string keyword in s_keywords)
        {
            int end = _position + keyword.Length;
            if (end <= _line.Length
                && string.Compare(_line, _position, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (end == _line.Length || !char.IsAsciiLetter(_line[end])))
            {
                return keyword;
            }
        }

        return null;
    }

    private void SkipBlanks()
    {
        while (Current is ' ' or '\t')
        {
            _position++;
        }
    }

    private void SkipDigits()
    {
        while (char.IsAsciiDigit(Current))
        {
            _position++;
        }
    }
}
