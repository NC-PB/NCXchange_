using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The plugins that Ncx.Acceptance builds from Plugins/TestPlugins/ and copies into TestPlugins/ next to the test
/// assembly (Ncx.Acceptance.csproj): ShopRules, which does what the plugin of a shop does, and FaultyRules, whose
/// classes fail. A test copies the ones it needs into a folder of its own and loads them from there.
/// </summary>
internal static class TestPlugins
{
    /// <summary>
    /// The plugin of code-guidelines 11 with one class of each interface: CoolantClutchRule and BeforeToolChange
    /// (IProgramRewriter), ShopCoolantCode (ISourceRule), EventRecorder (IVmListener), ZOnItsOwnLine (IBlockWriter).
    /// </summary>
    public const string ShopRules = "ShopRules";

    /// <summary>
    /// The plugin whose classes fail: ThrowingRule throws on COOLANT, BrokenTextRule inserts FOO=1 before DWELL,
    /// ThrowingSourceRule throws on M457 with a block begun, ThrowingListener throws on TOOL_BEGIN, ThrowingWriter
    /// edits the lines of a RAPID block and throws.
    /// </summary>
    public const string FaultyRules = "FaultyRules";

    /// <summary>
    /// No settings for any plugin.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> NoSettings { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// The DLL of a test plugin as the build left it, next to the test assembly, never in the working directory
    /// (code-guidelines 8).
    /// </summary>
    public static string PathOf(string plugin)
    {
        string folder = Path.GetDirectoryName(typeof(TestPlugins).Assembly.Location) ?? "";
        return Path.Combine(folder, "TestPlugins", plugin + ".dll");
    }

    /// <summary>
    /// Copies test plugins into a folder, which is created when it is not there.
    /// </summary>
    public static void CopyInto(string folder, params string[] plugins)
    {
        Directory.CreateDirectory(folder);
        foreach (string plugin in plugins)
        {
            File.Copy(PathOf(plugin), Path.Combine(folder, plugin + ".dll"), overwrite: true);
        }
    }

    /// <summary>
    /// Copies test plugins into a folder of its own and loads them from there, in this order.
    /// </summary>
    public static PluginSet Load(string folder, Diagnostics diagnostics, params string[] plugins)
    {
        return Load(folder, diagnostics, NoSettings, plugins);
    }

    /// <summary>
    /// Copies test plugins into a folder of its own and loads them from there with the given settings.
    /// </summary>
    public static PluginSet Load(string folder, Diagnostics diagnostics,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> settings, params string[] plugins)
    {
        CopyInto(folder, plugins);
        var files = new List<string>();
        foreach (string plugin in plugins)
        {
            files.Add(plugin + ".dll");
        }

        return PluginLoader.Load(folder, files, settings, new PluginDiagnostics(diagnostics));
    }

    /// <summary>
    /// The object of the plugin behind one entry of a plugin set.
    /// </summary>
    public static object Inner(object entry)
    {
        return entry switch
        {
            PluginRewriter rewriter => rewriter.Rewriter,
            PluginSourceRule rule => rule.Rule,
            PluginListener listener => listener.Listener,
            PluginBlockWriter writer => writer.Writer,
            _ => throw new ArgumentException("Not an entry of a plugin set.", nameof(entry)),
        };
    }

    /// <summary>
    /// The names of the classes of the plugins behind the entries of a plugin set, in the order of the set.
    /// </summary>
    public static List<string> ClassNames(IEnumerable<object> entries)
    {
        var names = new List<string>();
        foreach (object entry in entries)
        {
            names.Add(Inner(entry).GetType().Name);
        }

        return names;
    }
}
