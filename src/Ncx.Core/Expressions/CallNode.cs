using System.Text;

namespace Ncx.Core.Expressions;

/// <summary>
/// A call of one of the seventeen functions of an expression with its arguments, MAX($A, 2) (language 4.12, primary
/// and function).
/// </summary>
/// <param name="Function">The function called.</param>
/// <param name="Arguments">
/// The arguments in the order written, at least one: function "(" expr { "," expr } ")" (language 4.12).
/// </param>
public sealed record CallNode(ExprFunction Function, IReadOnlyList<ExprNode> Arguments) : ExprNode
{
    /// <summary>
    /// The name of the function, then the arguments separated by ", " in parentheses without spaces inside them.
    /// </summary>
    public override string ToCanonical()
    {
        var text = new StringBuilder();
        text.Append(ExprSymbols.Of(Function)).Append('(');
        for (int index = 0; index < Arguments.Count; index++)
        {
            if (index > 0)
            {
                text.Append(", ");
            }

            text.Append(Arguments[index].ToCanonical());
        }

        return text.Append(')').ToString();
    }

    /// <summary>
    /// Two calls are the same tree when they call the same function with equal arguments in the same order.
    /// </summary>
    public bool Equals(CallNode? other)
    {
        return other is not null && Function == other.Function && Arguments.SequenceEqual(other.Arguments);
    }

    /// <summary>
    /// A hash of the function and the arguments, so that equal calls hash alike.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Function);
        foreach (ExprNode argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }
}
