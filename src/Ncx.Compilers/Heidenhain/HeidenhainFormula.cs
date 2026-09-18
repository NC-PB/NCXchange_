using Ncx.Core.Expressions;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// NCX expressions as Klartext formulas (controllers heidenhain.md 6; controller-mapping 6, VAR and functions;
/// language 4.12): the Q parameters Q, QL and QR by their names, + - * / ^ and the parentheses as the expression
/// writes them, MOD as %, and the functions SIN COS TAN ASIN ACOS ATAN SQRT ABS INT FRAC SGN LN EXP before their
/// operand, Q2 + 3 * SIN Q3. What Klartext has no formula for is refused with the reason: ATAN2, ROUND, MIN and MAX,
/// the comparisons, AND, OR and NOT, an index, a string parameter QS and every other variable name.
/// </summary>
internal sealed class HeidenhainFormula
{
    // The functions of the formula syntax (controllers heidenhain.md 6) by the NCX function of language 4.12.
    private static readonly Dictionary<ExprFunction, string> s_functions = new()
    {
        [ExprFunction.Sin] = "SIN",
        [ExprFunction.Cos] = "COS",
        [ExprFunction.Tan] = "TAN",
        [ExprFunction.Asin] = "ASIN",
        [ExprFunction.Acos] = "ACOS",
        [ExprFunction.Atan] = "ATAN",
        [ExprFunction.Sqrt] = "SQRT",
        [ExprFunction.Abs] = "ABS",
        [ExprFunction.Int] = "INT",
        [ExprFunction.Frac] = "FRAC",
        [ExprFunction.Sgn] = "SGN",
        [ExprFunction.Ln] = "LN",
        [ExprFunction.Exp] = "EXP",
    };

    // The operators of the formula syntax (controllers heidenhain.md 6): + - * / as in NCX, ^ the power, % the modulo.
    private static readonly Dictionary<BinaryOperator, string> s_operators = new()
    {
        [BinaryOperator.Add] = "+",
        [BinaryOperator.Subtract] = "-",
        [BinaryOperator.Multiply] = "*",
        [BinaryOperator.Divide] = "/",
        [BinaryOperator.Mod] = "%",
        [BinaryOperator.Power] = "^",
    };

    // The numeric parameters: Q free and reserved, QL local, QR remanent; QS holds strings (controllers heidenhain.md
    // 6), which no formula of numbers takes.
    private static readonly string[] s_parameterPrefixes = ["QL", "QR", "Q"];

    private readonly string _separator;
    private string? _problem;

    private HeidenhainFormula(string separator)
    {
        _separator = separator;
    }

    /// <summary>
    /// Tells whether a variable name is a numeric Q parameter of Klartext: Q5, QL5, QR5 (controllers heidenhain.md 6).
    /// </summary>
    public static bool IsParameter(string name)
    {
        foreach (string prefix in s_parameterPrefixes)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal)
                && IsDigits(name.Substring(prefix.Length)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The Klartext formula of an expression; null with the reason when Klartext has none.
    /// </summary>
    /// <param name="node">The expression.</param>
    /// <param name="separator">The decimal separator of the machine, the comma of Klartext files.</param>
    /// <param name="problem">Why Klartext has no formula for it; null when it has.</param>
    public static string? Of(ExprNode node, string separator, out string? problem)
    {
        var formula = new HeidenhainFormula(separator);
        string text = formula.Write(node);
        problem = formula._problem;
        return problem is null ? text : null;
    }

    /// <summary>
    /// A Q parameter with its sign where a number stands, +Q1 of $Q1 and -Q1 of -$Q1 (controllers heidenhain.md 6);
    /// null for any other expression.
    /// </summary>
    public static string? SignedParameter(ExprNode node)
    {
        ExprNode inner = Unwrapped(node);
        if (inner is VariableNode { Index: null } variable && IsParameter(variable.Name))
        {
            return "+" + variable.Name;
        }

        if (inner is UnaryNode { Operator: UnaryOperator.Minus } negation
            && Unwrapped(negation.Operand) is VariableNode { Index: null } negated && IsParameter(negated.Name))
        {
            return "-" + negated.Name;
        }

        return null;
    }

    /// <summary>
    /// An operand of FN 9 to FN 12, a number or a Q parameter with its sign, +Q1, +10, -2,5 (controllers heidenhain.md
    /// 6); null for any other expression.
    /// </summary>
    public static string? Operand(ExprNode node, string separator)
    {
        ExprNode inner = Unwrapped(node);
        if (inner is NumberNode number)
        {
            return "+" + Digits(number.Text, separator);
        }

        if (inner is UnaryNode { Operator: UnaryOperator.Minus } negation
            && Unwrapped(negation.Operand) is NumberNode negative)
        {
            return "-" + Digits(negative.Text, separator);
        }

        return SignedParameter(inner);
    }

    /// <summary>
    /// The expression inside any parentheses around it.
    /// </summary>
    public static ExprNode Unwrapped(ExprNode node)
    {
        ExprNode inner = node;
        while (inner is ParenthesesNode group)
        {
            inner = group.Inner;
        }

        return inner;
    }

    // A number as the expression writes it, never rounded (language 2 rule 5), with the decimal separator of the
    // machine.
    private static string Digits(string text, string separator)
    {
        return text.Replace(".", separator, StringComparison.Ordinal);
    }

    private static bool IsDigits(string text)
    {
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return text.Length > 0;
    }

    private string Write(ExprNode node)
    {
        switch (node)
        {
            case NumberNode number:
                return Digits(number.Text, _separator);
            case VariableNode variable:
                return Variable(variable);
            case ParenthesesNode group:
                return "(" + Write(group.Inner) + ")";
            case UnaryNode unary:
                return Unary(unary);
            case BinaryNode binary:
                return Binary(binary);
            case CallNode call:
                return Call(call);
            default:
                return Refuse($"{node.ToCanonical()} has no Klartext formula");
        }
    }

    // A variable is a Q parameter by its name; an index, a string parameter and every other name have no Klartext
    // form (controllers heidenhain.md 6; language 4.9: the compiler maps names back through the machine configuration,
    // and a Heidenhain machine names none).
    private string Variable(VariableNode variable)
    {
        if (variable.Index is not null)
        {
            return Refuse($"{variable.ToCanonical()}: a Q parameter of Klartext takes no index");
        }

        return IsParameter(variable.Name)
            ? variable.Name
            : Refuse($"${variable.Name} is no numeric Q parameter of Klartext, Q, QL or QR");
    }

    // The minus in front of its operand; in front of a power in parentheses, which keeps the meaning of NCX, where
    // the power binds before the minus (language 4.12). NOT has no Klartext formula.
    private string Unary(UnaryNode unary)
    {
        if (unary.Operator != UnaryOperator.Minus)
        {
            return Refuse("NOT has no Klartext formula (controllers heidenhain.md 6)");
        }

        string operand = Write(unary.Operand);
        return unary.Operand is BinaryNode ? "-(" + operand + ")" : "-" + operand;
    }

    private string Binary(BinaryNode binary)
    {
        if (!s_operators.TryGetValue(binary.Operator, out string? symbol))
        {
            return Refuse($"{binary.ToCanonical()}: the comparisons, AND and OR have no Klartext formula; FN 9 to FN "
                + "12 compare two values of a jump (controllers heidenhain.md 6)");
        }

        return Write(binary.Left) + " " + symbol + " " + Write(binary.Right);
    }

    // SIN Q3 applies the function to the operand after it, SIN (Q3 + 5) to a group (controllers heidenhain.md 6).
    private string Call(CallNode call)
    {
        if (!s_functions.TryGetValue(call.Function, out string? name) || call.Arguments.Count != 1)
        {
            return Refuse($"{call.ToCanonical()}: the function has no Klartext form (controllers heidenhain.md 6)");
        }

        ExprNode argument = call.Arguments[0];
        string operand = Write(argument);
        return argument is NumberNode or VariableNode ? name + " " + operand : name + " (" + operand + ")";
    }

    private string Refuse(string problem)
    {
        _problem ??= problem;
        return "";
    }
}
