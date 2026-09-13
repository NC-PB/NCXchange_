namespace Ncx.Core.Model;

/// <summary>
/// An integer, -?[0-9]+ (language 3), kept with its text: NCX never rounds or reformats a number, and the writer
/// emits the text as read (language 2 rule 5).
/// </summary>
/// <param name="Number">The integer.</param>
/// <param name="Text">The integer as written: "1592", "-5".</param>
public sealed record IntegerValue(long Number, string Text) : Value
{
    /// <summary>
    /// The integer as written.
    /// </summary>
    public override string ToCanonical()
    {
        return Text;
    }
}
