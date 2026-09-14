using Ncx.Acceptance.Cli;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Compilers;
using Ncx.Core.Machine;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The "Done when" of P7-02 (M10): the template builds and runs unchanged after ncx plugin new, ncx plugin check lists
/// it as an IProgramRewriter, ncx compile of a program with COOLANT:THROUGH=ON on line 12 says what the plugin
/// inserted there, and ncx plugin test runs its test. These tests run dotnet build and dotnet test of the .NET SDK.
/// </summary>
public sealed class TemplateTests : IClassFixture<BuiltTemplate>
{
    // The DLL of the plugin in plugins/ of the working directory, as the user finds it.
    private static readonly string s_dll = Path.Combine("plugins", BuiltTemplate.Name + ".dll");

    private readonly BuiltTemplate _template;

    public TemplateTests(BuiltTemplate template)
    {
        _template = template;
    }

    // Code-guidelines 11, steps 2 to 5: the template builds unchanged; the command puts the DLL into plugins/ and adds
    // the line of machine-config 10 to ncx.toml, and names both.
    [Fact]
    public void PluginBuild_TemplateAfterNew_BuildsUnchangedIntoPluginsAndAddsTheLine()
    {
        Assert.True(_template.NewExitCode == 0, _template.NewError);
        Assert.True(_template.BuildExitCode == 0, _template.BuildError + _template.BuildOutput);
        Assert.True(File.Exists(_template.Workspace.PathOf(s_dll)));
        Assert.Equal("plugins = [\"CoolantClutch.dll\"]\n",
            File.ReadAllText(_template.Workspace.PathOf("ncx.toml")));
        Assert.Equal($"{s_dll}\nncx.toml: plugins = [\"CoolantClutch.dll\"]\n", _template.BuildOutput);
        Assert.Empty(_template.BuildError);
    }

    // Implementation 17, P7-02: ncx plugin check lists the DLL that ncx plugin new and ncx plugin build made as an
    // IProgramRewriter, the one interface of the template; ZOnItsOwnLine.cs is commented out.
    [Fact]
    public void PluginCheck_TemplateAfterNewAndBuild_ListsIProgramRewriter()
    {
        int exitCode = _template.Workspace.Check(s_dll);

        Assert.True(exitCode == 0, _template.Workspace.Error);
        string[] lines = _template.Workspace.Output.Split('\n');
        Assert.Equal($"{s_dll}: plugin CoolantClutch", lines[0]);
        Assert.StartsWith("  built against Ncx.Core ", lines[1], StringComparison.Ordinal);
        Assert.Equal(["  IProgramRewriter: 1 class", ""], lines[2..]);
    }

    // Code-guidelines 11, step 6; D98; M10: ncx compile of a program with COOLANT:THROUGH=ON on line 12 prints the
    // INFO of the plugin with its name, the blocks it inserted and the line.
    // TODO(question): D98, code-guidelines 11 and P7-02 print "inserted 2 blocks at line 12"; the rule of the template
    // inserts three blocks, and PluginRewriter counts every one (the TODO(question) of P7-01 there).
    // TODO: the compile runs with the compiler of the tests, EchoCompiler, until a compiler of a controller family is
    // registered in Program.Compilers() (P3-04, P3-06).
    [Fact]
    public void Compile_ClutchPluginOfTheTemplate_SaysWhatItInsertedAtLine12()
    {
        PluginWorkspace workspace = _template.Workspace;
        workspace.Write(Path.Combine("machines", "plugin-mill.toml"), PluginMachines.PlainMill);
        string file = workspace.Write("part.ncx", CliHarness.OneProgram(
            "UNITS=MM",
            "SPINDLE:MAIN=CW RPM:MAIN=1500",
            "RAPID X=0 Y=0 Z=50",
            "RAPID Z=5",
            "LINE Z=-2 F=100",
            "LINE X=40",
            "LINE Y=30",
            "LINE X=0",
            "RAPID Z=50",
            "COOLANT:THROUGH=ON",
            "RAPID X=0 Y=0"));
        var compilers = new CompilerRegistry();
        compilers.Register(Controller.Fanuc, () => new EchoCompiler());
        var settings = new RunSettings
        {
            File = file,
            MachineFile = "plugin-mill",
            WorkingDirectory = workspace.WorkingDirectory,
            ToolFolder = workspace.ToolFolder,
        };

        using var error = new StringWriter();
        int exitCode = CompileCommand.Run(settings, null, compilers, error);

        Assert.True(exitCode == 0, error.ToString());
        Assert.Equal($"{file}(12): INFO PLG001: plugin CoolantClutch: inserted 3 blocks at line 12\n",
            error.ToString());
    }

    // Code-guidelines 11, step 7: ncx plugin test runs the one test of the template, which passes unchanged.
    [Fact]
    public void PluginTest_TemplateAfterNew_RunsItsTestAndItPasses()
    {
        int exitCode = _template.Workspace.Test(null);

        Assert.True(exitCode == 0, _template.Workspace.Error + _template.Workspace.Output);
        Assert.Contains("Passed!", _template.Workspace.Output, StringComparison.Ordinal);
        Assert.Empty(_template.Workspace.Error);
    }
}
