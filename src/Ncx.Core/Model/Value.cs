namespace Ncx.Core.Model;

/// <summary>
/// The value of a word, one of the value types of language 3: none, integer, decimal, identifier, list, string,
/// expression, and the state key of the pseudo-words (D95). The set is closed: every value is one of the sealed
/// records NoValue, IntegerValue, DecimalValue, IdentValue, ListValue, StringValue, ExprValue and StateKeyValue.
/// </summary>
public abstract record Value
{
    // Only the value types of language 3 derive from Value, so that a switch over the eight records is complete.
    private protected Value()
    {
    }

    /// <summary>
    /// The value as canonical NCX writes it after the equals sign of its word; empty for NoValue. Numbers are
    /// written as they were read, never rounded or reformatted (language 2 rule 5).
    /// </summary>
    public abstract string ToCanonical();
}
