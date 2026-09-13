using Ncx.Core.Expressions;

namespace Ncx.Core.Model;

/// <summary>
/// An expression in braces, {$Q1 + 20}, evaluated by the virtual machine and allowed wherever a number is allowed
/// (language 3, 4.12): the text between the braces and the tree the expression parser builds from it.
/// </summary>
/// <param name="Text">The expression between the braces, as the writer emits it: "$Q1 + 20".</param>
/// <param name="Tree">The parsed expression (language 4.12); null while the text has not been parsed.</param>
public sealed record ExprValue(string Text, ExprNode? Tree) : Value
{
    /// <summary>
    /// The expression in braces (language 3, expression).
    /// </summary>
    public override string ToCanonical()
    {
        return "{" + Text + "}";
    }
}
