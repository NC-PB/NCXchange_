namespace Ncx.Core.Expressions;

/// <summary>
/// A node of the tree of an expression: the grammar of language 4.12 maps one production to one node type, the
/// expression parser builds the tree and the evaluator walks it (code-guidelines 5, Interpreter). The set is closed:
/// every node is one of the sealed records NumberNode, VariableNode, CallNode, UnaryNode, BinaryNode and
/// ParenthesesNode.
/// </summary>
public abstract record ExprNode
{
    // Only the node types of the grammar derive from ExprNode, so that a switch over the six records is complete.
    private protected ExprNode()
    {
    }

    /// <summary>
    /// The expression as canonical NCX writes it between the braces: one space on each side of a binary operator,
    /// AND, OR and MOD included; the unary minus directly before its operand; ", " between the arguments of a call; no
    /// spaces inside parentheses and brackets; numbers as written. The braces are the writer's (ExprValue).
    /// </summary>
    public abstract string ToCanonical();

    /// <summary>
    /// The canonical text, so that a tree reads as NCX in test output and in the debugger.
    /// </summary>
    public sealed override string ToString()
    {
        return ToCanonical();
    }
}
