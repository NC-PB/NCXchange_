namespace Ncx.Core.Expressions;

/// <summary>
/// One part of the text of an expression: a number, a name, a symbol or the end, with its position.
/// </summary>
/// <param name="Kind">What kind of part it is.</param>
/// <param name="Text">The text, a name in uppercase; empty for the end.</param>
/// <param name="Position">
/// The 1-based position of its first character in the text between the braces; for the end, one past the last
/// character.
/// </param>
internal sealed record ExprToken(ExprTokenKind Kind, string Text, int Position)
{
    /// <summary>
    /// True when the part is the symbol or the word of the grammar with this spelling: "(", "AND".
    /// </summary>
    public bool Is(string spelling)
    {
        return (Kind == ExprTokenKind.Symbol || Kind == ExprTokenKind.Name) && Text == spelling;
    }
}
