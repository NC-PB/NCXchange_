namespace Ncx.Core.Expressions;

/// <summary>
/// A number in an expression: an integer or a decimal of language 3 without a sign, because a minus in front of it is
/// the unary minus of the grammar (language 3, 4.12, primary and unary).
/// </summary>
/// <param name="Text">The digits as written: "20", "0.05", "007.50"; the evaluator reads the value from them.</param>
public sealed record NumberNode(string Text) : ExprNode
{
    /// <summary>
    /// The number as written, never rounded or reformatted (language 2 rule 5).
    /// </summary>
    public override string ToCanonical()
    {
        return Text;
    }
}
