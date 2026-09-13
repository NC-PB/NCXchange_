namespace Ncx.Core.Expressions;

/// <summary>
/// An expression in parentheses, ($Q1 + 1) * 2 (language 4.12, primary = "(" expr ")"). The tree keeps the
/// parentheses where the program wrote them, so that the canonical text never changes the grouping.
/// </summary>
/// <param name="Inner">The expression between the parentheses.</param>
public sealed record ParenthesesNode(ExprNode Inner) : ExprNode
{
    /// <summary>
    /// The inner expression in parentheses without spaces inside them.
    /// </summary>
    public override string ToCanonical()
    {
        // TODO(question): language 4.12 lets a program write parentheses the grammar does not need, ((1)) or
        // ($Q1 < $Q2) AND ($Q3 > 0), and does not say whether the canonical text keeps them. They are kept as written,
        // which loses nothing of the program (language 2 rule 8) until that is settled.
        return "(" + Inner.ToCanonical() + ")";
    }
}
