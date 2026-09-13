using System.Text;

namespace Ncx.Core.Expressions;

/// <summary>
/// How the operators and the functions of language 4.12 are spelled, in one place for the parser that reads them and
/// the nodes that write them.
/// </summary>
internal static class ExprSymbols
{
    /// <summary>
    /// The spelling of an operator with two operands (language 4.12, orexpr to power).
    /// </summary>
    public static string Of(BinaryOperator binaryOperator)
    {
        return binaryOperator switch
        {
            BinaryOperator.Or => "OR",
            BinaryOperator.And => "AND",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.Less => "<",
            BinaryOperator.LessOrEqual => "<=",
            BinaryOperator.Greater => ">",
            BinaryOperator.GreaterOrEqual => ">=",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Mod => "MOD",
            BinaryOperator.Power => "^",
            _ => throw new ArgumentOutOfRangeException(
                nameof(binaryOperator), binaryOperator, "Not an operator of language 4.12."),
        };
    }

    /// <summary>
    /// The spelling of an operator with one operand (language 4.12, notexpr and unary).
    /// </summary>
    public static string Of(UnaryOperator unaryOperator)
    {
        return unaryOperator switch
        {
            UnaryOperator.Minus => "-",
            UnaryOperator.Not => "NOT",
            _ => throw new ArgumentOutOfRangeException(
                nameof(unaryOperator), unaryOperator, "Not an operator of language 4.12."),
        };
    }

    /// <summary>
    /// The name of a function as the grammar writes it (language 4.12, function).
    /// </summary>
    public static string Of(ExprFunction function)
    {
        return function switch
        {
            ExprFunction.Sin => "SIN",
            ExprFunction.Cos => "COS",
            ExprFunction.Tan => "TAN",
            ExprFunction.Asin => "ASIN",
            ExprFunction.Acos => "ACOS",
            ExprFunction.Atan => "ATAN",
            ExprFunction.Atan2 => "ATAN2",
            ExprFunction.Sqrt => "SQRT",
            ExprFunction.Abs => "ABS",
            ExprFunction.Int => "INT",
            ExprFunction.Frac => "FRAC",
            ExprFunction.Round => "ROUND",
            ExprFunction.Sgn => "SGN",
            ExprFunction.Ln => "LN",
            ExprFunction.Exp => "EXP",
            ExprFunction.Min => "MIN",
            ExprFunction.Max => "MAX",
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, "Not a function of language 4.12."),
        };
    }

    /// <summary>
    /// The function a name calls, the name in uppercase; null for a name that is none of the seventeen of the grammar
    /// (language 4.12, function).
    /// </summary>
    public static ExprFunction? FunctionNamed(string name)
    {
        foreach (ExprFunction function in Enum.GetValues<ExprFunction>())
        {
            if (Of(function) == name)
            {
                return function;
            }
        }

        return null;
    }

    /// <summary>
    /// True for the words of the grammar that are operators, AND, OR, NOT and MOD: such a name is never a function
    /// (language 4.12).
    /// </summary>
    public static bool IsOperatorWord(string name)
    {
        return name == Of(BinaryOperator.Or)
            || name == Of(BinaryOperator.And)
            || name == Of(UnaryOperator.Not)
            || name == Of(BinaryOperator.Mod);
    }

    /// <summary>
    /// The seventeen function names as a message lists them: "SIN, COS, ..., MIN and MAX".
    /// </summary>
    public static string FunctionList()
    {
        ExprFunction[] functions = Enum.GetValues<ExprFunction>();
        var text = new StringBuilder();
        for (int index = 0; index < functions.Length; index++)
        {
            if (index == functions.Length - 1)
            {
                text.Append(" and ");
            }
            else if (index > 0)
            {
                text.Append(", ");
            }

            text.Append(Of(functions[index]));
        }

        return text.ToString();
    }
}
