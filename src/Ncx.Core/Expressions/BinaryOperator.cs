namespace Ncx.Core.Expressions;

/// <summary>
/// The operators with two operands, from the weakest binding to the strongest, as the grammar of language 4.12 orders
/// them. Comparisons yield 1 or 0 (language 4.12).
/// </summary>
public enum BinaryOperator
{
    /// <summary>
    /// OR (language 4.12, orexpr).
    /// </summary>
    Or,

    /// <summary>
    /// AND (language 4.12, andexpr).
    /// </summary>
    And,

    /// <summary>
    /// The comparison == (language 4.12, cmpexpr).
    /// </summary>
    Equal,

    /// <summary>
    /// The comparison != (language 4.12, cmpexpr).
    /// </summary>
    NotEqual,

    /// <summary>
    /// The comparison &lt; (language 4.12, cmpexpr).
    /// </summary>
    Less,

    /// <summary>
    /// The comparison &lt;= (language 4.12, cmpexpr).
    /// </summary>
    LessOrEqual,

    /// <summary>
    /// The comparison &gt; (language 4.12, cmpexpr).
    /// </summary>
    Greater,

    /// <summary>
    /// The comparison &gt;= (language 4.12, cmpexpr).
    /// </summary>
    GreaterOrEqual,

    /// <summary>
    /// + (language 4.12, sum).
    /// </summary>
    Add,

    /// <summary>
    /// - (language 4.12, sum).
    /// </summary>
    Subtract,

    /// <summary>
    /// * (language 4.12, product).
    /// </summary>
    Multiply,

    /// <summary>
    /// / (language 4.12, product); division by zero is an ERROR of the evaluator.
    /// </summary>
    Divide,

    /// <summary>
    /// MOD, which keeps the sign of the dividend (language 4.12, product).
    /// </summary>
    Mod,

    /// <summary>
    /// ^, the power (language 4.12, power).
    /// </summary>
    Power,
}
