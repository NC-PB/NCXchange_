namespace Ncx.Core.Model;

/// <summary>
/// A decimal, -?[0-9]+\.[0-9]+ (language 3), kept with its text: NCX never rounds or reformats a number, and the
/// writer emits the text as read (language 2 rule 5). Coordinates and feeds are decimal, never double
/// (code-guidelines 7).
/// </summary>
/// <param name="Number">The decimal.</param>
/// <param name="Text">The decimal as written: "-7.025", "0.05".</param>
public sealed record DecimalValue(decimal Number, string Text) : Value
{
    /// <summary>
    /// The decimal as written.
    /// </summary>
    public override string ToCanonical()
    {
        return Text;
    }
}
