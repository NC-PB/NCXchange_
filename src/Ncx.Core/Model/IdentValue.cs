namespace Ncx.Core.Model;

/// <summary>
/// An identifier, [A-Z][A-Z0-9_]*: CW, PER_REV, LEFT (language 3).
/// </summary>
/// <param name="Name">The identifier, uppercase.</param>
public sealed record IdentValue(string Name) : Value
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public override string ToCanonical()
    {
        return Name;
    }
}
