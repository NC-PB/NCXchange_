namespace Ncx.Core.Catalog;

// The members are named after the value types of language 3 and the ValueKind of architecture 4 (code-guidelines 3.1:
// the specification names the thing); CA1720 reads integer, decimal and string as .NET type names.
#pragma warning disable CA1720

/// <summary>
/// The value types a word accepts, as flags because many words accept more than one: TOOL an integer, a string or
/// no value, TOLERANCE a number or OFF (language 3, value types; language 4).
/// </summary>
[Flags]
public enum ValueKinds
{
    /// <summary>
    /// No value type at all.
    /// </summary>
    None = 0,

    /// <summary>
    /// The word may stand without a value: RAPID, a bare TOOL, a bare SKIP.
    /// </summary>
    Bare = 1,

    /// <summary>
    /// An integer, -?[0-9]+.
    /// </summary>
    Integer = 2,

    /// <summary>
    /// A decimal, -?[0-9]+\.[0-9]+.
    /// </summary>
    Decimal = 4,

    /// <summary>
    /// An identifier, [A-Z][A-Z0-9_]*.
    /// </summary>
    Ident = 8,

    /// <summary>
    /// A list of identifiers or integers separated by commas without spaces.
    /// </summary>
    List = 16,

    /// <summary>
    /// A double-quoted string.
    /// </summary>
    String = 32,

    /// <summary>
    /// An expression in braces (language 4.12).
    /// </summary>
    Expr = 64,

    /// <summary>
    /// A state key KEY[:ADDR], the value of the pseudo-words @SAVE and @RESTORE only (D95).
    /// </summary>
    StateKey = 128,

    /// <summary>
    /// A number: an integer or a decimal (language 3).
    /// </summary>
    Number = Integer | Decimal,

    /// <summary>
    /// A number or an expression.
    /// </summary>
    NumberOrExpr = Number | Expr,
}

#pragma warning restore CA1720
