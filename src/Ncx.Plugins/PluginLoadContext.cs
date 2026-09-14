using System.Reflection;
using System.Runtime.Loader;

namespace Ncx.Plugins;

/// <summary>
/// The AssemblyLoadContext of one plugin DLL (architecture 9; code-guidelines 5, plugin isolation; D106): it shares
/// every Ncx.* assembly with the host, so that the interfaces a plugin implements and the types their methods take are
/// the host's own, and loads everything else the plugin brings from the plugin's folder, isolated from the host and
/// from the other plugins.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    // The assemblies of NCXchange: Ncx.Core, Ncx.Config, Ncx.Readers, Ncx.Compilers, Ncx.Plugins and the others.
    private const string NcxPrefix = "Ncx.";

    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// The context of one plugin.
    /// </summary>
    /// <param name="plugin">The name of the plugin, which names the context.</param>
    /// <param name="pluginFile">The full path of the DLL, whose .deps.json, or else its folder, tells where the
    /// assemblies it brings lie.</param>
    public PluginLoadContext(string plugin, string pluginFile)
        : base(plugin)
    {
        _resolver = new AssemblyDependencyResolver(pluginFile);
    }

    /// <summary>
    /// Loads an assembly of the plugin from its file into this context. The file is read into memory, with its symbols
    /// when a .pdb lies next to it, so that it stays free: ncx plugin build can replace the DLL of a plugin another
    /// run has loaded, and nothing in plugins/ is locked on Windows.
    /// </summary>
    /// <param name="path">The full path of the assembly.</param>
    public Assembly LoadFromFile(string path)
    {
        using FileStream assembly = File.OpenRead(path);
        string symbolsPath = Path.ChangeExtension(path, ".pdb");
        if (!File.Exists(symbolsPath))
        {
            return LoadFromStream(assembly);
        }

        using FileStream symbols = File.OpenRead(symbolsPath);
        return LoadFromStream(assembly, symbols);
    }

    // An Ncx.* assembly comes from the host, even when the plugin brings a copy: no answer here hands the name to the
    // context of the host (D106). Anything else the plugin brings is loaded from its folder into this context; what it
    // does not bring, the base library among it, comes from the host as well.
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is string name && name.StartsWith(NcxPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromFile(path);
    }

    // A native library the plugin brings is loaded from its folder as well.
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
