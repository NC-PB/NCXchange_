using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// The start values of the variables of a program, &lt;name&gt;.vars.toml: numbers and strings by variable name, the
/// seed of the variable store that analyze starts from (machine-config 8, virtual machine 2.7, 3.6).
/// </summary>
public sealed record VarsFile
{
    /// <summary>
    /// The start value of each variable in file order: Q1 = 10 as an integer, Q2 = 5.5 as a decimal, QS1 = "TEXT" as
    /// a string.
    /// </summary>
    public IReadOnlyDictionary<string, Value> Variables { get; init; } = new Dictionary<string, Value>();

    /// <summary>
    /// Loads the vars file at a path. A file that cannot be read is an I/O error, thrown for the composition root
    /// (code-guidelines 6).
    /// </summary>
    /// <param name="path">The vars file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The start values, or null when the file has an ERROR.</returns>
    public static VarsFile? Load(string path, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), diagnostics);
    }

    /// <summary>
    /// Loads a vars file from its text: every key is a variable and its value a number or a string; anything else is
    /// a wrong type, an ERROR on its line (machine-config 8, P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the vars file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The start values, or null when the file has an ERROR.</returns>
    public static VarsFile? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        // Every key of the file names a variable; its start value is a number or a string, kept as written
        // (machine-config 8).
        ConfigTable root = document.Root("machine-config 8");
        var variables = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (string name in root.Keys)
        {
            if (root.NumberOrString(name) is Value value)
            {
                variables[name] = value;
            }
        }

        if (TomlDocument.ErrorCount(diagnostics) > errorsBefore)
        {
            return null;
        }

        return new VarsFile { Variables = variables };
    }
}
