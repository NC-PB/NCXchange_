namespace Ncx.Core.Expressions;

/// <summary>
/// The operators with one operand (language 4.12).
/// </summary>
public enum UnaryOperator
{
    /// <summary>
    /// The unary minus, -, before a power (language 4.12, unary).
    /// </summary>
    Minus,

    /// <summary>
    /// NOT, before a comparison (language 4.12, notexpr).
    /// </summary>
    Not,
}
