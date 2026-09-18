using System.Diagnostics;
using System.Text.RegularExpressions;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The Fanuc reader over the example sources end to end, through the library: the reader with the machine file loaded
/// by its path, then the canonical writer (phase 3, P3-02; ncx convert joins in part two). 2.5D_FRAESEN.fanuc.nc reads
/// into the blocks of examples/2.5D_FRAESEN.ncx, BOHREN.fanuc.nc into the frozen Expected/BOHREN.ncx,
/// 3D_FRAESEN.fanuc.nc in well under a second, the Nakamura pair with RAW only for the codes the plan names, and under
/// NCX_CORPUS every Fanuc and ISO file of the corpus without a crash.
/// </summary>
[Collection(ChainTiming.Name)]
public sealed partial class FanucReaderTests
{
    // The blocks of examples/2.5D_FRAESEN.ncx that the Fanuc reading writes otherwise, in their order: the example is
    // written as both readings together and its notes name where the two differ. An empty reading means the Fanuc
    // source has no block there.
    // TODO(question): the phase plan (P3-02) expects the Fanuc source to equal the example block for block with
    // comments and trivia stripped, while the example itself gives the Fanuc reading other blocks (notes 2 and 4: the
    // offsets where G43 H and D stand, D7; HOME for G28), and controller-mapping 1 makes a comment-only Fanuc line
    // trivia and no SECTION (D92), S belongs to the M3 of its block (controllers fanuc.md 5), and N70 of the source
    // carries no G40 for the COMP=OFF of the Heidenhain R0. The test compares every other block of the example as the
    // plan says, until D217 is answered.
    internal static readonly KeyValuePair<string, string>[] s_fanucReading =
    [
        new("SECTION=\"SIDE MILL D10 L35 SD10\"", ""),
        new("TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1592", "TOOL=1"),
        new("SPINDLE=CW", "SPINDLE=CW RPM=1592"),
        new("SECTION=\"UMFAHREN\"", ""),
        new("RAPID X=50.4 Y=-7.025 COMP=OFF", "RAPID X=50.4 Y=-7.025"),
        new("RAPID Z=2", "RAPID Z=2 OFFSET:LEN=1"),
        new("LINE Y=2 COMP=LEFT", "LINE Y=2 OFFSET:RAD=1 COMP=LEFT"),
        new("SECTION=\"KREISTASCHE\"", ""),
        new("RAPID Z=0 FRAME=MACHINE", "HOME Z"),
        new("RAPID X=0 Y=0 FRAME=MACHINE", "HOME X Y"),
    ];

    // The files of a Fanuc or ISO program in the corpus; Heidenhain (.h) and Siemens (.mpf, .spf) files are others'.
    private static readonly string[] s_fanucExtensions = [".nc", ".cnc", ".tap", ".eia", ".iso", ".ptp", ".min", ""];

    // M4: 2.5D_FRAESEN.fanuc.nc with machines/fanuc-mill-30i.toml reads into the blocks of examples/2.5D_FRAESEN.ncx,
    // comments and trivia stripped on both sides, the documented differences of the Fanuc reading applied.
    [Fact]
    public void Convert_25DFraesen_ReadsIntoTheBlocksOfTheExample()
    {
        NcxProgram example = Parser.Parse(
            Fixture.ReadText("2.5D_FRAESEN.ncx"), "2.5D_FRAESEN.ncx", new ParserOptions());
        NcxProgram read = Read("2.5D_FRAESEN.fanuc.nc", Mill());

        var expected = new List<string>();
        int difference = 0;
        foreach (string block in BlocksOf(example))
        {
            if (difference < s_fanucReading.Length && s_fanucReading[difference].Key == block)
            {
                if (s_fanucReading[difference].Value.Length > 0)
                {
                    expected.Add(s_fanucReading[difference].Value);
                }

                difference++;
                continue;
            }

            expected.Add(block);
        }

        Assert.Equal(s_fanucReading.Length, difference);
        Assert.Equal(string.Join('\n', expected), string.Join('\n', BlocksOf(read)));
        Assert.Empty(read.Diagnostics.Items);
    }

    // BOHREN.fanuc.nc reads into the expected file, reviewed against controller-mapping 5 before it was frozen
    // (Expected/README.md).
    [Fact]
    public void Convert_Bohren_EqualsTheFrozenExpectedFile()
    {
        string expected = File.ReadAllText(
            Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "BOHREN.ncx"));
        NcxProgram read = Read("BOHREN.fanuc.nc", Mill());

        Assert.Equal(expected.Replace("\r\n", "\n", StringComparison.Ordinal), NcxWriter.Write(read));
        Assert.Empty(read.Diagnostics.Items);
    }

    // 3D_FRAESEN.fanuc.nc, 834 blocks of short moves, converts in well under a second (phase 3, P3-02).
    [Fact]
    public void Convert_3DFraesen_TakesWellUnderASecond()
    {
        MachineConfig mill = Mill();
        Read("3D_FRAESEN.fanuc.nc", mill);

        var watch = Stopwatch.StartNew();
        NcxProgram read = Read("3D_FRAESEN.fanuc.nc", mill);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 500, $"3D_FRAESEN took {watch.ElapsedMilliseconds} ms.");
        Assert.Empty(read.Diagnostics.Items);
    }

    // The output of the reader is a program that ncx format gives back as it is and that check runs without an ERROR
    // (code-guidelines 4; D91).
    [Theory]
    [InlineData("2.5D_FRAESEN.fanuc.nc")]
    [InlineData("BOHREN.fanuc.nc")]
    [InlineData("3D_FRAESEN.fanuc.nc")]
    public void Convert_MillSources_FormatToThemselvesAndCheckWithoutError(string source)
    {
        MachineConfig mill = Mill();
        string text = NcxWriter.Write(Read(source, mill));
        NcxProgram parsed = Parser.Parse(text, source, new ParserOptions());
        var check = new Diagnostics(source);
        new VirtualMachine(mill, VmOptions.ForMachine(mill), check).Run(parsed);

        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
        Assert.False(check.HasErrors, check.ToText());
    }

    // The Nakamura pair with nakamura-ntjx.toml keeps as RAW the builder codes of its [raw] table, G411, G300, G333,
    // G131, as RAW:NAKAMURA, the data setting G10 as RAW:FANUC (the RAW address is the controller or builder dialect,
    // language 4.1; [raw] names the builder codes, machine-config 5; G10 is the control's, controller-mapping 9 and
    // controllers fanuc.md 9 rule 7), and the three blocks of path 2 that read system variables the machine file does
    // not map (#11099, #5024, #5025; language 4.12, D51) as RAW:FANUC until D250 is answered; nothing else. The output
    // formats to itself.
    [Theory]
    [InlineData("NAKAMURA_WY250L_O1000.path1.nc")]
    [InlineData("NAKAMURA_WY250L_O1000.path2.nc")]
    public void Convert_NakamuraPath_KeepsOnlyTheNamedCodesAsRaw(string source)
    {
        NcxProgram read = Read(source, Load("docs/spec/examples/machines/nakamura-ntjx.toml"));
        string text = NcxWriter.Write(read);

        foreach (Block block in read.Blocks)
        {
            if (block.Find("RAW") is not Word raw)
            {
                continue;
            }

            string native = ((StringValue)raw.Value).Content;
            bool builderCode = BuilderCode().IsMatch(native);
            bool dataSetting = DataSetting().IsMatch(native);
            bool unmappedSystemVariable = UnmappedSystemVariable().IsMatch(native);
            Assert.True(raw.Addr == "NAKAMURA" ? builderCode : dataSetting || unmappedSystemVariable,
                $"{source}({block.Line}): {raw.ToCanonical()}");
        }

        FormatsToItself(text, source);
    }

    // Every block a G411 jumps to keeps its LABEL, so that the RAW jumps still find their blocks after a compile; the
    // N2 M2 section after N30 M30 stands in front of PROGRAM=END with LABEL=2 (controller-mapping 6 and 8; language
    // 4.13, the Nakamura sample; D5).
    [Theory]
    [InlineData("NAKAMURA_WY250L_O1000.path1.nc")]
    [InlineData("NAKAMURA_WY250L_O1000.path2.nc")]
    public void Convert_NakamuraPath_KeepsTheLabelOfEveryG411Target(string source)
    {
        NcxProgram read = Read(source, Load("docs/spec/examples/machines/nakamura-ntjx.toml"));
        List<Block> blocks = [.. read.Blocks];

        var labels = new HashSet<string>(StringComparer.Ordinal);
        var targets = new List<string>();
        foreach (Block block in blocks)
        {
            if (block.Find("LABEL") is Word label)
            {
                labels.Add(label.Value.ToCanonical());
            }

            if (block.Find("RAW") is Word raw && G411Target().Match(((StringValue)raw.Value).Content) is
                { Success: true } target)
            {
                targets.Add(target.Groups[1].Value);
            }
        }

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.Contains(target, labels));
        int moved = blocks.FindIndex(block => block.Find("LABEL") is Word label && label.Value.ToCanonical() == "2");
        Assert.True(moved > 0, "LABEL=2 is missing.");
        Assert.True(blocks[moved + 1].Has("JUMP", null, "END"), NcxWriter.WriteBlock(blocks[moved + 1]));
        Assert.True(blocks[moved + 2].Has("PROGRAM", null, "END"), NcxWriter.WriteBlock(blocks[moved + 2]));
    }

    // Under NCX_CORPUS every Fanuc and ISO file of the corpus converts without a crash (phase 3, P3-02; controllers
    // sample-corpus 3: a crash is a bug). The files are read against the Fanuc mill; the batch of P3-07 reads them
    // against the closest machine.
    [CorpusFact]
    public void Convert_EveryFanucFileOfTheCorpus_NeverCrashes()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        MachineConfig mill = Mill();
        var crashes = new List<string>();
        int files = 0;
        foreach (string path in Directory.EnumerateFiles(corpus, "*", SearchOption.AllDirectories))
        {
            if (!s_fanucExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                continue;
            }

            files++;
            try
            {
                new FanucReader().Read(new SourceFile(Path.GetFileName(path), File.ReadAllText(path)), mill,
                    new ReadOptions());
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
                or FormatException or IndexOutOfRangeException or OverflowException or NullReferenceException
                or KeyNotFoundException)
            {
                // The test lists every file that crashes, not only the first.
                crashes.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(crashes.Count == 0, $"{crashes.Count} of {files} files crash:\n" + string.Join('\n', crashes));
    }

    private static NcxProgram Read(string source, MachineConfig machine)
    {
        return new FanucReader().Read(new SourceFile(source, Fixture.ReadText("sources/" + source)), machine,
            new ReadOptions());
    }

    // The mill of the Fanuc sources, loaded by its path as ncx convert --machine fanuc-mill-30i will load it.
    private static MachineConfig Mill()
    {
        return Load("machines/fanuc-mill-30i.toml");
    }

    // A machine file of the repository with the cycle catalog its [cycles] names (machine-config 6).
    private static MachineConfig Load(string relativePath)
    {
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "fanuc.toml"), Controller.Fanuc, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    // The blocks of a program in canonical form without their comments; the trivia are no blocks.
    private static List<string> BlocksOf(NcxProgram program)
    {
        var blocks = new List<string>();
        foreach (Block block in program.Blocks)
        {
            blocks.Add(NcxWriter.WriteBlock(block with { Comment = null }));
        }

        return blocks;
    }

    private static void FormatsToItself(string text, string source)
    {
        NcxProgram parsed = Parser.Parse(text, source, new ParserOptions());
        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
    }

    [GeneratedRegex(@"G0*(411|300|333|131)(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex BuilderCode();

    [GeneratedRegex(@"G0*10(?![0-9.])", RegexOptions.CultureInvariant)]
    private static partial Regex DataSetting();

    [GeneratedRegex(@"#(11099|5024|5025)(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex UnmappedSystemVariable();

    // G411 x1. I{n}: the jump on the part status to the block Nn (controller-mapping 6 and 8).
    [GeneratedRegex(@"G0*411[A-Z][0-9.]*I0*([0-9]+)\.?", RegexOptions.CultureInvariant)]
    private static partial Regex G411Target();
}
