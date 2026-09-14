using System.Text;
using System.Text.RegularExpressions;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Compilers;
using Ncx.Core.Machine;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx compile &lt;file.ncx&gt; --machine &lt;toml&gt; [--output &lt;folder&gt;] [--strict]: the compiler that the
/// controller of the machine file chooses, the files under out/&lt;machine&gt;/ or in the folder of --output, the tool
/// table of D10, the diagnostics on the standard error in the form of D98 and the exit codes of D97 (architecture 8,
/// 10; D77). The runs go through CompileCommand.Run with a working directory of their own and the repository as the
/// tool's own folder, so that no test depends on the working directory of the test process (code-guidelines 8).
/// </summary>
public sealed partial class CompileCommandTests : IDisposable
{
    // A file that parses and checks.
    private const string Program = "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"T\"\nUNITS=MM\nPROGRAM=END\nFILE=END\n";

    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());

    // What the last run wrote to the standard error: the diagnostics (D98).
    private string _error = "";

    public void Dispose()
    {
        _project.Dispose();
    }

    // D77, D97, D103, architecture 10: without --machine and without a machine in ncx.toml compile does not run
    // against the built-in default machine; the missing machine file decides exit code 2 before the run starts.
    [Fact]
    public void Compile_WithoutMachineOptionAndWithoutNcxToml_ExitsTwoWithCli400()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, null, Registry(new RecordingCompiler(Controller.Fanuc)));

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI400: ", _error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_project.WorkingDirectory, "out")));
    }

    // Architecture 8, machine-config 1: the controller of the machine file chooses the compiler; ncx has none for
    // Fanuc yet (P3-06), which is an ERROR on the file, and nothing is written; the inputs were read, so the exit code
    // is 1 (D97).
    [Fact]
    public void Compile_MachineOfAControllerWithoutCompiler_ExitsOneWithCli401()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, "fanuc-mill-30i", Ncx.Cli.Program.Compilers());

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{file}(1): ERROR CLI401: ", _error, StringComparison.Ordinal);
    }

    // Architecture 10, machine-config 10: the files go into out/<machine>/ of the working directory, named after the
    // machine file.
    [Fact]
    public void Compile_FilesOfTheCompiler_GoIntoTheOutFolderOfTheMachine()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);
        var compiler = new RecordingCompiler(Controller.Fanuc);

        int exitCode = RunCompile(file, "fanuc-mill-30i", Registry(compiler));

        Assert.True(exitCode == 0, _error);
        string written = Path.Combine(_project.WorkingDirectory, "out", "fanuc-mill-30i", "T.nc");
        Assert.Equal(Encoding.UTF8.GetBytes(RecordingCompiler.Text), File.ReadAllBytes(written));
        Assert.Equal(Controller.Fanuc, compiler.Machine?.Machine.Controller);
    }

    // Architecture 10 and the TODO(question) of CompileCommand.Run: --output names the folder the files go into.
    [Fact]
    public void Compile_OutputOption_WritesIntoThatFolder()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, "fanuc-mill-30i", Registry(new RecordingCompiler(Controller.Fanuc)), "nc");

        Assert.True(exitCode == 0, _error);
        Assert.True(File.Exists(Path.Combine(_project.WorkingDirectory, "nc", "T.nc")));
        Assert.False(Directory.Exists(Path.Combine(_project.WorkingDirectory, "out")));
    }

    // Architecture 10, machine-config 10: ncx.toml names the machine that --machine defaults to and the output folder.
    [Fact]
    public void Compile_MachineAndOutFolderOfNcxToml_AreThoseOfTheRun()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machine = \"fanuc-mill-30i\"\nout = \"build\"\n");
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, null, Registry(new RecordingCompiler(Controller.Fanuc)));

        Assert.True(exitCode == 0, _error);
        Assert.True(File.Exists(Path.Combine(_project.WorkingDirectory, "build", "fanuc-mill-30i", "T.nc")));
    }

    // Machine-config 1, D10: a machine without tool_table gives the compiler no tool table and no expected file.
    [Fact]
    public void Compile_MachineWithoutToolTable_GivesNoToolTable()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);
        var compiler = new RecordingCompiler(Controller.Fanuc);

        RunCompile(file, "fanuc-mill-30i", Registry(compiler));

        Assert.Null(compiler.Options?.ToolTable);
        Assert.Null(compiler.Options?.ToolTableFile);
    }

    // D10: a tool table that the machine names and that is not there is no error; the compiler gets its name as the
    // expected file of the warning block.
    [Fact]
    public void Compile_ToolTableNamedButMissing_GivesTheExpectedFile()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithToolTable);
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);
        var compiler = new RecordingCompiler(Controller.Fanuc);

        int exitCode = RunCompile(file, "mill", Registry(compiler));

        Assert.True(exitCode == 0, _error);
        Assert.Null(compiler.Options?.ToolTable);
        Assert.Equal(Path.Combine("machines", "mill.tools.toml"), compiler.Options?.ToolTableFile);
    }

    // Machine-config 1, D10: the tool table next to the machine file is loaded and handed to the compiler.
    [Fact]
    public void Compile_ToolTableNextToTheMachineFile_IsLoaded()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithToolTable);
        _project.WriteInWorkingDirectory("machines/mill.tools.toml", "[[tool]]\nnumber = 1\nkind = \"TURNING\"\n");
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);
        var compiler = new RecordingCompiler(Controller.Fanuc);

        int exitCode = RunCompile(file, "mill", Registry(compiler));

        Assert.True(exitCode == 0, _error);
        Assert.Equal("TURNING", compiler.Options?.ToolTable?.Find(new Ncx.Core.Model.ToolRef(1))?.Kind);
    }

    // D97: a tool table that is there and cannot be read is an input that cannot be read, exit code 2.
    [Fact]
    public void Compile_ToolTableThatIsNoUtf8_ExitsTwoWithCli402()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithToolTable);
        _project.WriteBytesInWorkingDirectory("machines/mill.tools.toml", Encoding.Latin1.GetBytes("# Ä\n"));
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, "mill", Registry(new RecordingCompiler(Controller.Fanuc)));

        Assert.Equal(2, exitCode);
        Assert.Equal(["CLI402"], Codes(_error));
    }

    // Virtual machine 2.9, code-guidelines 6: a compile that an ERROR stopped writes no file, and the exit code is 1.
    [Fact]
    public void Compile_ErrorOfTheCompiler_WritesNoFileAndExitsOne()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);

        int exitCode = RunCompile(file, "fanuc-mill-30i", Registry(new RecordingCompiler(Controller.Fanuc, true)));

        Assert.Equal(1, exitCode);
        Assert.Equal(["CMP010"], Codes(_error));
        Assert.False(Directory.Exists(Path.Combine(_project.WorkingDirectory, "out")));
    }

    // D97, architecture 10: an output file that cannot be written is an ERROR of the run, which has started, so the
    // exit code is 1.
    [Fact]
    public void Compile_FolderThatIsAFile_ExitsOneWithCli004()
    {
        string file = _project.WriteInWorkingDirectory("part.ncx", Program);
        _project.WriteInWorkingDirectory("taken", "a file, not a folder");

        int exitCode = RunCompile(file, "fanuc-mill-30i", Registry(new RecordingCompiler(Controller.Fanuc)), "taken");

        Assert.Equal(1, exitCode);
        Assert.Equal(["CLI004"], Codes(_error));
    }

    // Architecture 10: compile is a command of ncx, and --machine is required unless ncx.toml names the machine.
    [Fact]
    public void ProgramRun_CompileWithoutMachine_ExitsTwoWithCli400()
    {
        using var harness = new CliHarness();
        string file = harness.WriteFile("part.ncx", Program);

        int exitCode = harness.Run("compile", file);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI400: ", harness.Error, StringComparison.Ordinal);
    }

    // The clutch mill of the CLI tests with a tool table next to it (machine-config 1, D10).
    private static string MillWithToolTable => TestMachines.ClutchMill.Replace(
        "channels = [1]", "channels = [1]\ntool_table = \"mill.tools.toml\"", StringComparison.Ordinal);

    private int RunCompile(string file, string? machine, CompilerRegistry compilers, string? output = null)
    {
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = file,
            MachineFile = machine,
            WorkingDirectory = _project.WorkingDirectory,
            ToolFolder = _project.ToolFolder,
        };

        int exitCode = CompileCommand.Run(settings, output, compilers, error);
        _error = error.ToString();
        return exitCode;
    }

    private static CompilerRegistry Registry(RecordingCompiler compiler)
    {
        var compilers = new CompilerRegistry();
        compilers.Register(compiler.Controller, () => compiler);
        return compilers;
    }

    private static List<string> Codes(string diagnostics)
    {
        var codes = new List<string>();
        foreach (Match match in DiagnosticCode().Matches(diagnostics))
        {
            codes.Add(match.Groups[1].Value);
        }

        return codes;
    }

    [GeneratedRegex(@": (?:ERROR|WARNING|INFO) ([A-Z]+[0-9]{3}): ")]
    private static partial Regex DiagnosticCode();
}
