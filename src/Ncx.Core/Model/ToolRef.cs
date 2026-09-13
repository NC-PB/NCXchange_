using System.Globalization;

namespace Ncx.Core.Model;

/// <summary>
/// The tool of TOOL and PRELOAD, a number or a name: their value is an integer or a string (language 4.4).
/// </summary>
public readonly record struct ToolRef
{
    /// <summary>
    /// A tool by number: TOOL=4.
    /// </summary>
    /// <param name="number">The tool number.</param>
    public ToolRef(int number)
    {
        Number = number;
        Name = null;
    }

    /// <summary>
    /// A tool by name: TOOL="DRILL_D8".
    /// </summary>
    /// <param name="name">The tool name.</param>
    public ToolRef(string name)
    {
        Number = null;
        Name = name;
    }

    /// <summary>
    /// The tool number; null for a tool by name.
    /// </summary>
    public int? Number { get; }

    /// <summary>
    /// The tool name; null for a tool by number.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The tool as NCX writes it: the number, or the name as a string in quotes (language 4.4).
    /// </summary>
    public override string ToString()
    {
        if (Name is not null)
        {
            return new StringValue(Name).ToCanonical();
        }

        return Number?.ToString(CultureInfo.InvariantCulture) ?? "";
    }
}
