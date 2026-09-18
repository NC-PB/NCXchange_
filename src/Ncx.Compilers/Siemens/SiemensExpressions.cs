using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// NCX expressions and variables in the SINUMERIK language (controllers siemens.md 8; controller-mapping 6, VAR and
/// functions; language 4.9, 4.12): $R1 as R1, a declared name as it is, a name of another controller through
/// [variables] map, a SYS_ name through [system_variables]; != as &lt;&gt;, INT as TRUNC, MIN and MAX as MINVAL and
/// MAXVAL, the square as POT; the comparisons, which bind weaker than AND and OR on the control, in parentheses.
/// </summary>
internal static partial class SiemensExpressions
{
    // How strongly an operator binds on the control, the comparisons weakest (the priority list of the SINUMERIK
    // operators: NOT, then * / DIV MOD, + -, AND, XOR, OR, and the comparisons last).
    private const int ComparisonBinding = 1;
    private const int OrBinding = 2;
    private const int AndBinding = 3;
    private const int SumBinding = 4;
    private const int ProductBinding = 5;
    private const int UnaryBinding = 6;
    private const int AtomBinding = 7;

    // The prefix of the system variables of language 4.12, which [system_variables] maps (D51).
    private const string SystemPrefix = "SYS_";

    // The functions of language 4.12 whose SINUMERIK name is another (controller-mapping 6, functions).
    private static readonly Dictionary<ExprFunction, string> s_functions = new()
    {
        [ExprFunction.Sin] = "SIN",
        [ExprFunction.Cos] = "COS",
        [ExprFunction.Tan] = "TAN",
        [ExprFunction.Asin] = "ASIN",
        [ExprFunction.Acos] = "ACOS",
        [ExprFunction.Atan2] = "ATAN2",
        [ExprFunction.Sqrt] = "SQRT",
        [ExprFunction.Abs] = "ABS",
        [ExprFunction.Int] = "TRUNC",
        [ExprFunction.Round] = "ROUND",
        [ExprFunction.Ln] = "LN",
        [ExprFunction.Exp] = "EXP",
        [ExprFunction.Min] = "MINVAL",
        [ExprFunction.Max] = "MAXVAL",
    };

    /// <summary>
    /// The SINUMERIK text of an NCX expression; null, with an ERROR on the block, where it holds what the control
    /// cannot write.
    /// </summary>
    public static string? Text(SiemensBlock write, ExprValue expression)
    {
        if (expression.Tree is not ExprNode tree)
        {
            write.Error(DiagnosticCodes.SiemensExpressionNotWritable,
                $"{expression.ToCanonical()} is no expression of language 4.12, so it cannot be written.");
            return null;
        }

        // A problem of a variable is reported where the variable is written, as an empty entry here.
        var problems = new List<string>();
        string text = Write(write, tree, problems);
        var named = new List<string>();
        foreach (string problem in problems)
        {
            if (problem.Length > 0)
            {
                named.Add(problem);
            }
        }

        if (named.Count > 0)
        {
            write.Error(DiagnosticCodes.SiemensExpressionNotWritable,
                $"{expression.ToCanonical()} holds {string.Join(" and ", named)}, which the SINUMERIK language has no "
                + "form for (controllers siemens.md 8; language 4.12).");
        }

        return problems.Count > 0 ? null : text;
    }

    /// <summary>
    /// The text of a number or an expression as the value of an address: the number formatted for the address, an
    /// expression in its SINUMERIK text; null for another value, or an expression the control cannot write.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="value">The value of the word.</param>
    /// <param name="address">The address whose decimals apply to a number.</param>
    public static string? ValueText(SiemensBlock write, Value value, string address)
    {
        return value switch
        {
            IntegerValue integer => write.Format(address, integer.Number),
            DecimalValue number => write.Format(address, number.Number),
            ExprValue expression => Text(write, expression),
            _ => null,
        };
    }

    /// <summary>
    /// The SINUMERIK name of an NCX variable (controllers siemens.md 8; language 4.9; D173): an entry of [variables]
    /// map that covers its prefix writes it, "R" for Q turning Q1 into R1; an R parameter and a declared name stay as
    /// they are; a name of a letter and digits other than R is no name the control declares and is an ERROR.
    /// </summary>
    // TODO(question): D173: machine-config 7 shows [variables] map for Fanuc only, as a prefix plus the number; an
    // entry with {index} is rendered as a template of the number, as D173 recommends, an entry without it takes the
    // number behind its value, and a name no entry covers is written as it is where the control can declare it, until
    // D173 is answered.
    // TODO(question): controller-mapping 6 keeps the type of a DEF for the Siemens compiler and no NCX word carries it
    // (the reader's question on DEF); a declared name is written as it is, without its DEF, until that is answered.
    public static string? VariableName(SiemensBlock write, string name)
    {
        if (MappedName(write, name) is string mapped)
        {
            return mapped;
        }

        if (RParameter().IsMatch(name) || !LetterWithNumber().IsMatch(name))
        {
            return name;
        }

        write.Error(DiagnosticCodes.SiemensVariableNotMapped,
            $"The variable {name} is no name a SINUMERIK program declares, and no entry of [variables] map of the "
            + "machine covers it (machine-config 7; controllers siemens.md 8; D173).");
        return null;
    }

    // The expression node by node, parentheses where the control binds differently from language 4.12.
    private static string Write(SiemensBlock write, ExprNode node, List<string> problems)
    {
        switch (node)
        {
            case NumberNode number:
                return number.Text;
            case ParenthesesNode parentheses:
                return "(" + Write(write, parentheses.Inner, problems) + ")";
            case VariableNode variable:
                return Variable(write, variable, problems);
            case UnaryNode unary:
                string operand = Operand(write, unary.Operand, UnaryBinding, problems);
                return unary.Operator == UnaryOperator.Minus ? "-" + operand : "NOT " + operand;
            case CallNode call:
                return Call(write, call, problems);
            case BinaryNode binary:
                return Binary(write, binary, problems);
            default:
                problems.Add(node.ToCanonical());
                return node.ToCanonical();
        }
    }

    private static string Binary(SiemensBlock write, BinaryNode binary, List<string> problems)
    {
        // The square is POT, the only power the control writes (controller-mapping 6, functions).
        if (binary.Operator == BinaryOperator.Power)
        {
            if (binary.Right is NumberNode { Text: "2" })
            {
                return "POT(" + Write(write, binary.Left, problems) + ")";
            }

            problems.Add("the power " + binary.ToCanonical() + " other than the square");
            return binary.ToCanonical();
        }

        int binding = BindingOf(binary.Operator);
        string left = Operand(write, binary.Left, binding, problems);
        string right = Operand(write, binary.Right, binding + 1, problems);
        return left + SymbolOf(binary.Operator) + right;
    }

    // An operand in parentheses where the control would bind it to its neighbour otherwise.
    private static string Operand(SiemensBlock write, ExprNode operand, int binding, List<string> problems)
    {
        string text = Write(write, operand, problems);
        return BindingOf(operand) < binding ? "(" + text + ")" : text;
    }

    private static string Call(SiemensBlock write, CallNode call, List<string> problems)
    {
        var arguments = new List<string>();
        foreach (ExprNode argument in call.Arguments)
        {
            arguments.Add(Write(write, argument, problems));
        }

        switch (call.Function)
        {
            // ATAN of one value is ATAN2 with the abscissa 1, FRAC the value less its TRUNC (language 4.12).
            case ExprFunction.Atan:
                return "ATAN2(" + arguments[0] + ",1)";
            case ExprFunction.Frac:
                return "(" + arguments[0] + "-TRUNC(" + arguments[0] + "))";
            case ExprFunction.Sgn:
                problems.Add("SGN");
                return call.ToCanonical();
            case ExprFunction.Min or ExprFunction.Max when arguments.Count > 2:
                // MINVAL and MAXVAL take two values; more are nested.
                string nested = arguments[^1];
                for (int index = arguments.Count - 2; index >= 0; index--)
                {
                    nested = s_functions[call.Function] + "(" + arguments[index] + "," + nested + ")";
                }

                return nested;
            default:
                return s_functions[call.Function] + "(" + string.Join(",", arguments) + ")";
        }
    }

    // $R1 is R1, $SYS_POS_X the native variable of [system_variables] with the index of the register (language 4.12,
    // D51).
    private static string Variable(SiemensBlock write, VariableNode variable, List<string> problems)
    {
        if (!variable.Name.StartsWith(SystemPrefix, StringComparison.Ordinal))
        {
            string? name = VariableName(write, variable.Name);
            if (name is null)
            {
                problems.Add("");
            }

            return name ?? variable.Name;
        }

        if (write.Machine.SystemVariables.Find(variable.Name) is not string template)
        {
            write.Error(DiagnosticCodes.SiemensSystemVariableNotMapped,
                $"${variable.Name} has no entry in [system_variables] of the machine (language 4.12; machine-config 7;"
                + " D51).");
            problems.Add("");
            return variable.Name;
        }

        var values = new TemplateValues();
        if (variable.Index is NumberNode index
            && decimal.TryParse(index.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal register))
        {
            values.Set("index", register);
        }

        return write.TemplateOf(template).Render(values, write.Block, write.Diagnostics) ?? variable.Name;
    }

    // An entry of [variables] map whose key is the prefix of the name, the longest first: QL before Q.
    private static string? MappedName(SiemensBlock write, string name)
    {
        string? prefix = null;
        foreach (string key in write.Machine.Variables.Map.Keys)
        {
            bool covers = name.Length > key.Length && name.StartsWith(key, StringComparison.Ordinal)
                && IsDigits(name.Substring(key.Length));
            if (covers && (prefix is null || key.Length > prefix.Length))
            {
                prefix = key;
            }
        }

        if (prefix is null)
        {
            return null;
        }

        string number = name.Substring(prefix.Length);
        string mapping = write.Machine.Variables.Map[prefix];
        if (!mapping.Contains("{index", StringComparison.Ordinal))
        {
            return mapping + number;
        }

        var values = new TemplateValues();
        values.Set("index", decimal.Parse(number, CultureInfo.InvariantCulture));
        return write.TemplateOf(mapping).Render(values, write.Block, write.Diagnostics);
    }

    private static int BindingOf(ExprNode node)
    {
        return node switch
        {
            BinaryNode { Operator: BinaryOperator.Power } => AtomBinding,
            BinaryNode binary => BindingOf(binary.Operator),
            UnaryNode => UnaryBinding,
            _ => AtomBinding,
        };
    }

    private static int BindingOf(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Or => OrBinding,
            BinaryOperator.And => AndBinding,
            BinaryOperator.Add or BinaryOperator.Subtract => SumBinding,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Mod => ProductBinding,
            BinaryOperator.Power => AtomBinding,
            _ => ComparisonBinding,
        };
    }

    private static string SymbolOf(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Or => " OR ",
            BinaryOperator.And => " AND ",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "<>",
            BinaryOperator.Less => "<",
            BinaryOperator.LessOrEqual => "<=",
            BinaryOperator.Greater => ">",
            BinaryOperator.GreaterOrEqual => ">=",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Mod => " MOD ",
            _ => "^",
        };
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    // R1, R99: the arithmetic parameters (controllers siemens.md 8).
    [GeneratedRegex("^R[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RParameter();

    // A letter and digits, the form of an address: Q1, V105 (controllers siemens.md 1).
    [GeneratedRegex("^[A-Z][0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LetterWithNumber();
}
