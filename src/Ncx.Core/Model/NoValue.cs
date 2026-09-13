namespace Ncx.Core.Model;

/// <summary>
/// The value of a word written without one: RAPID, a bare TOOL, a bare SKIP (language 3, Word).
/// </summary>
public sealed record NoValue : Value
{
    private NoValue()
    {
    }

    /// <summary>
    /// The one value of every word written without a value.
    /// </summary>
    public static NoValue Instance { get; } = new();

    /// <summary>
    /// Empty: a word without a value has no equals sign.
    /// </summary>
    public override string ToCanonical()
    {
        return "";
    }
}
