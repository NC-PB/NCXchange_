using System.Runtime.Loader;
using Ncx.Acceptance.Cli;
using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The plugin loader: one AssemblyLoadContext per DLL from plugins/ and the [plugins] list of ncx.toml, which shares
/// every Ncx.* assembly with the host and isolates the rest, the classes of the four interfaces found in load order,
/// the settings of D80 in the context, and a plugin that fails to load reported with its name and dropped for the run
/// (architecture 9; D80, D106; implementation 17, P7-01). Each test works in a temporary folder of its own.
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-plugins-");
    private readonly Diagnostics _diagnostics = new("part.ncx");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // Architecture 9, D106: each plugin DLL is loaded into an AssemblyLoadContext of its own, named after the plugin,
    // never into the context of the host.
    [Fact]
    public void PluginLoader_TwoPlugins_LoadEachIntoAContextOfItsOwn()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules,
            TestPlugins.FaultyRules);

        AssemblyLoadContext? shop = ContextOf(plugins.Rewriters[0]);
        AssemblyLoadContext? faulty = ContextOf(plugins.Rewriters[3]);
        Assert.NotNull(shop);
        Assert.NotNull(faulty);
        Assert.NotSame(AssemblyLoadContext.Default, shop);
        Assert.NotSame(shop, faulty);
        Assert.Equal(TestPlugins.ShopRules, shop.Name);
        Assert.Equal(TestPlugins.FaultyRules, faulty.Name);
        Assert.Empty(_diagnostics.Items);
    }

    // D106: the context resolves every Ncx.* assembly from the host, so that the interface a plugin implements is the
    // host's own type, even when the plugin brings a copy of Ncx.Core with it.
    [Fact]
    public void PluginLoader_NcxAssemblies_AreTheHostsEvenWithACopyNextToThePlugin()
    {
        File.Copy(typeof(Block).Assembly.Location, Path.Combine(_folder.FullName, "Ncx.Core.dll"));

        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules);

        object rule = TestPlugins.Inner(plugins.Rewriters[1]);
        Assert.Equal("CoolantClutchRule", rule.GetType().Name);
        Assert.Contains(typeof(IProgramRewriter), rule.GetType().GetInterfaces());
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(typeof(IProgramRewriter).Assembly));
    }

    // Implementation 17, P7-01: the loader finds the public classes of the four interfaces, a plugin's classes in the
    // order of their names.
    [Fact]
    public void PluginLoader_ShopRules_FindsTheClassesOfTheFourInterfaces()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.ShopRules);

        Assert.Equal(["BeforeToolChange", "CoolantClutchRule"], TestPlugins.ClassNames(plugins.Rewriters));
        Assert.Equal(["ShopCoolantCode"], TestPlugins.ClassNames(plugins.SourceRules));
        Assert.Equal(["EventRecorder"], TestPlugins.ClassNames(plugins.Listeners));
        Assert.Equal(["ZOnItsOwnLine"], TestPlugins.ClassNames(plugins.BlockWriters));
        Assert.Empty(_diagnostics.Items);
    }

    // Implementation 17, P7-01: the plugins come in load order, the one loaded first first.
    [Fact]
    public void PluginLoader_TwoPlugins_GiveTheirRewritersInLoadOrder()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, TestPlugins.FaultyRules,
            TestPlugins.ShopRules);

        Assert.Equal(["BrokenTextRule", "ThrowingRule", "BeforeToolChange", "CoolantClutchRule"],
            TestPlugins.ClassNames(plugins.Rewriters));
    }

    // Implementation 17, P7-01: a DLL that does not load is reported with the plugin's name as a PLG ERROR on the
    // file, and dropped for the run; the plugins after it load.
    [Fact]
    public void PluginLoader_FileThatIsNoAssembly_IsReportedByNameAndTheOthersLoad()
    {
        File.WriteAllText(Path.Combine(_folder.FullName, "Broken.dll"), "not an assembly");
        TestPlugins.CopyInto(_folder.FullName, TestPlugins.ShopRules);

        PluginSet plugins = PluginLoader.Load(_folder.FullName, ["Broken.dll", "ShopRules.dll"],
            TestPlugins.NoSettings, new PluginDiagnostics(_diagnostics));

        Diagnostic broken = Assert.Single(_diagnostics.Items);
        Assert.Equal(Severity.Error, broken.Severity);
        Assert.Equal("PLG002", broken.Code);
        Assert.Equal("Broken.dll", broken.File);
        Assert.Equal(1, broken.Line);
        Assert.StartsWith("plugin Broken: ", broken.Message, StringComparison.Ordinal);
        Assert.Equal(2, plugins.Rewriters.Count);
    }

    // Implementation 17, P7-01: an assembly the [plugins] list names that is not there fails to load like any other.
    [Fact]
    public void PluginLoader_ListedFileThatIsNotThere_IsReportedByName()
    {
        PluginSet plugins = PluginLoader.Load(_folder.FullName, ["Missing.dll"], TestPlugins.NoSettings,
            new PluginDiagnostics(_diagnostics));

        Diagnostic missing = Assert.Single(_diagnostics.Items);
        Assert.Equal("PLG002", missing.Code);
        Assert.StartsWith("plugin Missing: ", missing.Message, StringComparison.Ordinal);
        Assert.Empty(plugins.Rewriters);
    }

    // The TODO(question) of PluginLoader: an assembly without a public class of the four interfaces is a WARNING and
    // gives nothing.
    [Fact]
    public void PluginLoader_AssemblyWithoutPluginClasses_WarnsAndGivesNothing()
    {
        File.Copy(typeof(FactAttribute).Assembly.Location, Path.Combine(_folder.FullName, "NoRules.dll"));

        PluginSet plugins = PluginLoader.Load(_folder.FullName, ["NoRules.dll"], TestPlugins.NoSettings,
            new PluginDiagnostics(_diagnostics));

        Diagnostic warning = Assert.Single(_diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal("PLG005", warning.Code);
        Assert.StartsWith("plugin NoRules: ", warning.Message, StringComparison.Ordinal);
        Assert.Empty(plugins.Rewriters);
        Assert.Empty(plugins.SourceRules);
        Assert.Empty(plugins.Listeners);
        Assert.Empty(plugins.BlockWriters);
    }

    // Implementation 17, P7-01 and the TODO(question) of PluginLoader.Files: the assemblies the [plugins] list of
    // ncx.toml names come first, in their order, then the DLLs of plugins/ in the order of their names; a name of the
    // list that is no file of the working directory is looked for in plugins/, and every file comes once.
    [Fact]
    public void Files_ListAndPluginsFolder_GiveTheListFirstThenTheFolderEachFileOnce()
    {
        WriteFile(Path.Combine("plugins", "B.dll"));
        WriteFile(Path.Combine("plugins", "A.dll"));
        WriteFile(Path.Combine("lib", "C.dll"));

        IReadOnlyList<string> files = PluginLoader.Files(_folder.FullName, ["lib/C.dll", "B.dll"]);

        Assert.Equal(["lib/C.dll", Path.Combine("plugins", "B.dll"), Path.Combine("plugins", "A.dll")], files);
    }

    // Implementation 17, P7-01: without a plugins/ folder and without a list there is no plugin to load.
    [Fact]
    public void Files_NoListAndNoPluginsFolder_AreNone()
    {
        Assert.Empty(PluginLoader.Files(_folder.FullName, []));
    }

    // D80: a plugin reads its own [plugins.<name>] section of ncx.toml through the context of its rewriters.
    [Fact]
    public void PluginRewriteContext_SettingsOfTheSectionOfThePlugin_ArriveInItsRewriter()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, Settings("ShopRules"),
            TestPlugins.ShopRules);

        NcxProgram expanded = PluginMachines.Expand(CliHarness.OneProgram("UNITS=MM", "TOOL=1"),
            PluginMachines.PlainMill, plugins.Rewriters);

        Assert.Equal(CliHarness.OneProgram("UNITS=MM", "COOLANT=OFF", "TOOL=1"),
            PluginMachines.WriteWithGenerated(expanded));
        Assert.Equal(TestPlugins.ShopRules, PluginMachines.Find(expanded, "COOLANT=OFF").Generated?.Source);
    }

    // D80: nothing but its own section is visible to a plugin; the section of another plugin does not arrive.
    [Fact]
    public void PluginRewriteContext_SectionOfAnotherPlugin_DoesNotArrive()
    {
        PluginSet plugins = TestPlugins.Load(_folder.FullName, _diagnostics, Settings("FaultyRules"),
            TestPlugins.ShopRules);
        string program = CliHarness.OneProgram("UNITS=MM", "TOOL=1");

        NcxProgram expanded = PluginMachines.Expand(program, PluginMachines.PlainMill, plugins.Rewriters);

        Assert.Equal(program, PluginMachines.WriteWithGenerated(expanded));
    }

    // The section of a plugin with the setting before_tool of ShopRules.
    private static Dictionary<string, IReadOnlyDictionary<string, string>> Settings(string plugin)
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [plugin] = new Dictionary<string, string> { ["before_tool"] = "COOLANT=OFF" },
        };
    }

    private static AssemblyLoadContext? ContextOf(object entry)
    {
        return AssemblyLoadContext.GetLoadContext(TestPlugins.Inner(entry).GetType().Assembly);
    }

    private void WriteFile(string relativePath)
    {
        string path = Path.Combine(_folder.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _folder.FullName);
        File.WriteAllText(path, "");
    }
}
