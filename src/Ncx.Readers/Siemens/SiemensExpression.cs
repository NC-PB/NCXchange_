using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// Translates a SINUMERIK expression into an NCX expression (controllers siemens.md 8, 11 rule 9; controller-mapping
/// 6; language 4.9, 4.12): R1 and R[1] to $R1, a DEF or GUD name to $NAME, a system variable through the
/// [system_variables] of the machine (D51), == and &lt;&gt; to == and !=, AND OR NOT MOD as they are, SIN COS TAN
/// ASIN ACOS ATAN2 SQRT ABS ROUND LN EXP to the NCX functions, TRUNC to INT, MINVAL and MAXVAL to MIN and MAX, POT to
/// the square. What NCX cannot express, an indirect R[R1], a string, XOR, the bit operators, the concatenation
/// &lt;&lt;,
/// an unmapped system variable, is a problem that keeps the block as RAW.
/// </summary>
internal static partial class SiemensExpression
{
    // The functions of controller-mapping 6 and their NCX names (language 4.12).
    private static readonly Dictionary<string, string> s_functions = new(StringComparer.Ordinal)
    {
        ["SIN"] = "SIN",
        ["COS"] = "COS",
        ["TAN"] = "TAN",
        ["ASIN"] = "ASIN",
        ["ACOS"] = "ACOS",
        ["ATAN2"] = "ATAN2",
        ["SQRT"] = "SQRT",
        ["ABS"] = "ABS",
        ["TRUNC"] = "INT",
        ["ROUND"] = "ROUND",
        ["LN"] = "LN",
        ["EXP"] = "EXP",
        ["MINVAL"] = "MIN",
        ["MAXVAL"] = "MAX",
    };

    /// <summary>
    /// The NCX value of a SINUMERIK value: a number as written, any other value an NCX expression; null, with the
    /// problem, where NCX cannot express it.
    /// </summary>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="text">The value as the source writes it, "R1*2", "-.5".</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static Value? ValueOf(SiemensBlock block, string text, out string? problem)
    {
        if (SiemensNumbers.Parse(text) is Value number)
        {
            problem = null;
            return number;
        }

        string? ncx = ToText(block, text, out problem);
        return ncx is null ? null : Parse(block, ncx, out problem);
    }

    /// <summary>
    /// An NCX expression value from its text, in the canonical form the parser gives it, so that the program formats to
    /// itself (language 4.12; D91).
    /// </summary>
    /// <param name="block">The block, whose line a problem cites.</param>
    /// <param name="text">The NCX expression without its braces.</param>
    /// <param name="problem">Why the text is no expression of language 4.12; null when it is one.</param>
    public static ExprValue? Parse(SiemensBlock block, string text, out string? problem)
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

    /// <summary>
    /// The NCX text of a SINUMERIK expression, before the expression parser checks it; null, with the problem, where
    /// NCX cannot express it.
    /// </summary>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="siemens">The expression as the source writes it.</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static string? ToText(SiemensBlock block, string siemens, out string? problem)
    {
        problem = null;
        string text = Translate(block, siemens, ref problem);
        return problem is null ? text.Trim() : null;
    }

    private static string Translate(SiemensBlock block, string text, ref string? problem)
    {
        var output = new StringBuilder();
        int position = 0;
        while (problem is null && position < text.Length)
        {
            char character = text[position];
            if (character is ' ' or '\t')
            {
                position++;
            }
            else if (char.IsAsciiDigit(character) || character == '.')
            {
                int end = SiemensScanner.ReadNumber(text, position);
                Value? number = end < 0 ? null : SiemensNumbers.Parse(text.Substring(position, end - position));
                if (number is null)
                {
                    problem = $"{text.Substring(position)} is no number";
                    break;
                }

                output.Append(number.ToCanonical());
                position = end;
            }
            else if (character == '(')
            {
                int end = SiemensScanner.ReadGroup(text, position);
                if (end < 0)
                {
                    problem = $"the parenthesis of {text} does not close";
                    break;
                }

                output.Append('(').Append(Translate(block, text.Substring(position + 1, end - position - 2),
                    ref problem)).Append(')');
                position = end;
            }
            else if (character == ',')
            {
                output.Append(", ");
                position++;
            }
            else if (SiemensScanner.StartsIdentifier(character))
            {
                position = Identifier(block, text, position, output, ref problem);
            }
            else
            {
                position = Operator(text, position, output, ref problem);
            }
        }

        return output.ToString();
    }

    // The comparisons and the arithmetic of controllers siemens.md 8; <> is != (language 4.12); << concatenates
    // strings, which NCX has no operator for (controller-mapping 6).
    private static int Operator(string text, int position, StringBuilder output, ref string? problem)
    {
        string two = position + 1 < text.Length ? text.Substring(position, 2) : "";
        string? ncx = two switch
        {
            "==" => "==",
            "<>" => "!=",
            "<=" => "<=",
            ">=" => ">=",
            _ => null,
        };
        if (ncx is not null)
        {
            output.Append(' ').Append(ncx).Append(' ');
            return position + 2;
        }

        char character = text[position];
        if (two == "<<" || character is not ('+' or '-' or '*' or '/' or '<' or '>'))
        {
            problem = two == "<<"
                ? "<< concatenates strings, which NCX has no operator for (controller-mapping 6)"
                : $"the character {character} of {text} has no NCX form";
            return position + 1;
        }

        output.Append(' ').Append(character).Append(' ');
        return position + 1;
    }

    // A name: an operator word, a function with its arguments, an R parameter, a system variable, or a variable.
    private static int Identifier(SiemensBlock block, string text, int position, StringBuilder output,
        ref string? problem)
    {
        int end = SiemensScanner.ReadIdentifier(text, position);
        string name = text.Substring(position, end - position).ToUpperInvariant();
        int next = SiemensScanner.SkipBlanks(text, end);
        switch (name)
        {
            case "AND" or "OR" or "NOT" or "MOD":
                output.Append(' ').Append(name).Append(' ');
                return end;

            // TODO(question): DIV divides whole numbers on the control (controllers siemens.md 8), and language 4.12
            // has no operator for it; a block with DIV stays RAW.
            case "DIV" or "XOR" or "B_AND" or "B_OR" or "B_NOT" or "B_XOR" or "TRUE" or "FALSE":
                problem = $"{name} has no NCX form (language 4.12; controller-mapping 6)";
                return end;
        }

        if (next < text.Length && text[next] == '(')
        {
            return Function(block, text, name, next, output, ref problem);
        }

        string index = "";
        if (end < text.Length && text[end] == '[')
        {
            int indexEnd = SiemensScanner.ReadGroup(text, end);
            if (indexEnd < 0)
            {
                problem = $"the bracket of {text} does not close";
                return text.Length;
            }

            index = text.Substring(end, indexEnd - end).ToUpperInvariant().Replace(" ", "");
            end = indexEnd;
        }

        output.Append(Variable(block, name, index, ref problem));
        return end;
    }

    // SIN(30) is SIN(30), TRUNC is INT, POT(x) the square of x (controller-mapping 6, functions).
    private static int Function(SiemensBlock block, string text, string name, int open, StringBuilder output,
        ref string? problem)
    {
        int end = SiemensScanner.ReadGroup(text, open);
        if (end < 0)
        {
            problem = $"the parenthesis of {name} does not close";
            return text.Length;
        }

        string arguments = Translate(block, text.Substring(open + 1, end - open - 2), ref problem);
        if (name == "POT")
        {
            output.Append("((").Append(arguments).Append(") ^ 2)");
        }
        else if (s_functions.TryGetValue(name, out string? ncx))
        {
            // TODO(question): D119, the argument order of ATAN2; the arguments stay in the order the source writes
            // them.
            output.Append(ncx).Append('(').Append(arguments).Append(')');
        }
        else
        {
            problem = $"{name}() has no NCX function (language 4.12; controller-mapping 6)";
        }

        return end;
    }

    // R1 and R[1] are $R1; an indirect R[R1] is RAW (controller-mapping 6, VAR); a system variable is the SYS_ name
    // the machine maps it to, and unmapped it is RAW (language 4.12, D51); a DEF or GUD name keeps its name (language
    // 4.9).
    private static string Variable(SiemensBlock block, string name, string index, ref string? problem)
    {
        if (name.StartsWith('$'))
        {
            string native = name + index;
            foreach (KeyValuePair<string, string> entry in block.Machine.SystemVariables.Entries)
            {
                if (string.Equals(entry.Value.Replace(" ", ""), native, StringComparison.OrdinalIgnoreCase))
                {
                    return "$" + entry.Key;
                }
            }

            problem = $"the system variable {native} is none the machine maps to a SYS_ name (language 4.12, D51)";
            return "";
        }

        if (name == "R" && index.Length > 2
            && SiemensNumbers.WholeNumber(index.Substring(1, index.Length - 2)) is int register && register >= 0)
        {
            return "$R" + register.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (index.Length > 0 || !VariableName().IsMatch(name))
        {
            problem = $"{name}{index} is no variable NCX can name (controller-mapping 6, VAR)";
            return "";
        }

        return "$" + name;
    }

    // The name of an NCX variable follows ADDR (language 3, 4.9).
    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableName();
}
