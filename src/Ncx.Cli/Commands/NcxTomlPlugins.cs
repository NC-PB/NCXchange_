using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli.Commands;

/// <summary>
/// The line of ncx.toml that names the plugin assemblies, plugins = ["MyShop.NcxPlugins.dll"] (machine-config 10), to
/// which ncx plugin build adds a plugin (code-guidelines 11, step 5; implementation 17, P7-02). The text changes at
/// that one line, so that every comment and every other line of the file stays as the user wrote it, and a change is
/// kept only when the file loads with it and lists the plugin.
/// </summary>
internal static class NcxTomlPlugins
{
    // The key of the plugin assemblies (machine-config 10).
    private const string Key = "plugins";

    // No character: outside the strings of a line of TOML.
    private const char NoQuote = '\0';

    /// <summary>
    /// Tells whether ncx.toml lists a plugin DLL among its plugin assemblies, by its name or by its path in plugins/
    /// (machine-config 10; implementation 17, P7-01).
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark.</param>
    /// <param name="dll">The name of the DLL: CoolantClutch.dll.</param>
    public static bool Lists(string text, string dll)
    {
        if (Load(text) is not ProjectSettings settings)
        {
            return false;
        }

        string inPluginsFolder = PluginLoader.PluginsFolder + "/" + dll;
        foreach (string entry in settings.Plugins)
        {
            string written = entry.Replace('\\', '/');
            if (written == dll || written == inPluginsFolder)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The text of ncx.toml with a plugin DLL added to its plugin assemblies: a new line without one, the DLL at the
    /// end of a list on one line.
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark; null when there is no ncx.toml.</param>
    /// <param name="dll">The name of the DLL: CoolantClutch.dll.</param>
    /// <returns>The new text; null when ncx.toml holds its plugins in a form the DLL cannot join.</returns>
    public static string? Add(string? text, string dll)
    {
        // Machine-config 10: the plugin assemblies are the list plugins = ["MyShop.NcxPlugins.dll"], which names a DLL
        // of plugins/ by its name (implementation 17, P7-01).
        // TODO(question): D238. ncx.toml cannot hold this list beside the [plugins.<name>] sections of D80. The line
        // joins a file that names its plugins as the list, or not at all, in the form of machine-config 10; a file
        // with a section, or with a list over several lines, stays as it is, and the DLL in plugins/ loads without the
        // line, until D238 is answered.
        string entry = "\"" + dll + "\"";
        if (text is null)
        {
            return Key + " = [" + entry + "]\n";
        }

        string lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = LinesOf(text);
        int keyLine = KeyLineIndex(lines);
        if (keyLine >= 0)
        {
            if (WithEntry(lines[keyLine], entry) is not string withEntry)
            {
                return null;
            }

            lines[keyLine] = withEntry;
        }
        else
        {
            // A key of the file stands before its first table (TOML), so the new line goes there, or after the last
            // line of a file without a table.
            string line = Key + " = [" + entry + "]";
            int firstTable = FirstTableIndex(lines);
            if (firstTable >= 0)
            {
                lines.Insert(firstTable, line);
            }
            else if (lines[lines.Count - 1].Length == 0)
            {
                lines.Insert(lines.Count - 1, line);
            }
            else
            {
                lines.Add(line);
                lines.Add("");
            }
        }

        string changed = string.Join(lineEnding, lines);
        return Lists(changed, dll) ? changed : null;
    }

    /// <summary>
    /// The line of ncx.toml that names the plugin assemblies, as written: plugins = ["CoolantClutch.dll"].
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark.</param>
    /// <returns>The line; null for a file without one.</returns>
    public static string? KeyLine(string text)
    {
        List<string> lines = LinesOf(text);
        int keyLine = KeyLineIndex(lines);
        return keyLine < 0 ? null : lines[keyLine].Trim();
    }

    // ncx.toml as every command loads it; null when it has an ERROR (P2-01).
    private static ProjectSettings? Load(string text)
    {
        return ProjectSettingsLoader.LoadText(text, new Diagnostics(ProjectSettings.FileName));
    }

    // The lines of a text without their line endings; a text that ends with a line ending ends with an empty line.
    private static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.Split('\n'))
        {
            lines.Add(line.TrimEnd('\r'));
        }

        return lines;
    }

    // The line of the key of the plugin assemblies among the keys before the first table: plugins = [...]; -1 when
    // there is none.
    private static int KeyLineIndex(List<string> lines)
    {
        int firstTable = FirstTableIndex(lines);
        int end = firstTable >= 0 ? firstTable : lines.Count;
        for (int index = 0; index < end; index++)
        {
            string line = lines[index].TrimStart();
            if (line.StartsWith(Key, StringComparison.Ordinal)
                && line.Substring(Key.Length).TrimStart().StartsWith('='))
            {
                return index;
            }
        }

        return -1;
    }

    // The first line that begins a table, [plugins.MyShopRules]; -1 when there is none.
    private static int FirstTableIndex(List<string> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            if (lines[index].TrimStart().StartsWith('['))
            {
                return index;
            }
        }

        return -1;
    }

    // The line of the key with the DLL at the end of its list; null when the value is no list that ends on the line.
    private static string? WithEntry(string line, string entry)
    {
        int equals = line.IndexOf('=', StringComparison.Ordinal);
        int open = line.IndexOf('[', equals + 1);
        if (open < 0 || line.Substring(equals + 1, open - equals - 1).Trim().Length > 0)
        {
            return null;
        }

        int close = ClosingBracket(line, open);
        if (close < 0)
        {
            return null;
        }

        string inside = line.Substring(open + 1, close - open - 1).TrimEnd();
        string added = inside.Trim().Length == 0 ? entry : (inside.EndsWith(',') ? " " : ", ") + entry;
        return line.Insert(open + 1 + inside.Length, added);
    }

    // The bracket that closes the list opened at a position, outside the strings of TOML; -1 when the line ends or a
    // comment begins before it.
    private static int ClosingBracket(string line, int open)
    {
        char quote = NoQuote;
        for (int index = open + 1; index < line.Length; index++)
        {
            char character = line[index];
            if (quote != NoQuote)
            {
                if (character == '\\' && quote == '"')
                {
                    index++;
                }
                else if (character == quote)
                {
                    quote = NoQuote;
                }
            }
            else if (character == '"' || character == '\'')
            {
                quote = character;
            }
            else if (character == ']')
            {
                return index;
            }
            else if (character == '#')
            {
                return -1;
            }
        }

        return -1;
    }
}
