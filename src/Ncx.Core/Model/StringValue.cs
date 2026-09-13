using System.Text;

namespace Ncx.Core.Model;

/// <summary>
/// A string: double-quoted, may contain spaces and semicolons, with \" for a quote and \\ for a backslash
/// (language 3). The content is the text between the quotes with the escapes resolved.
/// </summary>
/// <param name="Content">The text between the quotes, the escapes resolved: SIDE "MILL" D10.</param>
public sealed record StringValue(string Content) : Value
{
    /// <summary>
    /// The content in double quotes with its escapes: "SIDE \"MILL\" D10" (language 3, string).
    /// </summary>
    public override string ToCanonical()
    {
        // A quote and a backslash inside the string are written with a backslash in front (language 3, string).
        var text = new StringBuilder();
        text.Append('"');
        foreach (char character in Content)
        {
            if (character is '"' or '\\')
            {
                text.Append('\\');
            }

            text.Append(character);
        }

        text.Append('"');
        return text.ToString();
    }
}
