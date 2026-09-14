using Ncx.Core.Model;

namespace Ncx.Config;

// TODO(question): machine-config 1 and D10 give the tool table its content, the tool kind, length and radius per tool
// number, and no document gives the form of the file. It is read as [[tool]] tables with number (or name, for a tool
// that a program calls by name, language 4.4), kind (a key of [tool_change] kind_map, D52), length and radius, in the
// manner of the [[axis]] and [[resource]] tables of the machine file, until that is answered.

/// <summary>
/// The tool table of D10, the optional file that [machine] tool_table names: the tool kind, length and radius per
/// tool (machine-config 1). A compiler takes {kind} from it (machine-config 3, D52); without it, or for a tool it does
/// not describe, {kind} takes the default and the compiler writes a warning block at the head of the program (D10).
/// </summary>
public sealed record ToolTable
{
    // The file is made of [[tool]] tables, each with these keys.
    private static readonly string[] s_tables = ["tool"];
    private static readonly string[] s_toolKeys = ["number", "name", "kind", "length", "radius"];

    /// <summary>
    /// The tools by number, TOOL=4.
    /// </summary>
    public IReadOnlyDictionary<int, ToolData> Numbered { get; init; } = new Dictionary<int, ToolData>();

    /// <summary>
    /// The tools by name, TOOL="DRILL_D8" (language 4.4).
    /// </summary>
    public IReadOnlyDictionary<string, ToolData> Named { get; init; } = new Dictionary<string, ToolData>();

    /// <summary>
    /// The data of a tool; null when the table does not describe it.
    /// </summary>
    /// <param name="tool">The tool by number or by name.</param>
    public ToolData? Find(ToolRef tool)
    {
        if (tool.Number is int number)
        {
            return Numbered.TryGetValue(number, out ToolData? numbered) ? numbered : null;
        }

        return tool.Name is string name && Named.TryGetValue(name, out ToolData? named) ? named : null;
    }

    /// <summary>
    /// Loads a tool table from its text; its mistakes are diagnostics on their lines, a wrong type or a tool without
    /// number and name an ERROR, an unknown key a WARNING (P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the tool table.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The table, or null when the file has an ERROR.</returns>
    public static ToolTable? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        ConfigTable root = document.Root("machine-config 1, D10");
        root.WarnUnknownTables(s_tables, s_tables);
        var numbered = new Dictionary<int, ToolData>();
        var named = new Dictionary<string, ToolData>(StringComparer.Ordinal);
        foreach (ConfigTable tool in root.Tables("tool"))
        {
            // Every tool is found by its number, or by its name when a program calls it by name (language 4.4).
            tool.WarnUnknownKeys(s_toolKeys);
            var data = new ToolData
            {
                Kind = tool.Text("kind"),
                Length = tool.Number("length"),
                Radius = tool.Number("radius"),
            };
            if (tool.Integer("number") is int number)
            {
                numbered[number] = data;
            }
            else if (tool.Text("name") is string name)
            {
                named[name] = data;
            }
            else
            {
                tool.MissingKey("number");
            }
        }

        if (TomlDocument.ErrorCount(diagnostics) > errorsBefore)
        {
            return null;
        }

        return new ToolTable { Numbered = numbered, Named = named };
    }
}
