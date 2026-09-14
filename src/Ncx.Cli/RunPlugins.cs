using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli;

/// <summary>
/// The plugins of a run (architecture 9, 10; D80, D106; implementation 17, P7-01): the assemblies the [plugins] list of
/// ncx.toml names and every DLL of plugins/ of the working directory, each with its own [plugins.&lt;name&gt;]
/// settings, loaded once the inputs of the run are read. What the plugins report goes into the diagnostics of the run,
/// named after the plugin; a plugin that fails is left out, and the run goes on without it.
/// </summary>
internal static class RunPlugins
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_noSettings = [];

    /// <summary>
    /// Loads the plugins of the working directory of a run.
    /// </summary>
    /// <param name="settings">The file and the working directory of the run.</param>
    /// <param name="project">ncx.toml of the working directory; null without one.</param>
    /// <param name="diagnostics">The diagnostics of the run, whose file is the file the plugins see.</param>
    public static PluginSet Load(RunSettings settings, ProjectSettings? project, Diagnostics diagnostics)
    {
        // TODO(question): D238. ncx.toml cannot hold the [plugins] list of machine-config 10 and the [plugins.<name>]
        // sections of D80 under one key; ProjectSettings reads an array as the list and a table as the sections. The
        // DLLs of plugins/ load either way, with the sections of a table, until D238 is answered.
        IReadOnlyList<string> files = PluginLoader.Files(settings.WorkingDirectory, project?.Plugins ?? []);
        return PluginLoader.Load(settings.WorkingDirectory, files, project?.PluginSettings ?? s_noSettings,
            new PluginDiagnostics(diagnostics));
    }
}
