using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// NCX values and expressions in custom macro B (controllers fanuc.md 7; controller-mapping 6; language 4.9, 4.12):
/// ( ) as [ ], $Vn as #n (D33), a Heidenhain name through [variables] map, a SYS_ name through [system_variables]
/// (D51), the comparisons as EQ NE LT LE GT GE, INT as FIX, ATAN2(a, b) as ATAN[a]/[b]; what custom macro B cannot
/// write is an ERROR.
/// </summary>
internal static class FanucExpressions
{
    /// <summary>
    /// The value of an address word: a number as [format] writes it, a variable #101, or an expression in brackets
    /// [#101 + 20] (controllers fanuc.md 7); null, with an ERROR, for a value custom macro B cannot write.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="address">The address whose decimals apply.</param>
    /// <param name="value">The NCX value.</param>
    /// <param name="factor">The factor the machine writes the value with: 2 or 0.5 between radius and diameter (D60),
    /// 1000 for a dwell in milliseconds (D159).</param>
    public static string? WordValue(FanucBlock write, string address, Value value, decimal factor)
    {
        switch (value)
        {
            case IntegerValue integer:
                return factor == 1m ? write.Format(address, integer.Number) : write.FormatComputed(address,
                    integer.Number * factor);
            case DecimalValue number:
                return factor == 1m ? write.Format(address, number.Number) : write.FormatComputed(address,
                    number.Number * factor);
            case ExprValue { Tree: ExprNode tree }:
                string? text = Expression(write, tree);
                if (text is null)
                {
                    return null;
                }

                // The factor stands as a constant of the expression: * 2 or / 2 between radius and diameter (D60),
                // * 1000 from the seconds of DWELL and CYCLE_DWELL to the milliseconds of P (language 4.1, 4.7;
                // controllers fanuc.md 4, 6; D159); an operation is its operand in brackets (controllers fanuc.md 7).
                if (factor != 1m)
                {
                    string operand = tree is BinaryNode ? "[" + text + "]" : text;
                    return "[" + operand + (factor < 1m ? " / " + Constant(1m / factor) : " * " + Constant(factor))
                        + "]";
                }

                bool variable = tree is VariableNode
                    || tree is UnaryNode { Operator: UnaryOperator.Minus, Operand: VariableNode };
                return variable
                    ? text
                    : "[" + text + "]";
            default:
                write.Error(DiagnosticCodes.FanucExpressionNotWritable,
                    $"{value.ToCanonical()} is no number or expression that custom macro B writes (controllers "
                    + "fanuc.md 7).");
                return null;
        }
    }

    /// <summary>
    /// An expression in the syntax of custom macro B, without brackets around it; null, with an ERROR, where it has
    /// no Fanuc form.
    /// </summary>
    public static string? Expression(FanucBlock write, ExprNode node)
    {
        switch (node)
        {
            case NumberNode number:
                return number.Text;
            case VariableNode variable:
                return Variable(write, variable);
            case ParenthesesNode parentheses:
                return Expression(write, parentheses.Inner) is string inner ? "[" + inner + "]" : null;
            case UnaryNode { Operator: UnaryOperator.Minus } minus:
                return Operand(write, minus.Operand) is string operand ? "-" + operand : null;
            case UnaryNode not:
                return Expression(write, Negated(not.Operand));
            case BinaryNode binary:
                return Binary(write, binary);
            case CallNode call:
                return Call(write, call);
            default:
                return NotWritable(write, node.ToCanonical());
        }
    }

    /// <summary>
    /// A condition of IF or WHILE in brackets, [#103 LT #102] (controllers fanuc.md 7); a value that is no comparison
    /// is compared with 0, as IF executes when the expression is not 0 (language 4.9).
    /// </summary>
    public static string? Condition(FanucBlock write, ExprNode node)
    {
        ExprNode condition = IsCondition(node)
            ? node
            : new BinaryNode(BinaryOperator.NotEqual, node, new NumberNode("0"));
        string? text = Expression(write, condition);
        return text is null ? null : "[" + text + "]";
    }

    /// <summary>
    /// The Fanuc variable of an NCX variable name: Vn is #n (D33, language 4.9); a SYS_ name the native variable of
    /// [system_variables] (D51); Q1, QR1, QL1 the prefix of [variables] map with the number (machine-config 7).
    /// </summary>
    public static string? Variable(FanucBlock write, string name, ExprNode? index)
    {
        MachineConfig machine = write.Machine;
        if (name.Length > 1 && name[0] == 'V' && IsDigits(name.Substring(1)) && index is null)
        {
            return "#" + name.Substring(1).TrimStart('0');
        }

        if (name.StartsWith("SYS_", StringComparison.Ordinal))
        {
            return SystemVariable(write, name, index);
        }

        // TODO(question): D173: machine-config 7 maps Q1 to #101 with map Q = "#1", which holds only with the number
        // padded to two digits, and says so nowhere; the prefix is written with the number padded to two digits, as
        // Q1 -> #101 and QR1 -> #501 read, until D173 is answered.
        int digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1]))
        {
            digits--;
        }

        string prefix = name.Substring(0, digits);
        string number = name.Substring(digits);
        if (index is null && number.Length > 0 && machine.Variables.Map.TryGetValue(prefix, out string? native))
        {
            return native + number.PadLeft(2, '0');
        }

        write.Error(DiagnosticCodes.FanucVariableNotMapped,
            $"${name} has no Fanuc variable: it is no V name of custom macro B, no SYS_ name of [system_variables] "
            + "and no name of [variables] map (language 4.9, machine-config 7, D51, D173).");
        return null;
    }

    private static string? Variable(FanucBlock write, VariableNode variable)
    {
        return Variable(write, variable.Name, variable.Index);
    }

    // A SYS_ name through its native template, {index} for the index (machine-config 7, D51).
    private static string? SystemVariable(FanucBlock write, string name, ExprNode? index)
    {
        string? template = write.Machine.SystemVariables.Find(name);
        if (string.IsNullOrEmpty(template))
        {
            write.Error(DiagnosticCodes.FanucVariableNotMapped,
                $"${name} has no native variable in [system_variables] of the machine (language 4.12, D51).");
            return null;
        }

        var values = new TemplateValues();
        if (index is NumberNode number
            && decimal.TryParse(number.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal register))
        {
            values.Set("index", register);
        }
        else if (index is not null)
        {
            return NotWritable(write, "the index " + index.ToCanonical());
        }

        return write.Render(template, $"[system_variables] {name} (machine-config 7)", values);
    }

    // + - * / MOD, the comparisons, AND and OR; every operand that is itself an operation stands in brackets, since
    // custom macro B ranks AND with * and OR with + (controllers fanuc.md 7).
    private static string? Binary(FanucBlock write, BinaryNode binary)
    {
        string? symbol = binary.Operator switch
        {
            BinaryOperator.Or => "OR",
            BinaryOperator.And => "AND",
            BinaryOperator.Equal => "EQ",
            BinaryOperator.NotEqual => "NE",
            BinaryOperator.Less => "LT",
            BinaryOperator.LessOrEqual => "LE",
            BinaryOperator.Greater => "GT",
            BinaryOperator.GreaterOrEqual => "GE",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Mod => "MOD",
            _ => null,
        };
        if (symbol is null)
        {
            return NotWritable(write, binary.ToCanonical());
        }

        string? left = Operand(write, binary.Left);
        string? right = Operand(write, binary.Right);
        return left is null || right is null ? null : left + " " + symbol + " " + right;
    }

    private static string? Operand(FanucBlock write, ExprNode node)
    {
        string? text = Expression(write, node);
        return text is not null && node is BinaryNode ? "[" + text + "]" : text;
    }

    // The functions of custom macro B take their argument in brackets; INT truncates toward zero as FIX does, and
    // ATAN2(a, b) is ATAN[a]/[b] as the reader reads it (controllers fanuc.md 7, language 4.12).
    // TODO(question): D119: the argument order and the range of ATAN2 are open; ATAN2(a, b) is written as
    // ATAN[a]/[b], the form the reader reads into it, until D119 is answered.
    private static string? Call(FanucBlock write, CallNode call)
    {
        string? name = call.Function switch
        {
            ExprFunction.Sin => "SIN",
            ExprFunction.Cos => "COS",
            ExprFunction.Tan => "TAN",
            ExprFunction.Asin => "ASIN",
            ExprFunction.Acos => "ACOS",
            ExprFunction.Atan => "ATAN",
            ExprFunction.Sqrt => "SQRT",
            ExprFunction.Abs => "ABS",
            ExprFunction.Int => "FIX",
            ExprFunction.Round => "ROUND",
            ExprFunction.Ln => "LN",
            ExprFunction.Exp => "EXP",
            _ => null,
        };
        if (call.Function == ExprFunction.Atan2 && call.Arguments.Count == 2)
        {
            string? first = Expression(write, call.Arguments[0]);
            string? second = Expression(write, call.Arguments[1]);
            return first is null || second is null ? null : "ATAN[" + first + "]/[" + second + "]";
        }

        if (name is null || call.Arguments.Count != 1)
        {
            return NotWritable(write, call.ToCanonical());
        }

        return Expression(write, call.Arguments[0]) is string argument ? name + "[" + argument + "]" : null;
    }

    // NOT has no operator in custom macro B: a comparison is inverted, AND and OR by De Morgan, anything else compared
    // with 0 (language 4.12, NOT; controllers fanuc.md 7).
    private static ExprNode Negated(ExprNode node)
    {
        switch (node)
        {
            case ParenthesesNode parentheses:
                return Negated(parentheses.Inner);
            case UnaryNode { Operator: UnaryOperator.Not } not:
                return not.Operand;
            case BinaryNode { Operator: BinaryOperator.And } and:
                return new BinaryNode(BinaryOperator.Or, Negated(and.Left), Negated(and.Right));
            case BinaryNode { Operator: BinaryOperator.Or } or:
                return new BinaryNode(BinaryOperator.And, Negated(or.Left), Negated(or.Right));
            case BinaryNode comparison when Inverse(comparison.Operator) is BinaryOperator inverse:
                return comparison with { Operator = inverse };
            default:
                return new BinaryNode(BinaryOperator.Equal, node, new NumberNode("0"));
        }
    }

    private static BinaryOperator? Inverse(BinaryOperator comparison)
    {
        return comparison switch
        {
            BinaryOperator.Equal => BinaryOperator.NotEqual,
            BinaryOperator.NotEqual => BinaryOperator.Equal,
            BinaryOperator.Less => BinaryOperator.GreaterOrEqual,
            BinaryOperator.LessOrEqual => BinaryOperator.Greater,
            BinaryOperator.Greater => BinaryOperator.LessOrEqual,
            BinaryOperator.GreaterOrEqual => BinaryOperator.Less,
            _ => null,
        };
    }

    // A comparison, AND, OR or NOT of them is a condition as it stands.
    private static bool IsCondition(ExprNode node)
    {
        return node switch
        {
            ParenthesesNode parentheses => IsCondition(parentheses.Inner),
            UnaryNode { Operator: UnaryOperator.Not } => true,
            BinaryNode { Operator: BinaryOperator.And or BinaryOperator.Or } => true,
            BinaryNode binary => Inverse(binary.Operator) is not null,
            _ => false,
        };
    }

    private static string? NotWritable(FanucBlock write, string what)
    {
        write.Error(DiagnosticCodes.FanucExpressionNotWritable,
            $"{what} has no form in custom macro B, which knows + - * / MOD, EQ NE LT LE GT GE, AND OR and SIN COS TAN "
            + "ASIN ACOS ATAN SQRT ABS ROUND FIX LN EXP (controllers fanuc.md 7).");
        return null;
    }

    // A constant of an expression as written in custom macro B: invariant, without trailing zeros, 2 and 1000.
    private static string Constant(decimal value)
    {
        return value.ToString("0.############################", CultureInfo.InvariantCulture);
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
}
