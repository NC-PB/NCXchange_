using Ncx.Core.Model;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// ncx plugin check &lt;dll&gt; loads a plugin in a context of its own, as a run loads it, and lists the interfaces it
/// implements and the version of the Ncx assemblies it was built against (code-guidelines 11; implementation 17,
/// P7-02, and its risks). The plugins are the test plugins of P7-01, copied into plugins/ of the working directory.
/// </summary>
public sealed class PluginCheckTests : IDisposable
{
    private readonly PluginWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    // Code-guidelines 11: the listing names the plugin, the Ncx assemblies it was built against with their version,
    // and each interface it implements with the number of its classes, in the order of architecture 9.
    [Fact]
    public void PluginCheck_ShopRules_ListsItsFourInterfacesInTheOrderOfArchitecture9()
    {
        TestPlugins.CopyInto(_workspace.PathOf("plugins"), TestPlugins.ShopRules);
        string dll = Path.Combine("plugins", "ShopRules.dll");

        int exitCode = _workspace.Check(dll);

        Assert.True(exitCode == 0, _workspace.Error);
        string version = typeof(Block).Assembly.GetName().Version?.ToString() ?? "";
        Assert.Equal(
            $"""
            {dll}: plugin ShopRules
              built against Ncx.Compilers {version}, Ncx.Core {version}, Ncx.Readers {version}
              ISourceRule: 1 class
              IProgramRewriter: 2 classes
              IVmListener: 1 class
              IBlockWriter: 1 class

            """,
            _workspace.Output);
        Assert.Empty(_workspace.Error);
    }

    // Implementation 17, P7-01: a DLL that does not load is reported by the loader with the plugin's name, and check
    // lists nothing of it.
    [Fact]
    public void PluginCheck_FileThatIsNoAssembly_IsReportedByNameAndListsNothing()
    {
        string dll = Path.Combine("plugins", "Broken.dll");
        _workspace.Write(dll, "not an assembly");

        int exitCode = _workspace.Check(dll);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{dll}(1): ERROR PLG002: plugin Broken: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Empty(_workspace.Output);
    }

    // D97: the DLL is the input of check, and one that is not there decides exit code 2 before the run starts.
    [Fact]
    public void PluginCheck_FileThatIsNotThere_IsAnInputThatCannotBeRead()
    {
        string dll = Path.Combine("plugins", "Missing.dll");

        int exitCode = _workspace.Check(dll);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"{dll}(1): ERROR CLI002: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Empty(_workspace.Output);
    }

    // The TODO(question) of PluginLoader: an assembly without a class of the four interfaces is a WARNING; check names
    // it and lists no interface, and --strict makes the WARNING exit code 1 (D97).
    [Fact]
    public void PluginCheck_AssemblyWithoutPluginClass_WarnsAndListsNoInterface()
    {
        string dll = Path.Combine("plugins", "NoRules.dll");
        Directory.CreateDirectory(_workspace.PathOf("plugins"));
        File.Copy(typeof(FactAttribute).Assembly.Location, _workspace.PathOf(dll));

        int exitCode = _workspace.Check(dll);
        int strictExitCode = _workspace.Check(dll, strict: true);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.Equal($"{dll}: plugin NoRules\n", _workspace.Output);
        Assert.StartsWith($"{dll}(1): WARNING PLG005: plugin NoRules: ", _workspace.Error, StringComparison.Ordinal);
    }
}
