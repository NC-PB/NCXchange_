using Ncx.Acceptance.Cli;
using Ncx.Acceptance.Examples;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The segment length and the tool vector change through ncx analyze on the examples, and their references for later
/// versions in Expected/analytics (implementation 14, P4-03; M7): 3D_FRAESEN from both of its sources, each converted
/// with the machine file of its controller and analyzed on it, and the 5-axis A/C pair of the maintainer's corpus.
/// </summary>
public sealed class SegmentAndVectorReferenceTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // Architecture 10, D103: analyze runs both analytics on every example INTERPRETED without a machine file, exit 0,
    // the segment length first, then the tool vector change, in the order --analytic names them.
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Analyze_Example_WritesTheSegmentLengthAndTheToolVectorChange(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("analyze", file, "--analytic", "segments,vectors");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.StartsWith($"Segment length: {example}, the whole file, on the built-in default machine, INTERPRETED "
            + "run\n", _cli.Output, StringComparison.Ordinal);
        Assert.Contains($"\n\nTool vector change: {example}, the whole file,", _cli.Output, StringComparison.Ordinal);
    }

    // P4-03 done when: the results on 3D_FRAESEN, both sources, are recorded as reference files in the acceptance
    // project (M7), the reference for later versions. Each source is converted with the machine file of its controller
    // and analyzed on it; the three mills of machines/ have no rotary axis, so every tool vector is the normal of the
    // WORKPLANE and changes by 0.
    [Theory]
    [InlineData("sources/3D_FRAESEN.fanuc.nc", "fanuc-mill-30i", "segments")]
    [InlineData("sources/3D_FRAESEN.fanuc.nc", "fanuc-mill-30i", "vectors")]
    [InlineData("sources/3D_FRAESEN.h", "heidenhain-itnc530", "segments")]
    [InlineData("sources/3D_FRAESEN.h", "heidenhain-itnc530", "vectors")]
    public void Analyze_3DFraesenFromEachSource_EqualsTheReference(string source, string machine, string analytic)
    {
        string file = Convert(source, machine);

        int exitCode = _cli.Run("analyze", file, "--machine", machine, "--analytic", analytic);

        Assert.True(exitCode == 0, _cli.Error);
        AnalyticsReference.AssertEquals($"3D_FRAESEN.{machine}.{analytic}.txt", _cli.Output);
    }

    // The two sources are the same part (sources README), and both readers read the cutting moves alike: the LINE and
    // ARC rows of their segment lengths are the same. Their RAPID rows differ at the ends of the program, where the
    // Fanuc source returns home with G28 and the Klartext source moves in the machine frame with M91.
    [Fact]
    public void Analyze_3DFraesenFromBothSources_MeasureTheSameCuttingMoves()
    {
        string fanuc = SegmentReport("sources/3D_FRAESEN.fanuc.nc", "fanuc-mill-30i");
        string heidenhain = SegmentReport("sources/3D_FRAESEN.h", "heidenhain-itnc530");

        Assert.Equal(Row(fanuc, "LINE "), Row(heidenhain, "LINE "));
        Assert.Equal(Row(fanuc, "ARC "), Row(heidenhain, "ARC "));
    }

    // P4-03 done when: the results on a 5-axis program of the corpus are recorded as reference files (M7). The
    // maintainer puts the 5-axis A/C pair into the folder 5X of the corpus with the machine file of each control,
    // heidenhain.toml for the Klartext program (.h) and fanuc.toml for the Fanuc program (.nc); each program is
    // converted and analyzed on its machine and compared with <part>.heidenhain.segments.txt, .vectors.txt and the
    // fanuc ones, <part> the name of the program up to its first dot. A report without a reference is written to the
    // temporary folder ncx-acceptance, to be reviewed and committed as the reference.
    [FiveAxisCorpusFact]
    public void Analyze_FiveAxisPairOfTheCorpus_EqualsTheReferences()
    {
        string folder = FiveAxisCorpusFactAttribute.Folder();
        var sources = new List<string>(Directory.GetFiles(folder, "*.h"));
        sources.AddRange(Directory.GetFiles(folder, "*.nc"));
        Assert.NotEmpty(sources);

        foreach (string source in sources)
        {
            string machineName = source.EndsWith(".h", StringComparison.OrdinalIgnoreCase) ? "heidenhain" : "fanuc";
            string machine = Path.Combine(folder, machineName + ".toml");
            string part = Path.GetFileName(source).Split('.')[0];
            string file = _cli.PathOf(part + ".ncx");
            int convertExitCode = _cli.Run("convert", source, "--machine", machine, "--output", file);
            Assert.True(convertExitCode == 0, _cli.Error);

            foreach (string analytic in new[] { "segments", "vectors" })
            {
                int exitCode = _cli.Run("analyze", file, "--machine", machine, "--analytic", analytic);
                Assert.True(exitCode == 0, _cli.Error);
                AnalyticsReference.AssertEquals($"{part}.{machineName}.{analytic}.txt", _cli.Output);
            }
        }
    }

    // A source of the examples converted with a shipped machine file into 3D_FRAESEN.ncx of the temporary folder.
    private string Convert(string source, string machine)
    {
        string copy = _cli.CopyExample(source);
        string file = _cli.PathOf("3D_FRAESEN.ncx");
        int exitCode = _cli.Run("convert", copy, "--machine", machine, "--output", file);
        Assert.True(exitCode == 0, _cli.Error);
        return file;
    }

    private string SegmentReport(string source, string machine)
    {
        string file = Convert(source, machine);
        int exitCode = _cli.Run("analyze", file, "--machine", machine, "--analytic", "segments");
        Assert.True(exitCode == 0, _cli.Error);
        return _cli.Output;
    }

    // The line of a report that starts with the text: the row of a verb.
    private static string Row(string report, string start)
    {
        foreach (string line in report.Split('\n'))
        {
            if (line.StartsWith(start, StringComparison.Ordinal))
            {
                return line;
            }
        }

        return "";
    }
}
