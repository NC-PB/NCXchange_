using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Ncx.Compilers;
using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Plugins;
using Ncx.Readers;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin check &lt;dll&gt;: loads a plugin DLL in a context of its own, as a run loads it, and lists the
/// interfaces it implements and the version of the Ncx assemblies it was built against (code-guidelines 11;
/// implementation 17, P7-02, and its risks). What the loader reports goes to the standard error (D98). Exit code 0; 1
/// when the plugin does not load; 2 when the file is not there (D97).
/// </summary>
internal static class PluginCheck
{
    // The assemblies of NCXchange, which a plugin shares with ncx (D106).
    private const string NcxPrefix = "Ncx.";

    // A diagnostic about a whole file stands on its first line.
    private const int FileLine = 1;

    // check loads a plugin without the settings of ncx.toml, which change nothing of what it implements (D80).
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_noSettings = [];

    /// <summary>
    /// Loads a plugin DLL and lists what it implements.
    /// </summary>
    /// <param name="dll">The DLL as the command line names it, from the working directory: plugins/MyShopRules.dll.
    /// </param>
    /// <param name="settings">The working directory and --strict.</param>
    /// <param name="output">The standard output, for the listing.</param>
    /// <param name="error">The standard error, for the diagnostics (D98).</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string dll, PluginCommandSettings settings, TextWriter output, TextWriter error)
    {
        var diagnostics = new Diagnostics(dll);
        string path = Path.Combine(settings.WorkingDirectory, dll);

        // The DLL is the input of check: one that is not there decides exit code 2 before the run starts (D97).
        if (!File.Exists(path))
        {
            diagnostics.Error(FileLine, DiagnosticCodes.InputUnreadable,
                "The plugin cannot be read: there is no such file (implementation 17, P7-02; D97).");
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // The plugin loads as in a run: in a context of its own that shares the Ncx assemblies with ncx (D106), every
        // public class of the four interfaces made, and what fails reported with the name of the plugin
        // (implementation 17, P7-01). A plugin that does not load has nothing to list.
        PluginSet plugins = PluginLoader.Load(settings.WorkingDirectory, [dll], s_noSettings,
            new PluginDiagnostics(diagnostics));
        if (!diagnostics.HasErrors)
        {
            output.Write(Listing(dll, path, plugins));
        }

        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // Code-guidelines 11: the plugin, the Ncx assemblies it was built against with their version (implementation 17,
    // risks), and each of the four interfaces it implements with the number of its classes, in the order of
    // architecture 9: reader, expander, virtual machine, compiler.
    // TODO(question): no document gives what the plugin commands write on the standard output: this listing, the
    // files ncx plugin new writes, the DLL and the line of ncx.toml of ncx plugin build. Each is named on a line of its
    // own, until that is answered.
    private static string Listing(string dll, string path, PluginSet plugins)
    {
        var text = new StringBuilder();
        text.Append(dll).Append(": plugin ").Append(Path.GetFileNameWithoutExtension(dll)).Append('\n');
        List<string> references = NcxReferences(path);
        if (references.Count > 0)
        {
            text.Append("  built against ").AppendJoin(", ", references).Append('\n');
        }

        AppendInterface(text, nameof(ISourceRule), plugins.SourceRules.Count);
        AppendInterface(text, nameof(IProgramRewriter), plugins.Rewriters.Count);
        AppendInterface(text, nameof(IVmListener), plugins.Listeners.Count);
        AppendInterface(text, nameof(IBlockWriter), plugins.BlockWriters.Count);
        return text.ToString();
    }

    // One interface the plugin implements, with the number of its classes: IProgramRewriter: 1 class.
    private static void AppendInterface(StringBuilder text, string name, int classes)
    {
        if (classes == 0)
        {
            return;
        }

        string counted = classes == 1 ? "1 class" : classes.ToString(CultureInfo.InvariantCulture) + " classes";
        text.Append("  ").Append(name).Append(": ").Append(counted).Append('\n');
    }

    // Implementation 17, risks: the version of every Ncx assembly the plugin references, read from its metadata,
    // Ncx.Core 1.0.0.0, in the ordinal order of the names; none for a file whose metadata cannot be read, which the
    // loader has reported.
    private static List<string> NcxReferences(string path)
    {
        var references = new List<string>();
        try
        {
            using FileStream stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            MetadataReader metadata = reader.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                string name = metadata.GetString(reference.Name);
                if (name.StartsWith(NcxPrefix, StringComparison.Ordinal))
                {
                    references.Add(name + " " + reference.Version.ToString());
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or BadImageFormatException or InvalidOperationException)
        {
            return [];
        }

        references.Sort(StringComparer.Ordinal);
        return references;
    }
}
