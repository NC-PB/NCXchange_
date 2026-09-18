using System.Text;
using System.Text.RegularExpressions;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Core.Machine;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx convert &lt;file&gt; --machine &lt;toml&gt; [--output &lt;file&gt;] [--strict]: the reader that the controller
/// of the machine file chooses, the canonical writer and a STATIC check over the produced program whose diagnostics are
/// reported together with the reader's, on the standard error in the form of D98, with the exit codes of D97
/// (architecture 7, 10; D77; phase 3, P3-02). The runs go through ConvertCommand.Run with a working directory of their
/// own and the repository as the tool's own folder, whose machines/ and cycles/ hold the shipped files, so that no test
/// depends on the working directory of the test process (code-guidelines 8).
/// </summary>
public sealed partial class ConvertCommandTests : IDisposable
{
    // A Fanuc program that reads and checks without a diagnostic.
    private const string CleanProgram =
        "%\nO0004\nG90 G21 G17 G94\nT1 M6\nS1000 M3\nG0 X0. Y0. Z5.\nG1 X10. F100.\nM30\n%\n";

    // The M6 of line 4 changes to the preloaded tool and nothing is preloaded, the ERROR RDR101 of the Fanuc reader
    // (controller-mapping 3).
    private const string ChangeWithoutPreload = "%\nO0001\nG90 G21 G17\nM6\nG0 X0. Y0.\nG1 X10. F100.\nM30\n%\n";

    // The G1 of line 6 cuts while the spindle is off, the WARNING VM500 of the STATIC check (virtual machine 5).
    private const string CutWithTheSpindleOff =
        "%\nO0002\nG90 G21 G17 G94\nT1 M6\nG0 X0. Y0. Z5.\nG1 X10. F100.\nM30\n%\n";

    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());

    // What the last run wrote to the standard output, the NCX text, and to the standard error, the diagnostics (D98).
    private string _output = "";
    private string _error = "";

    public void Dispose()
    {
        _project.Dispose();
    }

    // D77, D97, D103, architecture 10: without --machine and without a machine in ncx.toml convert does not run against
    // the built-in default machine; the missing machine file decides exit code 2 before the run starts, and it is
    // reported as a mistake of the command line (wave-1 question #79).
    [Fact]
    public void Convert_WithoutMachineOptionAndWithoutNcxToml_ExitsTwoWithCli250()
    {
        string file = _project.WriteInWorkingDirectory("part.nc", CleanProgram);

        int exitCode = RunConvert(file, null);

        Assert.Equal(2, exitCode);
        Assert.Equal("", _output);
        Assert.StartsWith("ncx(1): ERROR CLI250: ", _error, StringComparison.Ordinal);
        Assert.Single(Lines(_error));
    }

    // D77, architecture 10: convert needs a machine file from --machine or from ncx.toml, so the machine that ncx.toml
    // names is the machine of a run without --machine.
    [Fact]
    public void Convert_MachineOfNcxToml_IsTheMachineOfARunWithoutMachineOption()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machine = \"fanuc-mill-30i\"\n");
        string file = _project.WriteInWorkingDirectory("part.nc", CleanProgram);

        int exitCode = RunConvert(file, null);

        Assert.True(exitCode == 0, _error);
        Assert.Equal("", _error);
        Assert.StartsWith("FILE=BEGIN NCX=1\n", _output, StringComparison.Ordinal);
    }

    // Architecture 7, machine-config 1: the controller of the machine file chooses the reader; a registry without a
    // reader for Siemens makes that an ERROR on the program, and nothing is written; the inputs were read, so the exit
    // code is 1 (D97).
    [Fact]
    public void Convert_MachineOfAControllerWithoutReader_ExitsOneWithCli251()
    {
        string file = _project.WriteInWorkingDirectory("part.mpf", "G0 X0 Y0\nM30\n");
        var readers = new ReaderRegistry();
        readers.Register(Controller.Fanuc, () => new FanucReader());

        int exitCode = RunConvert(file, "siemens-840dsl-mill", readers: readers);

        Assert.Equal(1, exitCode);
        Assert.Equal("", _output);
        Assert.StartsWith($"{file}(1): ERROR CLI251: ", _error, StringComparison.Ordinal);
    }

    // D97, architecture 10: the program and the machine file are read before the run starts, both are reported, and one
    // that cannot be found decides exit code 2.
    [Fact]
    public void Convert_MissingProgramAndUnknownMachine_ReportsBothAndExitsTwo()
    {
        string file = Path.Combine(_project.WorkingDirectory, "missing.nc");

        int exitCode = RunConvert(file, "nowhere");

        Assert.Equal(2, exitCode);
        Assert.Equal("", _output);
        Assert.Equal(["CLI002", "CLI200"], Codes(_error));
    }

    // D229, as recommended: a controller program whose bytes are no UTF-8 is read as Windows-1252 with the WARNING
    // CLI352 on its first line, and the NCX text carries its umlauts as UTF-8 (language 3, Encoding).
    [Fact]
    public void Convert_ProgramThatIsNoUtf8_IsReadAsWindows1252WithAWarning()
    {
        string file = _project.WriteBytesInWorkingDirectory("latin1.nc",
            Encoding.Latin1.GetBytes("%\nO0001 (ÄNDERUNG)\nM30\n%\n"));

        int exitCode = RunConvert(file, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _error);
        Assert.StartsWith($"{file}(1): WARNING CLI352: ", _error, StringComparison.Ordinal);
        Assert.Contains("NAME=\"ÄNDERUNG\"", _output, StringComparison.Ordinal);
    }

    // D229: a program that is UTF-8 text is read as UTF-8, its byte order mark left out, without a WARNING.
    [Fact]
    public void Convert_ProgramThatIsUtf8WithAByteOrderMark_IsReadWithoutAWarning()
    {
        string file = _project.WriteBytesInWorkingDirectory("utf8.nc",
            [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes("%\nO0001 (ÄNDERUNG)\nM30\n%\n")]);

        int exitCode = RunConvert(file, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _error);
        Assert.DoesNotContain("CLI352", _error, StringComparison.Ordinal);
        Assert.StartsWith("FILE=BEGIN NCX=1\n", _output, StringComparison.Ordinal);
        Assert.Contains("NAME=\"ÄNDERUNG\"", _output, StringComparison.Ordinal);
    }

    // D97, machine-config 1: a machine file that reads but loads with an ERROR stops the run before it starts, with
    // exit code 1, and nothing is written.
    [Fact]
    public void Convert_MachineFileWithAnError_ExitsOneAndWritesNothing()
    {
        string machine = _project.WriteInWorkingDirectory("broken.toml", TestMachines.WithoutController);
        string file = _project.WriteInWorkingDirectory("part.nc", CleanProgram);

        int exitCode = RunConvert(file, machine);

        Assert.Equal(1, exitCode);
        Assert.Equal("", _output);
        Assert.Contains($"{machine}(", _error, StringComparison.Ordinal);
        Assert.Contains(": ERROR CFG", _error, StringComparison.Ordinal);
    }

    // Architecture 10: --output writes the NCX text into its file as UTF-8 without a byte order mark, the same text the
    // standard output gets without it, and nothing to the standard output.
    [Fact]
    public void ConvertOutput_WritesTheTextIntoTheFileAndNothingToTheStandardOutput()
    {
        string file = _project.WriteInWorkingDirectory("part.nc", CleanProgram);
        string outputFile = Path.Combine(_project.WorkingDirectory, "part.ncx");
        RunConvert(file, "fanuc-mill-30i");
        string written = _output;

        int exitCode = RunConvert(file, "fanuc-mill-30i", outputFile);

        Assert.True(exitCode == 0, _error);
        Assert.Equal("", _output);
        Assert.Equal(Encoding.UTF8.GetBytes(written), File.ReadAllBytes(outputFile));
    }

    // D97, architecture 10: an output file that cannot be written is an ERROR of the run, which has started, so the
    // exit code is 1.
    [Fact]
    public void ConvertOutput_FileThatCannotBeWritten_ExitsOneWithCli004()
    {
        string file = _project.WriteInWorkingDirectory("part.nc", CleanProgram);
        string outputFile = Path.Combine(_project.WorkingDirectory, "missing", "part.ncx");

        int exitCode = RunConvert(file, "fanuc-mill-30i", outputFile);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{outputFile}(1): ERROR CLI004: ", _error, StringComparison.Ordinal);
    }

    // D5, virtual machine 2.9 (and the TODO(question) D205 of ConvertCommand.Run on an ERROR): an ERROR of the reader
    // is reported, the whole program the reader produced is written with the bare TOOL of the M6, and the check does
    // not run, as an ERROR of the parser stops a run before its first block; the exit code is 1 (D97).
    [Fact]
    public void Convert_ErrorOfTheReader_WritesTheTextAndStopsTheCheck()
    {
        string file = _project.WriteInWorkingDirectory("change.nc", ChangeWithoutPreload);

        int exitCode = RunConvert(file, "fanuc-mill-30i");

        Assert.Equal(1, exitCode);
        Assert.Equal(["RDR101"], Codes(_error));
        Assert.StartsWith($"{file}(4): ERROR RDR101: ", _error, StringComparison.Ordinal);
        Assert.Contains("\nTOOL\n", _output, StringComparison.Ordinal);
        Assert.EndsWith("PROGRAM=END\nFILE=END\n", _output, StringComparison.Ordinal);
    }

    // Architecture 7 (Begin(sourceLine)), code-guidelines 6: the check runs over the blocks the reader produced, which
    // carry the lines of their source blocks, so what it finds cites the controller program: the G1 of line 6.
    [Fact]
    public void Convert_DiagnosticOfTheCheck_CitesTheLineOfTheSourceBlock()
    {
        string file = _project.WriteInWorkingDirectory("spindle.nc", CutWithTheSpindleOff);

        int exitCode = RunConvert(file, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _error);
        Assert.StartsWith($"{file}(6): WARNING VM500: ", _error, StringComparison.Ordinal);
        Assert.Single(Lines(_error));
    }

    // Architecture 7, D98: the diagnostics of the check are reported together with the reader's, the reader's first:
    // 2.5D_FRAESEN.h reports the missing M30 and the two BLK FORM blocks it keeps as RAW, then the check the two RAW
    // blocks it ran (D5; language 4.1; virtual machine 5).
    [Fact]
    public void Convert_DiagnosticsOfTheCheck_FollowThoseOfTheReader()
    {
        string file = CopyExample("sources/2.5D_FRAESEN.h");

        RunConvert(file, "heidenhain-itnc530");

        Assert.Equal(["RDR010", "RDR001", "RDR001", "VM400", "VM400"], Codes(_error));
    }

    // D97, architecture 10: under --strict, which every command accepts, the WARNINGs of 2.5D_FRAESEN.h exit 1; they
    // exit 0 without it, and the text is written either way.
    [Fact]
    public void ConvertStrict_ProgramWithWarnings_ExitsOne()
    {
        string file = CopyExample("sources/2.5D_FRAESEN.h");

        int strictExitCode = RunConvert(file, "heidenhain-itnc530", strict: true);
        string strictOutput = _output;
        int exitCode = RunConvert(file, "heidenhain-itnc530");

        Assert.Equal(1, strictExitCode);
        Assert.Equal(0, exitCode);
        Assert.Equal(_output, strictOutput);
    }

    // Wave-1 question #80: a byte order mark is no part of the program the reader reads, so a program converts the same
    // with it and without it.
    [Fact]
    public void Convert_ProgramWithAByteOrderMark_ConvertsAsWithoutIt()
    {
        string plain = _project.WriteInWorkingDirectory("plain.nc", CleanProgram);
        string marked = _project.WriteBytesInWorkingDirectory("marked.nc",
            [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(CleanProgram)]);
        RunConvert(plain, "fanuc-mill-30i");
        string expected = _output;

        int exitCode = RunConvert(marked, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _error);
        Assert.Equal(expected, _output);
    }

    // D97, architecture 10: convert without its file is a usage error, which decides exit code 2 before the run starts.
    [Fact]
    public void CommandLine_ConvertWithoutFile_IsAUsageErrorExitingTwo()
    {
        using var cli = new CliHarness();

        int exitCode = cli.Run("convert", "--machine", "fanuc-mill-30i");

        Assert.Equal(2, exitCode);
        Assert.Equal("", cli.Output);
        Assert.Contains("ERROR CLI001", cli.Error, StringComparison.Ordinal);
    }

    // ncx convert in the working directory and with the tool folder of the harness; the outputs are kept in _output and
    // _error.
    private int RunConvert(string file, string? machine, string? outputFile = null, bool strict = false,
        ReaderRegistry? readers = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = file,
            MachineFile = machine,
            Strict = strict,
            WorkingDirectory = _project.WorkingDirectory,
            ToolFolder = _project.ToolFolder,
        };

        int exitCode = ConvertCommand.Run(settings, outputFile, readers ?? Program.Readers(), output, error);
        _output = output.ToString();
        _error = error.ToString();
        return exitCode;
    }

    // A copy of an embedded example in the working directory, byte for byte (tests/README.md).
    private string CopyExample(string example)
    {
        return _project.WriteBytesInWorkingDirectory(Path.GetFileName(example), Fixture.ReadBytes(example));
    }

    private static string[] Lines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    // The codes of the diagnostics in the order reported: file(line): ERROR VM042: message (D98).
    private static List<string> Codes(string diagnostics)
    {
        var codes = new List<string>();
        foreach (string line in Lines(diagnostics))
        {
            codes.Add(DiagnosticCode().Match(line).Groups[1].Value);
        }

        return codes;
    }

    [GeneratedRegex(@"\): (?:ERROR|WARNING|INFO) ([A-Z]+[0-9]{3}): ", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCode();
}
