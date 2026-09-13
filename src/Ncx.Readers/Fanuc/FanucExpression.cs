using System.Text;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Translates an expression of custom macro B into an NCX expression (controllers fanuc.md 7; controller-mapping 6;
/// language 4.9, 4.12): [ ] to ( ), #n to $Vn (D33), a system variable through the [system_variables] of the machine
/// (D51), EQ NE GT GE LT LE to the NCX comparisons, SIN COS TAN ASIN ACOS ATAN SQRT ABS ROUND LN EXP to the NCX
/// functions, FIX to INT and FUP to the rounding away from zero. What NCX cannot express, an indirect variable, the
/// vacant #0, an unmapped system variable, XOR, is a problem that keeps the block as RAW.
/// </summary>
internal sealed class FanucExpression
{
    // Custom macro B nests brackets five deep; the reader follows far more and stops before a runaway nesting.
    private const int MaxNesting = 64;

    private readonly string _text;
    private readonly FanucBlock _block;
    private int _position;
    private int _nesting;
    private string? _problem;

    private FanucExpression(string text, FanucBlock block)
    {
        _text = text;
        _block = block;
    }

    /// <summary>
    /// The NCX text of a Fanuc expression, before the expression parser checks it.
    /// </summary>
    /// <param name="fanuc">The expression as the source writes it, "[#502+#11099]", "-#501".</param>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static string? ToText(string fanuc, FanucBlock block, out string? problem)
    {
        var expression = new FanucExpression(fanuc, block);
        string text = expression.Sequence('\0');
        problem = expression._problem;
        return problem is null ? text.Trim() : null;
    }

    /// <summary>
    /// An NCX expression value from its text, in the canonical form the parser gives it, so that the program formats to
    /// itself (language 4.12; D91).
    /// </summary>
    /// <param name="text">The NCX expression without its braces.</param>
    /// <param name="block">The block, whose line a problem cites.</param>
    /// <param name="problem">Why the text is no expression of language 4.12; null when it is one.</param>
    public static ExprValue? Parse(string text, FanucBlock block, out string? problem)
    {
        var scratch = new Diagnostics(block.Diagnostics.File);
        ExprNode? tree = ExprParser.Parse(text, block.Line, scratch);
        if (tree is null)
        {
            problem = $"{{{text}}} is no expression of language 4.12";
            return null;
        }

        problem = null;
        return new ExprValue(tree.ToCanonical(), tree);
    }

    // The expression up to the closing bracket, which it reads, or to the end; the bracket pairs of Fanuc become the
    // parentheses of NCX.
    private string Sequence(char closer)
    {
        var text = new StringBuilder();
        if (++_nesting > MaxNesting)
        {
            _problem = $"[{_text}] nests its brackets deeper than {MaxNesting}";
        }

        while (_problem is null && _position < _text.Length)
        {
            char character = _text[_position];
            if (character == closer)
            {
                _position++;
                return text.ToString();
            }

            if (character is ' ' or '\t')
            {
                _position++;
            }
            else if (character == '[')
            {
                _position++;
                text.Append('(').Append(Sequence(']')).Append(')');
            }
            else if (character == '#')
            {
                _position++;
                text.Append(Variable());
            }
            else if (char.IsAsciiDigit(character) || character == '.')
            {
                text.Append(Number());
            }
            else if (char.IsAsciiLetter(character))
            {
                text.Append(Word());
            }
            else if (character is '+' or '-' or '*' or '/')
            {
                _position++;
                text.Append(' ').Append(character).Append(' ');
            }
            else if (character == ',')
            {
                _position++;
                text.Append(", ");
            }
            else
            {
                _problem = $"the character {character} of [{_text}] has no NCX form";
            }
        }

        if (closer != '\0')
        {
            _problem ??= $"a [ of [{_text}] is not closed";
        }

        return text.ToString();
    }

    // #n: local, common and permanent variables are Vn (D33, language 4.9); #1000 and above are the system variables of
    // the control, which the machine maps to SYS_ names (D51, language 4.12).
    private string Variable()
    {
        if (_position < _text.Length && _text[_position] == '[')
        {
            _problem = $"the indirect variable of [{_text}] has no NCX name (language 4.9)";
            return "";
        }

        int start = _position;
        while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
        {
            _position++;
        }

        string digits = _text.Substring(start, _position - start).TrimStart('0');
        if (digits.Length == 0)
        {
            _problem = "#0, the vacant variable, has no NCX form (language 4.9)";
            return "";
        }

        if (digits.Length <= 3)
        {
            return "$V" + digits;
        }

        return SystemVariable("#" + digits) ?? "";
    }

    // A system variable is the SYS_ name whose native template the machine writes for it, with its index:
    // SYS_WEAR_Z = "#11{index:03}" reads #11099 as $SYS_WEAR_Z[99]; an unmapped one becomes RAW (language 4.12, D51,
    // machine-config 7).
    private string? SystemVariable(string native)
    {
        foreach (KeyValuePair<string, string> entry in _block.Machine.SystemVariables.Entries)
        {
            if (_block.Templates.For(entry.Value) is Template template
                && template.Matches(native, out TemplateValues values))
            {
                return values.TryGetNumber("index", out decimal index)
                    ? "$" + entry.Key + "[" + FanucNumbers.Of(index).ToCanonical() + "]"
                    : "$" + entry.Key;
            }
        }

        _problem = $"{native} is a system variable the machine does not map in [system_variables] (language 4.12, D51)";
        return null;
    }

    // A number with a leading or a trailing dot, 1. and .5, in the form of language 3.
    private string Number()
    {
        int start = _position;
        while (_position < _text.Length && (char.IsAsciiDigit(_text[_position]) || _text[_position] == '.'))
        {
            _position++;
        }

        string written = _text.Substring(start, _position - start);
        Value? number = new SourceWord { Address = "", Text = written }.ToNcxNumber();
        if (number is null)
        {
            _problem = $"{written} of [{_text}] is no number";
            return "";
        }

        return number.ToCanonical();
    }

    // An operator word or a function of custom macro B (controllers fanuc.md 7).
    private string Word()
    {
        int start = _position;
        while (_position < _text.Length && char.IsAsciiLetter(_text[_position]))
        {
            _position++;
        }

        string name = _text.Substring(start, _position - start).ToUpperInvariant();
        switch (name)
        {
            case "EQ":
                return " == ";
            case "NE":
                return " != ";
            case "GT":
                return " > ";
            case "GE":
                return " >= ";
            case "LT":
                return " < ";
            case "LE":
                return " <= ";
            case "AND" or "OR" or "MOD":
                return " " + name + " ";
            case "SIN" or "COS" or "TAN" or "ASIN" or "ACOS" or "SQRT" or "ABS" or "ROUND" or "LN" or "EXP":
                return name + "(" + Argument(name) + ")";
            case "FIX":
                // FIX discards the fraction, INT truncates toward zero (language 4.12).
                return "INT(" + Argument(name) + ")";
            case "FUP":
                return RoundedUp();
            case "ATAN":
                return ArcTangent();
            default:
                _problem = $"{name} of [{_text}] has no NCX form (language 4.12)";
                return "";
        }
    }

    // The bracketed argument of a function, SIN[#3].
    private string Argument(string function)
    {
        while (_position < _text.Length && _text[_position] == ' ')
        {
            _position++;
        }

        if (_position >= _text.Length || _text[_position] != '[')
        {
            _problem = $"{function} of [{_text}] has no [ ] argument";
            return "";
        }

        _position++;
        return Sequence(']');
    }

    // FUP raises the fraction to the next integer away from zero, INT plus the sign of the fraction.
    // TODO(question): language 4.12 does not define FRAC of a negative number (wave-1 question #85); with FRAC(x) =
    // x - INT(x) this form gives FUP[-1.2] = -2, the rounding away from zero.
    private string RoundedUp()
    {
        string argument = Argument("FUP");
        return "(INT(" + argument + ") + SGN(FRAC(" + argument + ")))";
    }

    // ATAN[a]/[b] and ATAN[a,b] are the arc tangent of a over b, ATAN[a] the one of a single argument.
    // TODO(question): the argument order of ATAN2 is not defined (wave-1 question #86); the reader writes ATAN2(a, b)
    // for ATAN[a]/[b], the order of ATAN2(y, x).
    private string ArcTangent()
    {
        string first = Argument("ATAN");
        int afterFirst = _position;
        while (_position < _text.Length && _text[_position] == ' ')
        {
            _position++;
        }

        if (_position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '[')
        {
            _position += 2;
            string second = Sequence(']');
            return "ATAN2(" + first + ", " + second + ")";
        }

        _position = afterFirst;
        return first.Contains(',', StringComparison.Ordinal) ? "ATAN2(" + first + ")" : "ATAN(" + first + ")";
    }
}
