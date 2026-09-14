using System.Text;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// Translates a Klartext formula into an NCX expression (controllers heidenhain.md 6; controller-mapping 6; language
/// 4.9, 4.12): the Q parameters keep their names, Q5 as $Q5, QL5 as $QL5, QR5 as $QR5; + - * / ^ and the parentheses
/// stay, % is MOD; SIN COS TAN ASIN ACOS ATAN SQRT ABS INT FRAC SGN LN EXP take the operand after them as their
/// argument, NEG is the minus, LOG the logarithm to the base 10. What NCX cannot express keeps the block RAW.
/// </summary>
internal sealed class HeidenhainExpression
{
    // The functions of the formula syntax (controllers heidenhain.md 6); NEG and LOG have no NCX function of their own.
    private static readonly string[] s_functions =
        ["SIN", "COS", "TAN", "ASIN", "ACOS", "ATAN", "SQRT", "ABS", "INT", "FRAC", "SGN", "NEG", "LN", "LOG", "EXP"];

    private readonly List<string> _tokens = [];
    private readonly string _klartext;
    private int _position;
    private string? _problem;

    private HeidenhainExpression(string klartext)
    {
        _klartext = klartext;
    }

    private string Current => _position < _tokens.Count ? _tokens[_position] : "";

    /// <summary>
    /// The NCX value of a Klartext value: the number as written, "+10" as 10 and "-6,964" as -6.964, or the expression
    /// of a formula, "+Q1" as {$Q1} (language 2 rule 5, 4.12; controllers heidenhain.md 6).
    /// </summary>
    /// <param name="klartext">The value as the source writes it.</param>
    /// <param name="line">The line of the block, which a problem of the expression parser cites.</param>
    /// <param name="file">The file of the block.</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static Value? ValueOf(string klartext, int line, string file, out string? problem)
    {
        Value? number = new SourceWord { Address = "", Text = klartext.Trim() }.ToNcxNumber();
        if (number is not null)
        {
            problem = null;
            return number;
        }

        string? text = ToText(klartext, out problem);
        return text is null ? null : Parse(text, line, file, out problem);
    }

    /// <summary>
    /// The NCX text of a Klartext formula, before the expression parser checks it.
    /// </summary>
    /// <param name="klartext">The formula as the source writes it, "Q2 + 3 * SIN Q3".</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static string? ToText(string klartext, out string? problem)
    {
        var expression = new HeidenhainExpression(klartext);
        expression.Tokenize();
        string text = expression._problem is null ? expression.Sequence(closes: false) : "";
        if (expression._problem is null && expression._position < expression._tokens.Count)
        {
            expression._problem = $"{expression.Current} of {klartext} stands where no operator or operand fits";
        }

        problem = expression._problem;
        return problem is null ? text : null;
    }

    /// <summary>
    /// An NCX expression value from its text, in the canonical form the parser gives it, so that the program formats to
    /// itself (language 4.12; D91).
    /// </summary>
    /// <param name="text">The NCX expression without its braces.</param>
    /// <param name="line">The line of the block.</param>
    /// <param name="file">The file of the block.</param>
    /// <param name="problem">Why the text is no expression of language 4.12; null when it is one.</param>
    public static ExprValue? Parse(string text, int line, string file, out string? problem)
    {
        ExprNode? tree = ExprParser.Parse(text, line, new Diagnostics(file));
        if (tree is null)
        {
            problem = $"{{{text}}} is no expression of language 4.12";
            return null;
        }

        problem = null;
        return new ExprValue(tree.ToCanonical(), tree);
    }

    // Operands and operators in turn, up to the closing parenthesis of a group or to the end.
    private string Sequence(bool closes)
    {
        var text = new StringBuilder();
        bool operand = true;
        while (_problem is null && _position < _tokens.Count && Current != ")")
        {
            if (operand)
            {
                text.Append(Operand());
                operand = false;
                continue;
            }

            string op = Current switch
            {
                "+" or "-" or "*" or "/" or "^" => " " + Current + " ",
                "%" => " MOD ",
                _ => "",
            };
            if (op.Length == 0)
            {
                _problem = $"{Current} of {_klartext} stands where an operator belongs";
                break;
            }

            text.Append(op);
            _position++;
            operand = true;
        }

        if (_problem is null && (operand || (closes && Current != ")")))
        {
            _problem = $"{_klartext} is not complete";
        }

        return text.ToString();
    }

    // An operand with its signs: a number, a Q parameter, a group in parentheses, or a function with its operand. A
    // plus in front is dropped, a minus is the unary minus of NCX (language 4.12).
    private string Operand()
    {
        bool negative = false;
        while (Current is "+" or "-")
        {
            negative ^= Current == "-";
            _position++;
        }

        string token = Current;
        _position++;
        string primary;
        if (token == "(")
        {
            primary = Group();
        }
        else if (Array.IndexOf(s_functions, token) >= 0)
        {
            primary = Function(token);
        }
        else if (IsParameter(token))
        {
            primary = "$" + token;
        }
        else if (HeidenhainNumbers.Parse(token) is not null && new SourceWord { Address = "", Text = token }
            .ToNcxNumber() is Value number)
        {
            primary = number.ToCanonical();
        }
        else
        {
            _problem ??= token.Length == 0
                ? $"{_klartext} is not complete"
                : $"{token} of {_klartext} has no NCX form (language 4.12)";
            primary = "";
        }

        return negative ? "-" + primary : primary;
    }

    // A group in parentheses, read after its opening parenthesis, with its parentheses.
    private string Group()
    {
        string inner = Sequence(closes: true);
        _position++;
        return "(" + inner + ")";
    }

    // SIN Q3 applies SIN to the operand after it, SIN (Q3 + 5) to the group; NEG x is -x, LOG x is LN(x) / LN(10).
    private string Function(string name)
    {
        string argument;
        if (Current == "(")
        {
            _position++;
            argument = Group();
        }
        else
        {
            argument = "(" + Operand() + ")";
        }

        return name switch
        {
            "NEG" => "(-" + argument + ")",
            "LOG" => "(LN" + argument + " / LN(10))",
            _ => name + argument,
        };
    }

    // Q, QL, QR and QS parameters with their number (controllers heidenhain.md 6).
    private static bool IsParameter(string token)
    {
        return HeidenhainQ.IsParameter(token);
    }

    // Numbers with the comma or the dot, names of letters and digits, and the operators and parentheses.
    private void Tokenize()
    {
        int position = 0;
        while (position < _klartext.Length && _problem is null)
        {
            char character = _klartext[position];
            int start = position;
            if (character is ' ' or '\t')
            {
                position++;
                continue;
            }

            if (char.IsAsciiDigit(character) || character is '.' or ',')
            {
                while (position < _klartext.Length && (char.IsAsciiDigit(_klartext[position])
                    || _klartext[position] is '.' or ','))
                {
                    position++;
                }
            }
            else if (char.IsAsciiLetter(character))
            {
                while (position < _klartext.Length && char.IsAsciiLetterOrDigit(_klartext[position]))
                {
                    position++;
                }
            }
            else if ("+-*/^%()".Contains(character, StringComparison.Ordinal))
            {
                position++;
            }
            else
            {
                _problem = $"the character {character} of {_klartext} has no NCX form";
                break;
            }

            _tokens.Add(_klartext.Substring(start, position - start).ToUpperInvariant());
        }
    }
}
