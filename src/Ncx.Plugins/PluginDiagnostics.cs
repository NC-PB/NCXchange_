using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Plugins;

/// <summary>
/// What the plugins of a run report, into the diagnostics of the run (implementation 17, P7-01; D98). Every message
/// begins with the name of the plugin, "plugin MyShopRules: ...", so that the user knows whose it is: a plugin that
/// fails to load or throws is an ERROR and is left out while the run goes on, a block of a rewriter that does not
/// parse is an ERROR, and the blocks a rewriter inserted are the INFO "plugin MyShopRules: inserted 2 blocks at line
/// 12".
/// </summary>
public sealed class PluginDiagnostics
{
    private readonly Diagnostics _diagnostics;

    /// <summary>
    /// Reports into the diagnostics of a run.
    /// </summary>
    /// <param name="diagnostics">The diagnostics of the run; their file is the file whose blocks the plugins see, the
    /// NCX file or the controller program (D98).</param>
    public PluginDiagnostics(Diagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// The file whose blocks the plugins see, which their diagnostics about a block name (D98).
    /// </summary>
    internal string File => _diagnostics.File;

    // D98, code-guidelines 11: a rewriter that inserted blocks says so with its name, how many and at which line.
    internal void Inserted(string plugin, int blocks, int line)
    {
        string counted = blocks == 1 ? "1 block" : Invariant($"{blocks} blocks");
        _diagnostics.Info(line, DiagnosticCodes.InsertedBlocks,
            Invariant($"plugin {plugin}: inserted {counted} at line {line}"));
    }

    // Implementation 17, P7-01: a plugin that fails to load is reported with its name on its file, and left out.
    internal void NotLoaded(string plugin, string file, string reason)
    {
        Add(Severity.Error, file, DiagnosticCodes.PluginNotLoaded,
            $"plugin {plugin}: {file} cannot be loaded, {reason}; the run goes on without the plugin (architecture 9, "
            + "D106).");
    }

    // An assembly without a class of the four interfaces does nothing (the TODO(question) of PluginLoader.Load).
    internal void NoPluginClass(string plugin, string file)
    {
        Add(Severity.Warning, file, DiagnosticCodes.NoPluginClass,
            $"plugin {plugin}: {file} holds no public class that implements IProgramRewriter, ISourceRule, IVmListener "
            + "or IBlockWriter, so the plugin does nothing (architecture 9, D106).");
    }

    // Implementation 17, P7-01: a class of a plugin that fails in its method is reported with the plugin's name on the
    // block it failed at, and the plugin is left out for the rest of the run.
    internal void Failed(string plugin, string what, int line, int? originLine)
    {
        _diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = _diagnostics.File,
            Line = line,
            OriginLine = originLine,
            Code = DiagnosticCodes.PluginFailed,
            Message = $"plugin {plugin}: {what}; the plugin is left out for the rest of the run (architecture 9).",
        });
    }

    // Implementation 17, P7-01; code-guidelines 10.3; D95: a block of a rewriter's answer that does not parse under the
    // option of generated text leaves the whole answer out, reported on the line of the block it was for.
    internal void TextDoesNotParse(string plugin, string text, int line, string reason)
    {
        _diagnostics.Error(line, DiagnosticCodes.TextDoesNotParse,
            $"plugin {plugin}: the block \"{text}\" of its answer does not parse, so the answer is left out: {reason} "
            + "(code-guidelines 10.3, D95).");
    }

    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private void Add(Severity severity, string file, string code, string message)
    {
        _diagnostics.Add(new Diagnostic
        {
            Severity = severity,
            File = file,
            Line = 1,
            Code = code,
            Message = message,
        });
    }

    private static string Invariant(FormattableString text)
    {
        return text.ToString(CultureInfo.InvariantCulture);
    }
}
