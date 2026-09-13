namespace Ncx.Core.Model;

/// <summary>
/// A list of identifiers or integers separated by commas without spaces: MAIN,SUB or 1,2 (language 3).
/// </summary>
/// <param name="Items">The items as written, in their order.</param>
public sealed record ListValue(IReadOnlyList<string> Items) : Value
{
    /// <summary>
    /// The items separated by commas without spaces (language 3, list).
    /// </summary>
    public override string ToCanonical()
    {
        return string.Join(',', Items);
    }

    /// <summary>
    /// Two lists are the same value when they hold the same items in the same order.
    /// </summary>
    public bool Equals(ListValue? other)
    {
        return other is not null && Items.SequenceEqual(other.Items);
    }

    /// <summary>
    /// A hash of the items, so that equal lists hash alike.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (string item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
