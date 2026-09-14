using System.Collections.ObjectModel;
using Ncx.Compilers;
using Ncx.Core.Expander;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Readers;

namespace Ncx.Plugins;

/// <summary>
/// Loads the plugins of a working directory (architecture 9; code-guidelines 5, plugin isolation; D80, D106;
/// implementation 17, P7-01): every DLL into an AssemblyLoadContext of its own, which shares the Ncx.* assemblies with
/// the host and isolates the rest, and every public class of it that implements one of the four interfaces into the
/// place it acts in, with the plugin's own settings. A plugin that fails to load is reported with its name and left
/// out for the run, which goes on without it; nothing a plugin does takes the command line down.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// plugins/: the folder of the working directory whose every DLL is a plugin; ncx plugin build puts the DLL of a
    /// plugin there (implementation 17, P7-01, P7-02).
    /// </summary>
    public const string PluginsFolder = "plugins";

    // A plugin is a DLL.
    private const string DllPattern = "*.dll";

    /// <summary>
    /// The plugin DLLs of a working directory in load order.
    /// </summary>
    /// <param name="workingDirectory">The working directory, the folder of ncx.toml.</param>
    /// <param name="listed">The [plugins] list of ncx.toml, plugins = ["MyShop.NcxPlugins.dll"] (machine-config
    /// 10).</param>
    /// <returns>The files as the user finds them from the working directory: "plugins/MyShopRules.dll".</returns>
    public static IReadOnlyList<string> Files(string workingDirectory, IReadOnlyList<string> listed)
    {
        // The plugins come from the [plugins] list of ncx.toml and from plugins/ (implementation 17, P7-01), each DLL
        // once, since each gets a context of its own.
        // TODO(question): no document gives the load order, which is the order the rewriters of several plugins act in
        // (D199 recommends "plugin load order" without giving it), nor where a name of the [plugins] list is found. The
        // list comes first, in its order, then the DLLs of plugins/ that it does not name, in the ordinal order of
        // their names; a name of the list is a path from the working directory, and a name that is no file there is
        // looked for in plugins/, where ncx plugin build puts it (P7-02), until that is answered.
        var files = new List<string>();
        var fullPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string entry in listed)
        {
            string file = FindListed(workingDirectory, entry);
            if (fullPaths.Add(FullPath(workingDirectory, file)))
            {
                files.Add(file);
            }
        }

        foreach (string name in DllsOfPluginsFolder(workingDirectory))
        {
            string file = Path.Combine(PluginsFolder, name);
            if (fullPaths.Add(FullPath(workingDirectory, file)))
            {
                files.Add(file);
            }
        }

        return files;
    }

    /// <summary>
    /// Loads plugin DLLs, each into a context of its own, in the order given.
    /// </summary>
    /// <param name="workingDirectory">The folder a relative file is found from.</param>
    /// <param name="files">The DLLs, as <see cref="Files"/> gives them.</param>
    /// <param name="settings">The [plugins.&lt;name&gt;] sections of ncx.toml by plugin name (D80).</param>
    /// <param name="diagnostics">Where the plugins report, into the diagnostics of the run.</param>
    /// <returns>The plugins by the place they act in; what failed to load is not in it.</returns>
    public static PluginSet Load(string workingDirectory, IReadOnlyList<string> files,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> settings, PluginDiagnostics diagnostics)
    {
        var plugins = new PluginSet();
        foreach (string file in files)
        {
            // A plugin is named after its DLL, MyShopRules.dll is MyShopRules: the name of its section of ncx.toml
            // (D80) and of every diagnostic about it (implementation 17, P7-01).
            string name = Path.GetFileNameWithoutExtension(file);
            List<object>? classes = PluginClasses.Load(name, file, FullPath(workingDirectory, file), diagnostics);
            if (classes is null)
            {
                continue;
            }

            // TODO(question): no document says what a DLL among the plugins that implements none of the four
            // interfaces is, a dependency a plugin brings into plugins/ among them. It is a WARNING and gives nothing,
            // until that is answered.
            if (classes.Count == 0)
            {
                diagnostics.NoPluginClass(name, file);
                continue;
            }

            var plugin = new Plugin(name, SettingsOf(name, settings));
            foreach (object instance in classes)
            {
                plugins.Add(plugin, instance, diagnostics);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Tells whether a type is a class the loader makes an object of: public, not abstract, not generic, and
    /// implementing at least one of the four interfaces of architecture 9, whose types are the host's (D106).
    /// </summary>
    internal static bool IsPluginClass(Type type)
    {
        return type.IsClass
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && (typeof(IProgramRewriter).IsAssignableFrom(type)
                || typeof(ISourceRule).IsAssignableFrom(type)
                || typeof(IVmListener).IsAssignableFrom(type)
                || typeof(IBlockWriter).IsAssignableFrom(type));
    }

    // D80: the plugin's own section of ncx.toml as strings and nothing else of the file, empty without one; a copy no
    // plugin can change.
    private static ReadOnlyDictionary<string, string> SettingsOf(string name,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> settings)
    {
        var own = new Dictionary<string, string>(StringComparer.Ordinal);
        if (settings.TryGetValue(name, out IReadOnlyDictionary<string, string>? section))
        {
            foreach (KeyValuePair<string, string> setting in section)
            {
                own[setting.Key] = setting.Value;
            }
        }

        return new ReadOnlyDictionary<string, string>(own);
    }

    // A name of the [plugins] list as a file: a path from the working directory, or else the file of that name in
    // plugins/ (the TODO(question) of Files); one that is in neither is kept as written and reported when it loads.
    private static string FindListed(string workingDirectory, string entry)
    {
        if (File.Exists(FullPath(workingDirectory, entry)))
        {
            return entry;
        }

        string inPluginsFolder = Path.Combine(PluginsFolder, entry);
        return File.Exists(FullPath(workingDirectory, inPluginsFolder)) ? inPluginsFolder : entry;
    }

    // The names of the DLLs of plugins/ in ordinal order; none without the folder.
    private static List<string> DllsOfPluginsFolder(string workingDirectory)
    {
        var names = new List<string>();
        string folder = Path.Combine(workingDirectory, PluginsFolder);
        if (Directory.Exists(folder))
        {
            foreach (string path in Directory.GetFiles(folder, DllPattern))
            {
                names.Add(Path.GetFileName(path));
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string FullPath(string workingDirectory, string file)
    {
        return Path.GetFullPath(Path.Combine(workingDirectory, file));
    }
}
