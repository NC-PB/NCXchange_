namespace Ncx.Core.Expressions;

/// <summary>
/// The kinds of the parts the text of an expression is split into before it is parsed (language 4.12).
/// </summary>
internal enum ExprTokenKind
{
    /// <summary>
    /// A number of language 3 without a sign: 20, 0.05.
    /// </summary>
    Number,

    /// <summary>
    /// A name, uppercase: a function, one of the words AND, OR, NOT and MOD, or a variable name after $.
    /// </summary>
    Name,

    /// <summary>
    /// An operator or a delimiter: + - * / ^ == != &lt; &lt;= &gt; &gt;= ( ) [ ] , $.
    /// </summary>
    Symbol,

    /// <summary>
    /// The end of the expression.
    /// </summary>
    End,
}
