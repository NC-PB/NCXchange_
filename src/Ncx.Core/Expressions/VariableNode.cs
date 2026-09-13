namespace Ncx.Core.Expressions;

/// <summary>
/// A variable read in an expression, $Q1, or a register or table row of a system variable selected by an index,
/// $SYS_WEAR_Z[99] (language 4.9, 4.12, variable; D51).
/// </summary>
/// <param name="Name">
/// The name after the dollar sign, uppercase: "Q1", "SYS_WEAR_Z" (language 4.9: variable names follow ADDR).
/// </param>
/// <param name="Index">The expression between the brackets; null for a variable without an index.</param>
public sealed record VariableNode(string Name, ExprNode? Index) : ExprNode
{
    /// <summary>
    /// The dollar sign, the name, and the index in brackets without spaces inside them.
    /// </summary>
    public override string ToCanonical()
    {
        if (Index is null)
        {
            return "$" + Name;
        }

        return "$" + Name + "[" + Index.ToCanonical() + "]";
    }
}
