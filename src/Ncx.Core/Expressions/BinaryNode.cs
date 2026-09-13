namespace Ncx.Core.Expressions;

/// <summary>
/// An operator with two operands: OR, AND, a comparison, a sum, a product or a power (language 4.12, orexpr to power).
/// </summary>
/// <param name="Operator">The operator.</param>
/// <param name="Left">The operand before the operator.</param>
/// <param name="Right">The operand after the operator.</param>
public sealed record BinaryNode(BinaryOperator Operator, ExprNode Left, ExprNode Right) : ExprNode
{
    /// <summary>
    /// The two operands with one space on each side of the operator, AND, OR and MOD included. The tree holds the
    /// parentheses of the expression as ParenthesesNode, so the operands are written as they stand.
    /// </summary>
    public override string ToCanonical()
    {
        return Left.ToCanonical() + " " + ExprSymbols.Of(Operator) + " " + Right.ToCanonical();
    }
}
