namespace Ncx.Core.Expressions;

/// <summary>
/// A node of the tree of an expression: the grammar of language 4.12 maps one production to one node type, the
/// expression parser builds the tree and the evaluator walks it (code-guidelines 5, Interpreter).
/// </summary>
public abstract record ExprNode;
