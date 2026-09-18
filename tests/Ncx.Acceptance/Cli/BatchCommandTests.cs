using System.Text;
using System.Text.RegularExpressions;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx convert --batch &lt;folder&gt; --machine &lt;toml&gt; --report &lt;file&gt;: every file of a folder converted as
/// ncx convert converts one program, the batch never stopping on an error, and a summary of counts and codes in the
/// file of --report (implementation 13, P3-07; controllers sample-corpus 3). The batch over
/// docs/spec/examples/sources/ writes the reports of tests/corpus-reports/. The runs go through BatchCommand.Run with a
/// working directory of their own and the repository as the tool's own folder, or through the command line in a
/// temporary folder (code-guidelines 8).
/// </summary>
public sealed partial class BatchCommandTests : IDisposable
{
    // A Fanuc program that reads and checks without a diagnostic.
    private const string CleanProgram =
        "%\nO0004\nG90 G21 G17 G94\nT1 M6\nS1000 M3\nG0 X0. Y0. Z5.\nG1 X10. F100.\nM30\n%\n";

    // The M6 of line 4 changes to the preloaded tool and nothing is preloaded, the ERROR RDR101 of the Fanuc reader
    // (controller-mapping 3).
    private const string ChangeWithoutPreload = "%\nO0001\nG90 G21 G17\nM6\nG0 X0. Y0.\nG1 X10. F100.\nM30\n%\n";

    // The word that makes CrashingReader throw.
    private const string CrashMark = "(CRASH)";

    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());
    private readonly CliHarness _cli = new();

    // What the last run wrote to the standard error, the diagnostics (D98).
    private string _error = "";

    public void Dispose()
    {
        _project.Dispose();
        _cli.Dispose();
    }

    // Implementation 13, P3-07: the batch command on docs/spec/examples/sources/ produces the report, once with each
    // mill, every file of the folder read by the reader of the machine's controller and none crashing it. The reports
    // are tests/corpus-reports/examples-sources.<machine>.txt, written by this test when they differ and committed, as
    // the generated documents are (implementation 00-method 4).
    [Theory]
    [InlineData("fanuc-mill-30i")]
    [InlineData("heidenhain-itnc530")]
    public void Batch_ExampleSources_WritesTheReportOfTheFolder(string machine)
    {
        string folder = CopyExampleSources();
        string report = _cli.PathOf("report.txt");

        _cli.Run("convert", "--batch", folder, "--machine", machine, "--report", report);

        string written = File.ReadAllText(report);
        Assert.Contains("files: 9, converted 9, unreadable 0, crashed 0\n", written, StringComparison.Ordinal);
        string path = Path.Combine(Fixture.RepositoryRoot(), "tests", "corpus-reports",
            $"examples-sources.{machine}.txt");
        string committed = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
        if (committed != written)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, written);
        }

        Assert.Equal(written, committed);
    }

    // Implementation 13, P3-07: a file whose conversion crashes is reported with CLI351 and counted in the report with
    // the name of its exception, and the batch goes on with the next file; the crash sets exit code 1 (D97).
    [Fact]
    public void Batch_FileThatCrashesTheReader_IsCountedAndTheBatchGoesOn()
    {
        string folder = WriteFolder(("a.nc", CleanProgram), ("b.nc", CrashMark + "\n" + CleanProgram),
            ("c.nc", CleanProgram));
        var readers = new ReaderRegistry();
        readers.Register(Controller.Fanuc, () => new CrashingReader());

        int exitCode = RunBatch(folder, "fanuc-mill-30i", readers);

        Assert.Equal(1, exitCode);
        Assert.Equal(["CLI351"], Codes(_error));
        Assert.StartsWith(Path.Join(folder, "b.nc") + "(1): ERROR CLI351: The conversion of the program stopped with "
            + "InvalidOperationException: ", _error, StringComparison.Ordinal);
        string report = File.ReadAllText(ReportPath());
        Assert.Contains("files: 3, converted 2, unreadable 0, crashed 1\n", report, StringComparison.Ordinal);
        Assert.Matches(@"\nb\.nc +crashed InvalidOperationException +0 +- +CLI351 1\n", report);
        Assert.Matches(@"\nc\.nc +converted +[0-9]+ +- +-\n", report);
    }

    // Implementation 13, P3-07: an ERROR of a file does not stop the batch; the file is converted and counted with its
    // codes, and the ERROR sets exit code 1 (D97).
    [Fact]
    public void Batch_ErrorOfAFile_IsCountedAndTheBatchGoesOn()
    {
        string folder = WriteFolder(("a.nc", ChangeWithoutPreload), ("b.nc", CleanProgram));

        int exitCode = RunBatch(folder, "fanuc-mill-30i");

        Assert.Equal(1, exitCode);
        Assert.Contains(Path.Join(folder, "a.nc") + "(4): ERROR RDR101: ", _error, StringComparison.Ordinal);
        string report = File.ReadAllText(ReportPath());
        Assert.Contains("files: 2, converted 2, unreadable 0, crashed 0\n", report, StringComparison.Ordinal);
        Assert.Matches(@"\na\.nc +converted +[0-9]+ +- +RDR101 1\n", report);
        Assert.Matches(@"\nRDR101 ERROR +1 +1\n", report);
    }

    // Implementation 13, P3-07: a clean folder converts with exit code 0, and the report counts the blocks of every
    // file and in total.
    [Fact]
    public void Batch_CleanFolder_ExitsZeroAndCountsTheBlocks()
    {
        string folder = WriteFolder(("a.nc", CleanProgram), ("b.nc", CleanProgram));

        int exitCode = RunBatch(folder, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _error);
        Assert.Equal("", _error);
        string report = File.ReadAllText(ReportPath());
        Match block = Regex.Match(report, @"\na\.nc +converted +(?<blocks>[0-9]+) +- +-\n");
        Assert.True(block.Success, report);
        int blocks = int.Parse(block.Groups["blocks"].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Matches(@"\nblocks +" + (2 * blocks).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " +2\n", report);
    }

    // The TODO(question) of BatchCommand.Run: every file of the folder and of its folders is converted, whatever its
    // name, and the report lists them by their paths from the folder with forward slashes, in ordinal order.
    [Fact]
    public void Batch_FilesOfTheFolderAndItsFolders_AreListedByTheirPathsInOrder()
    {
        string folder = WriteFolder(("sub/a.nc", CleanProgram), ("b.nc", CleanProgram), ("C.NC", CleanProgram),
            ("notes", CleanProgram));

        RunBatch(folder, "fanuc-mill-30i");

        List<string> names = [];
        foreach (Match row in FileRow().Matches(File.ReadAllText(ReportPath())))
        {
            names.Add(row.Groups["name"].Value);
        }

        Assert.Equal(["C.NC", "b.nc", "notes", "sub/a.nc"], names);
    }

    // D229, as recommended: a file that is no UTF-8 text is read as Windows-1252 with the WARNING CLI352, converted and
    // counted with it; --strict makes the WARNING exit code 1 (D97).
    [Fact]
    public void Batch_FileThatIsNoUtf8_IsReadAsWindows1252AndCountedWithTheWarning()
    {
        string folder = WriteFolder(("a.nc", CleanProgram));
        File.WriteAllBytes(Path.Combine(folder, "b.nc"), Encoding.Latin1.GetBytes("%\nO0001 (ÄNDERUNG)\nM30\n%\n"));

        int exitCode = RunBatch(folder, "fanuc-mill-30i");
        int strictExitCode = RunBatch(folder, "fanuc-mill-30i", strict: true);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.StartsWith(Path.Join(folder, "b.nc") + "(1): WARNING CLI352: ", _error, StringComparison.Ordinal);
        Assert.Matches(@"\nb\.nc +converted +[0-9]+ +- +CLI352 1\n", File.ReadAllText(ReportPath()));
    }

    // D97, implementation 13, P3-07: a folder that is not there cannot be read, CLI350, exit code 2 before the first
    // file, and no report is written.
    [Fact]
    public void Batch_FolderThatIsNotThere_ExitsTwoWithCli350()
    {
        string folder = Path.Combine(_project.WorkingDirectory, "missing");

        int exitCode = RunBatch(folder, "fanuc-mill-30i");

        Assert.Equal(2, exitCode);
        Assert.StartsWith(folder + "(1): ERROR CLI350: The folder of --batch cannot be read: ", _error,
            StringComparison.Ordinal);
        Assert.False(File.Exists(ReportPath()));
    }

    // D77, D97, D103: the batch, like convert, never runs against the built-in default machine; without --machine and
    // without ncx.toml it exits 2 with CLI250 before the first file.
    [Fact]
    public void Batch_WithoutMachine_ExitsTwoWithCli250()
    {
        string folder = WriteFolder(("a.nc", CleanProgram));

        int exitCode = RunBatch(folder, null);

        Assert.Equal(2, exitCode);
        Assert.Equal(["CLI250"], Codes(_error));
        Assert.False(File.Exists(ReportPath()));
    }

    // D97, architecture 10: a report file that cannot be written is an ERROR of the run, which has started, CLI004,
    // exit code 1.
    [Fact]
    public void Batch_ReportThatCannotBeWritten_ExitsOneWithCli004()
    {
        string folder = WriteFolder(("a.nc", CleanProgram));
        string report = Path.Combine(_project.WorkingDirectory, "no-such-folder", "report.txt");

        int exitCode = RunBatch(folder, "fanuc-mill-30i", reportFile: report);

        Assert.Equal(1, exitCode);
        Assert.Equal(["CLI004"], Codes(_error));
    }

    // Implementation 13, P3-07; D209, D97: convert takes one program or a folder with --batch, --batch needs --report
    // and takes neither a program nor --output, and --report belongs to --batch; any other command line is a usage
    // error, CLI001, exit code 2.
    [Theory]
    [InlineData("convert", "--machine", "fanuc-mill-30i")]
    [InlineData("convert", "part.nc", "--batch", "folder", "--machine", "fanuc-mill-30i", "--report", "report.txt")]
    [InlineData("convert", "--batch", "folder", "--machine", "fanuc-mill-30i")]
    [InlineData("convert", "part.nc", "--machine", "fanuc-mill-30i", "--report", "report.txt")]
    [InlineData("convert", "--batch", "folder", "--machine", "fanuc-mill-30i", "--report", "report.txt", "--output",
        "out.ncx")]
    public void CommandLine_BatchOptionsThatDoNotGoTogether_AreAUsageError(params string[] args)
    {
        int exitCode = _cli.Run(args);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _cli.Error, StringComparison.Ordinal);
    }

    // The report holds counts and codes and the names of the files only: every outcome with its columns, the RAW
    // blocks per word, the totals with the severity of each code and the files it stands in (the TODO(question) of
    // BatchReport).
    [Fact]
    public void Report_FilesOfEveryOutcome_ListsTheirCountsAndTheTotals()
    {
        var files = new List<BatchFile>
        {
            CountedFile("a.nc", BatchOutcome.Converted, null, 12, [("RAW:FANUC", 2), ("RAW:NAKAMURA", 1)],
                [("RDR001", 3, Severity.Warning), ("VM005", 1, Severity.Error)]),
            CountedFile("sub/b.nc", BatchOutcome.Unreadable, null, 0, [], [("CLI002", 1, Severity.Error)]),
            CountedFile("c.nc", BatchOutcome.Crashed, "IndexOutOfRangeException", 0, [],
                [("CLI351", 1, Severity.Error)]),
            CountedFile("d.nc", BatchOutcome.Converted, null, 7, [("RAW:FANUC", 1)],
                [("RDR001", 1, Severity.Warning)]),
        };

        string report = BatchReport.Write("mill.toml (Mill, Fanuc)", files);

        Assert.Equal("""
            ncx convert --batch (implementation 13, P3-07)
            machine: mill.toml (Mill, Fanuc)
            files: 4, converted 2, unreadable 1, crashed 1

            file      result                            blocks  RAW                          diagnostics
            a.nc      converted                             12  RAW:FANUC 2, RAW:NAKAMURA 1  RDR001 3, VM005 1
            sub/b.nc  unreadable                             0  -                            CLI002 1
            c.nc      crashed IndexOutOfRangeException       0  -                            CLI351 1
            d.nc      converted                              7  RAW:FANUC 1                  RDR001 1

            total           count  files
            blocks             19      2
            RAW:FANUC           3      2
            RAW:NAKAMURA        1      1
            CLI002 ERROR        1      1
            CLI351 ERROR        1      1
            RDR001 WARNING      4      2
            VM005 ERROR         1      1

            """.ReplaceLineEndings("\n"), report);
    }

    // A file of the report as the batch counts it.
    private static BatchFile CountedFile(string name, BatchOutcome outcome, string? crash, int blocks,
        (string Word, int Count)[] raw, (string Code, int Count, Severity Severity)[] codes)
    {
        var rawBlocks = new Dictionary<string, int>();
        foreach ((string word, int count) in raw)
        {
            rawBlocks[word] = count;
        }

        var counts = new Dictionary<string, int>();
        var severities = new Dictionary<string, Severity>();
        foreach ((string code, int count, Severity severity) in codes)
        {
            counts[code] = count;
            severities[code] = severity;
        }

        return new BatchFile
        {
            Name = name,
            Outcome = outcome,
            Crash = crash,
            Blocks = blocks,
            RawBlocks = rawBlocks,
            Codes = counts,
            Severities = severities,
        };
    }

    // ncx convert --batch through BatchCommand.Run, the report into report.txt of the working directory unless named.
    private int RunBatch(string folder, string? machine, ReaderRegistry? readers = null, bool strict = false,
        string? reportFile = null)
    {
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = folder,
            MachineFile = machine,
            Strict = strict,
            WorkingDirectory = _project.WorkingDirectory,
            ToolFolder = _project.ToolFolder,
        };

        int exitCode = BatchCommand.Run(settings, reportFile ?? ReportPath(), readers ?? Program.Readers(), error);
        _error = error.ToString();
        return exitCode;
    }

    private string ReportPath()
    {
        return Path.Combine(_project.WorkingDirectory, "report.txt");
    }

    // A folder "programs" of the working directory with the files given, by their paths in it.
    private string WriteFolder(params (string Name, string Text)[] files)
    {
        foreach ((string name, string text) in files)
        {
            _project.WriteInWorkingDirectory(Path.Combine("programs", name), text);
        }

        return Path.Combine(_project.WorkingDirectory, "programs");
    }

    // The files of docs/spec/examples/sources/, embedded in the test assembly, copied byte for byte into a folder
    // "sources" of the temporary folder (tests/README.md).
    private string CopyExampleSources()
    {
        string folder = _cli.PathOf("sources");
        Directory.CreateDirectory(folder);
        foreach (string example in Fixture.List())
        {
            if (example.StartsWith("sources/", StringComparison.Ordinal))
            {
                File.WriteAllBytes(Path.Combine(folder, example.Substring("sources/".Length)),
                    Fixture.ReadBytes(example));
            }
        }

        return folder;
    }

    // The codes of the diagnostics in the order reported: file(line): ERROR CLI351: message (D98).
    private static List<string> Codes(string diagnostics)
    {
        var codes = new List<string>();
        foreach (Match match in DiagnosticCode().Matches(diagnostics))
        {
            codes.Add(match.Groups[1].Value);
        }

        return codes;
    }

    [GeneratedRegex(@"\): (?:ERROR|WARNING|INFO) ([A-Z]+[0-9]{3}): ", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCode();

    // A row of the files of the report: its name, then its result.
    [GeneratedRegex(@"^(?<name>\S+) +(?:converted|unreadable|crashed)", RegexOptions.Multiline
        | RegexOptions.CultureInvariant)]
    private static partial Regex FileRow();

    // The Fanuc reader, except for a program that holds the crash mark, on which it throws as a defect of a reader
    // would.
    private sealed class CrashingReader : IReader
    {
        public Controller Controller => Controller.Fanuc;

        public NcxProgram Read(SourceFile source, MachineConfig machine, ReadOptions options)
        {
            if (source.Text.Contains(CrashMark, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("the reader of this test crashes on purpose");
            }

            return new FanucReader().Read(source, machine, options);
        }
    }
}
