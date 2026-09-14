using System.Text.RegularExpressions;
using Ncx.Acceptance.Cli;
using Ncx.Cli;
using Ncx.Cli.Commands;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Readers.Heidenhain;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The acceptance of the readers through the command line: ncx convert on the example sources with their machines by
/// name, as "dotnet run --project src/Ncx.Cli -- convert docs/spec/examples/sources/2.5D_FRAESEN.fanuc.nc --machine
/// fanuc-mill-30i" runs it (phase 3, P3-02 and P3-05; milestones M4 and M6). 2.5D_FRAESEN from both sources equals the
/// blocks of examples/2.5D_FRAESEN.ncx as P3-02 compares them, BOHREN.fanuc.nc the frozen Expected/BOHREN.ncx, every
/// source what its reader reads through the library, and under NCX_CORPUS no file of the corpus crashes the command.
/// </summary>
public sealed partial class ConvertExampleTests : IDisposable
{
    // The files of a Fanuc or ISO program in the corpus, as FanucReaderTests counts them; Heidenhain files end with .h.
    private static readonly string[] s_fanucExtensions = [".nc", ".cnc", ".tap", ".eia", ".iso", ".ptp", ".min", ""];

    private readonly CliHarness _cli = new();

    /// <summary>
    /// Each example source with the machine it was written for: the mill sources with the Fanuc mill and the iTNC 530,
    /// the Nakamura pair with the Nakamura (implementation 13, P3-02, P3-05).
    /// </summary>
    public static TheoryData<string, string> SourcesWithTheirMachines()
    {
        return new TheoryData<string, string>
        {
            { "2.5D_FRAESEN.fanuc.nc", "fanuc-mill-30i" },
            { "BOHREN.fanuc.nc", "fanuc-mill-30i" },
            { "3D_FRAESEN.fanuc.nc", "fanuc-mill-30i" },
            { "2.5D_FRAESEN.h", "heidenhain-itnc530" },
            { "BOHREN.h", "heidenhain-itnc530" },
            { "3D_FRAESEN.h", "heidenhain-itnc530" },
            { "NAKAMURA_WY250L_O1000.path1.nc", "nakamura-ntjx" },
            { "NAKAMURA_WY250L_O1000.path2.nc", "nakamura-ntjx" },
        };
    }

    public void Dispose()
    {
        _cli.Dispose();
    }

    // M4, P3-02 done when: ncx convert 2.5D_FRAESEN.fanuc.nc --machine fanuc-mill-30i equals examples/2.5D_FRAESEN.ncx,
    // compared as P3-02 compares it: comments and trivia stripped on both sides and the blocks in canonical form, except
    // the blocks the example reads otherwise for Fanuc (FanucReaderTests, with its TODO(question)). The Fanuc source
    // converts without a diagnostic of the reader or of the check.
    [Fact]
    public void CommandLine_25DFraesenFanuc_EqualsTheBlocksOfTheExample()
    {
        string file = _cli.CopyExample("sources/2.5D_FRAESEN.fanuc.nc");

        int exitCode = _cli.Run("convert", file, "--machine", "fanuc-mill-30i");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("", _cli.Error);
        Assert.Equal(string.Join('\n', ExampleBlocks(FanucReaderTests.s_fanucReading)),
            string.Join('\n', BlocksOf(_cli.Output)));
    }

    // M6, P3-05 done when: ncx convert 2.5D_FRAESEN.h --machine heidenhain-itnc530 equals examples/2.5D_FRAESEN.ncx as
    // P3-02 compares it, except the blocks the example reads otherwise for Klartext (HeidenhainReaderTests, with its
    // TODO(question)), with the WARNING about the missing M30 (RDR010); the two BLK FORM blocks kept as RAW add their
    // WARNINGs, the reader's (RDR001, D5) and the check's (VM400, virtual machine 5).
    [Fact]
    public void CommandLine_25DFraesenHeidenhain_EqualsTheBlocksOfTheExampleWithTheWarningAboutTheMissingM30()
    {
        string file = _cli.CopyExample("sources/2.5D_FRAESEN.h");

        int exitCode = _cli.Run("convert", file, "--machine", "heidenhain-itnc530");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal(string.Join('\n', ExampleBlocks(HeidenhainReaderTests.s_heidenhainReading)),
            string.Join('\n', BlocksOf(_cli.Output)));
        Assert.Equal(["RDR010", "RDR001", "RDR001", "VM400", "VM400"], Codes(_cli.Error));
        Assert.Contains("(M30, M2)", _cli.Error.Split('\n')[0], StringComparison.Ordinal);
    }

    // P3-02 done when: BOHREN.fanuc.nc converts into the expected CYCLE= words, the frozen Expected/BOHREN.ncx, byte for
    // byte and without a diagnostic of the reader or of the check.
    [Fact]
    public void CommandLine_BohrenFanuc_EqualsTheFrozenExpectedFile()
    {
        string file = _cli.CopyExample("sources/BOHREN.fanuc.nc");

        int exitCode = _cli.Run("convert", file, "--machine", "fanuc-mill-30i");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("", _cli.Error);
        CliHarness.AssertExpectedFile("BOHREN.ncx", _cli.Output);
    }

    // Architecture 7, 10: the command writes what the reader of the machine's controller reads through the library,
    // byte for byte, and its STATIC check finds no ERROR in any example source, so what the tests of the readers
    // establish for the example sources holds for the command (FanucReaderTests, HeidenhainReaderTests).
    [Theory]
    [MemberData(nameof(SourcesWithTheirMachines))]
    public void CommandLine_ExampleSource_WritesWhatItsReaderReadsWithoutError(string source, string machine)
    {
        string file = _cli.CopyExample("sources/" + source);

        int exitCode = _cli.Run("convert", file, "--machine", machine);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain(": ERROR ", _cli.Error, StringComparison.Ordinal);
        Assert.Equal(LibraryReading(file, machine), _cli.Output);
    }

    // Under NCX_CORPUS every Fanuc, ISO and Heidenhain file of the corpus converts without a crash, the STATIC check over
    // its program included (phase 3, P3-02 and P3-05; controllers sample-corpus 3: a crash is a bug). Fanuc and ISO
    // files are read against the Fanuc mill, Heidenhain files against the iTNC 530; the batch of P3-07 reads them against
    // the closest machine.
    [CorpusFact]
    public void Convert_EveryFileOfTheCorpus_NeverCrashes()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        using var project = new ProjectHarness(Fixture.RepositoryRoot());
        ReaderRegistry readers = Program.Readers();
        var crashes = new List<string>();
        int files = 0;
        foreach (string path in Directory.EnumerateFiles(corpus, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path).ToUpperInvariant();
            string? machine = extension == ".H" ? "heidenhain-itnc530"
                : s_fanucExtensions.Contains(extension.ToLowerInvariant()) ? "fanuc-mill-30i" : null;
            if (machine is null)
            {
                continue;
            }

            files++;
            using var output = new StringWriter();
            using var error = new StringWriter();
            var settings = new RunSettings
            {
                File = path,
                MachineFile = machine,
                WorkingDirectory = project.WorkingDirectory,
                ToolFolder = project.ToolFolder,
            };
            try
            {
                ConvertCommand.Run(settings, null, readers, output, error);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
                or FormatException or IndexOutOfRangeException or OverflowException or NullReferenceException
                or KeyNotFoundException or InvalidCastException)
            {
                // The test lists every file that crashes, not only the first.
                crashes.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(crashes.Count == 0, $"{crashes.Count} of {files} files crash:\n" + string.Join('\n', crashes));
    }

    // The blocks of examples/2.5D_FRAESEN.ncx as a reading gives them: each block the reading writes otherwise replaced
    // by the lines of its reading, by none where the source has no block there (FanucReaderTests,
    // HeidenhainReaderTests).
    private static List<string> ExampleBlocks(KeyValuePair<string, string>[] reading)
    {
        var expected = new List<string>();
        int difference = 0;
        foreach (string block in BlocksOf(Fixture.ReadText("2.5D_FRAESEN.ncx")))
        {
            if (difference < reading.Length && reading[difference].Key == block)
            {
                expected.AddRange(reading[difference].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                difference++;
                continue;
            }

            expected.Add(block);
        }

        Assert.Equal(reading.Length, difference);
        return expected;
    }

    // The blocks of an NCX text in canonical form without their comments; the trivia are no blocks (P3-02: the
    // comparison strips comments and trivia on both sides).
    private static List<string> BlocksOf(string text)
    {
        NcxProgram program = Parser.Parse(text, "converted.ncx", new ParserOptions());
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        var blocks = new List<string>();
        foreach (Block block in program.Blocks)
        {
            blocks.Add(NcxWriter.WriteBlock(block with { Comment = null }));
        }

        return blocks;
    }

    // The reading through the library, without the command: the shipped machine file with the catalog its [cycles]
    // names (machine-config 6), the reader of its controller, the canonical writer (architecture 7).
    private static string LibraryReading(string file, string machineName)
    {
        string root = Fixture.RepositoryRoot();
        var diagnostics = new Diagnostics(machineName);
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(root, "machines", machineName + ".toml"), diagnostics);
        Assert.True(machine?.Machine.Controller is not null && !diagnostics.HasErrors, diagnostics.ToText());
        Controller controller = machine.Machine.Controller.Value;
        if (machine.Cycles?.CatalogFile is string catalogFile)
        {
            CycleCatalog? catalog =
                CycleCatalogLoader.Load(Path.Combine(root, "cycles", catalogFile), controller, diagnostics);
            Assert.True(catalog is not null, diagnostics.ToText());
            machine = CycleCatalogLoader.WithCatalog(machine, catalog);
        }

        IReader reader = controller == Controller.Heidenhain ? new HeidenhainReader() : new FanucReader();
        return NcxWriter.Write(reader.Read(new SourceFile(file, File.ReadAllText(file)), machine, new ReadOptions()));
    }

    // The codes of the diagnostics in the order reported: file(line): ERROR VM042: message (D98).
    private static List<string> Codes(string diagnostics)
    {
        var codes = new List<string>();
        foreach (string line in diagnostics.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            codes.Add(DiagnosticCode().Match(line).Groups[1].Value);
        }

        return codes;
    }

    [GeneratedRegex(@"\): (?:ERROR|WARNING|INFO) ([A-Z]+[0-9]{3}): ", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCode();
}
