using System.Text;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// Writes the tree of an expression with every operator node in parentheses, so that a test shows which operand
/// belongs to which operator: 1 + 2 * 3 is (1 + (2 * 3)), -2 ^ 2 is (-(2 ^ 2)). Parentheses written in the expression
/// itself show as paren(...).
/// </summary>
internal static class ExprStructure
{
    /// <summary>
    /// Parses an expression that must be valid and returns the structure of its tree.
    /// </summary>
    public static string Parse(string text)
    {
        return Of(ParseValid(text));
    }

    /// <summary>
    /// Parses an expression that must be valid: no diagnostic and a tree.
    /// </summary>
    public static ExprNode ParseValid(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ExprNode? tree = ExprParser.Parse(text, 1, diagnostics);

        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        Assert.NotNull(tree);
        return tree;
    }

    /// <summary>
    /// The structure of a tree.
    /// </summary>
    public static string Of(ExprNode node)
    {
        return node switch
        {
            NumberNode number => number.Text,
            VariableNode variable => OfVariable(variable),
            CallNode call => OfCall(call),
            UnaryNode unary => "(" + ExprSymbols.Of(unary.Operator) + Separator(unary) + Of(unary.Operand) + ")",
            BinaryNode binary => "(" + Of(binary.Left) + " " + ExprSymbols.Of(binary.Operator) + " " + Of(binary.Right)
                + ")",
            ParenthesesNode parentheses => "paren(" + Of(parentheses.Inner) + ")",
            _ => throw new ArgumentException($"{node.GetType().Name} is not a node of language 4.12.", nameof(node)),
        };
    }

    // NOT is a word and needs a space before its operand; the minus does not.
    private static string Separator(UnaryNode unary)
    {
        return unary.Operator == UnaryOperator.Not ? " " : "";
    }

    private static string OfVariable(VariableNode variable)
    {
        if (variable.Index is null)
        {
            return "$" + variable.Name;
        }

        return "$" + variable.Name + "[" + Of(variable.Index) + "]";
    }

    private static string OfCall(CallNode call)
    {
        var text = new StringBuilder();
        text.Append(ExprSymbols.Of(call.Function)).Append('(');
        for (int index = 0; index < call.Arguments.Count; index++)
        {
            if (index > 0)
            {
                text.Append(", ");
            }

            text.Append(Of(call.Arguments[index]));
        }

        return text.Append(')').ToString();
    }
}
