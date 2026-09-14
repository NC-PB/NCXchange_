namespace Ncx.Core.Expressions;

/// <summary>
/// An operator with one operand: the unary minus before a power, -$A (language 4.12, unary), or NOT before a
/// comparison, NOT $Q3 (language 4.12, notexpr).
/// </summary>
/// <param name="Operator">The operator.</param>
/// <param name="Operand">What the operator applies to: the power behind the minus, the comparison behind NOT.</param>
public sealed record UnaryNode(UnaryOperator Operator, ExprNode Operand) : ExprNode
{
    /// <summary>
    /// The minus directly before its operand; NOT with one space before its operand.
    /// </summary>
    public override string ToCanonical()
    {
        // The unary minus stands directly before its operand (P0-05, canonical text).
        if (Operator == UnaryOperator.Minus)
        {
            return ExprSymbols.Of(Operator) + Operand.ToCanonical();
        }

        // TODO(question): the phase file (P0-05) writes NOT, like the minus, directly before its operand, but its own
        // case {$Q1 < $Q2 AND NOT $Q3} has a space, and NOT written directly before a function name reads back as one
        // name (NOTSIN). NOT is written with one space until the canonical text of NOT is settled (D112).
        return ExprSymbols.Of(Operator) + " " + Operand.ToCanonical();
    }
}
