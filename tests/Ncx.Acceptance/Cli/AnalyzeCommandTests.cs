using Ncx.Acceptance.Analytics;
using Ncx.Acceptance.Examples;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx analyze &lt;file&gt; [--machine] [--vars] [--from --to] [--analytic tools,runtime] [--static] [--format]: the
/// tool list and the runtime estimate of an INTERPRETED run over a block range, on the standard output, with the exit
/// codes of D97 (architecture 10; virtual machine 8; D67, D103; implementation 14, P4-02).
/// </summary>
public sealed class AnalyzeCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // Architecture 10, D103: analyze runs every example INTERPRETED without a machine file, exit 0, and writes the tool
    // list, then the runtime estimate.
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Analyze_Example_ExitsZeroAndWritesTheToolListAndTheRuntimeEstimate(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("analyze", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.StartsWith($"Tool list: {example}, the whole file, on the built-in default machine, INTERPRETED run\n",
            _cli.Output, StringComparison.Ordinal);
        Assert.Contains($"\n\nRuntime estimate: {example}, the whole file,", _cli.Output, StringComparison.Ordinal);
    }

    // P4-02 done when: the tool list of 2.5D_FRAESEN names tool 1 with the distances of the example (by hand in
    // ToolListAnalyticTests), as the reference file.
    [Fact]
    public void Analyze_Fraesen25DTools_EqualsTheReference()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--analytic", "tools");

        Assert.Equal(0, exitCode);
        AnalyticsReference.AssertEquals("2.5D_FRAESEN.tools.txt", _cli.Output);
    }

    // P4-02 done when: the runtime of 3D_FRAESEN.fanuc.nc, converted with fanuc-mill-30i, is within 10 percent of the
    // cycle time the maintainer measured on the machine. That time is not known yet, so the computed report is the
    // reference, unverified (implementation 14, P4-02): 191.5 s with the dynamics of fanuc-mill-30i.toml, which are
    // plausible values and not verified on a machine either (D100).
    // TODO: when the maintainer's measured cycle time of 3D_FRAESEN is known, assert the estimated time within 10
    // percent of it (P4-02 done when), and correct the dynamics of fanuc-mill-30i.toml rather than the code where it is
    // not.
    [Fact]
    public void Analyze_3DFraesenConvertedForFanucMill30i_RuntimeEqualsTheUnverifiedReference()
    {
        string source = _cli.CopyExample("sources/3D_FRAESEN.fanuc.nc");
        string file = _cli.PathOf("3D_FRAESEN.ncx");
        int convertExitCode = _cli.Run("convert", source, "--machine", "fanuc-mill-30i", "--output", file);

        int exitCode = _cli.Run("analyze", file, "--machine", "fanuc-mill-30i", "--analytic", "runtime");

        Assert.True(convertExitCode == 0 && exitCode == 0, _cli.Error);
        AnalyticsReference.AssertEquals("3D_FRAESEN.fanuc-mill-30i.runtime.txt", _cli.Output);
    }

    // VM 8, D67: --from and --to give the block range, NCX line numbers of the file, which the report names.
    [Fact]
    public void Analyze_FromTo_ReportsTheBlockRange()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--from", "33", "--to", "51", "--analytic", "tools");

        Assert.Equal(0, exitCode);
        Assert.StartsWith("Tool list: 2.5D_FRAESEN.ncx, lines 33 to 51,", _cli.Output, StringComparison.Ordinal);
        Assert.Contains("154.59   91.907  2        19", _cli.Output, StringComparison.Ordinal);
    }

    // D97, D67: a --from after --to is a usage error, exit 2 before the run starts, and nothing is written.
    [Fact]
    public void Analyze_FromAfterTo_IsAUsageError()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--from", "600", "--to", "380");

        Assert.Equal(2, exitCode);
        Assert.Equal("", _cli.Output);
        Assert.Contains("ERROR CLI001: --from 600 lies after --to 380", _cli.Error, StringComparison.Ordinal);
    }

    // D97, D67: a line number below 1 is a usage error.
    [Fact]
    public void Analyze_LineZero_IsAUsageError()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--from", "0");

        Assert.Equal(2, exitCode);
        Assert.Contains("ERROR CLI001: --from and --to are NCX line numbers of the file, 1 or more", _cli.Error,
            StringComparison.Ordinal);
    }

    // D97, architecture 9: --analytic names registered analytics; another name is a usage error that names them.
    [Fact]
    public void Analyze_UnknownAnalytic_IsAUsageErrorThatNamesTheAnalytics()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--analytic", "tools,loops");

        Assert.Equal(2, exitCode);
        Assert.Contains("--analytic names loops, which is no analytic of ncx analyze; the analytics are tools, runtime",
            _cli.Error, StringComparison.Ordinal);
    }

    // Architecture 9: --analytic writes the reports in the order it names them.
    [Fact]
    public void Analyze_AnalyticsNamed_WritesTheirReportsInThatOrder()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--analytic", "runtime,tools");

        Assert.Equal(0, exitCode);
        Assert.StartsWith("Runtime estimate: ", _cli.Output, StringComparison.Ordinal);
        Assert.Contains("\n\nTool list: ", _cli.Output, StringComparison.Ordinal);
    }

    // Implementation 14, P4-02: --static runs the one-pass form, which the report names.
    [Fact]
    public void Analyze_Static_ReportsTheStaticRun()
    {
        string file = _cli.CopyExample("INCREMENTAL_SUB.ncx");

        int exitCode = _cli.Run("analyze", file, "--static", "--analytic", "tools");

        Assert.Equal(0, exitCode);
        Assert.StartsWith("Tool list: INCREMENTAL_SUB.ncx, the whole file, on the built-in default machine, STATIC run",
            _cli.Output, StringComparison.Ordinal);
    }

    // VM 3.6: the vars file gives the start values of an INTERPRETED run; --vars with --static is a usage error.
    [Fact]
    public void Analyze_VarsWithStatic_IsAUsageError()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string vars = _cli.WriteFile("start.vars.toml", "Q1 = 100\n");

        int exitCode = _cli.Run("analyze", file, "--static", "--vars", vars);

        Assert.Equal(2, exitCode);
        Assert.Contains("--vars gives the start values of an INTERPRETED run", _cli.Error, StringComparison.Ordinal);
    }

    // VM 2.7, 3.6; machine-config 8: the vars file starts the variables the program reads; without it the unassigned
    // variable is an ERROR (D38).
    [Fact]
    public void Analyze_VarsFile_StartsTheVariablesOfTheRun()
    {
        string file = _cli.WriteFile("vars.ncx",
            CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X={$Q1} F=6000"));
        string vars = _cli.WriteFile("start.toml", "Q1 = 100\n");

        int withVars = _cli.Run("analyze", file, "--vars", vars, "--analytic", "runtime");
        int withoutVars = _cli.Run("analyze", file, "--analytic", "runtime");

        Assert.Equal(0, withVars);
        Assert.Equal(1, withoutVars);
    }

    // VM 8: CSV instead of aligned columns under --format csv; a field with a comma stands in quotes.
    [Fact]
    public void Analyze_FormatCsv_WritesCommaSeparatedValues()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--analytic", "tools", "--format", "csv");

        Assert.Equal(0, exitCode);
        Assert.Contains("\ntool,holder,preloaded,rpm,feeds,offsets,cutting,rapid,unknown,blocks,seconds\n", _cli.Output,
            StringComparison.Ordinal);
        Assert.Contains("\n1,H1,no,1592,\"2387 PER_MIN, 1.5 PER_REV\",\"LEN 1, RAD 1\",550.856,113.907,4,40,13.8\n",
            _cli.Output, StringComparison.Ordinal);
    }

    // Machine-config 4, implementation 14 risks: with a machine file the estimate is the trapezoidal profile of its
    // [dynamics], and the report names the file.
    [Fact]
    public void Analyze_MachineByName_TimesWithItsDynamicsAndNamesIt()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("analyze", file, "--machine", "fanuc-mill-30i", "--analytic", "runtime");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.StartsWith("Runtime estimate: 2.5D_FRAESEN.ncx, the whole file, on fanuc-mill-30i.toml, INTERPRETED run",
            _cli.Output, StringComparison.Ordinal);
        Assert.Contains("Motions: trapezoidal profile (virtual machine 8, D64), path_mode continuous, "
            + "corner_speed 2000 mm/min, block_time 0.001 s.", _cli.Output, StringComparison.Ordinal);
    }

    // D97: a file that cannot be read exits 2 before the run starts, and no report is written.
    [Fact]
    public void Analyze_FileThatDoesNotExist_ExitsTwoAndWritesNoReport()
    {
        int exitCode = _cli.Run("analyze", _cli.PathOf("missing.ncx"));

        Assert.Equal(2, exitCode);
        Assert.Equal("", _cli.Output);
        Assert.Contains("ERROR CLI002", _cli.Error, StringComparison.Ordinal);
    }

    // The reading of the TODO(question) in AnalyzeCommand (wave-2 question #42): an ERROR stops the run, the reports
    // cover the blocks before it after a line that says so, and the exit code is 1 (D97).
    [Fact]
    public void Analyze_RunStoppedByAnError_WritesTheReportsAfterALineThatSaysSo()
    {
        string file = _cli.WriteFile("stops.ncx", CliHarness.OneProgram("UNITS=MM", "VAR:Q1={1 / 0}"));

        int exitCode = _cli.Run("analyze", file);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("The run stopped at an ERROR; the reports cover the blocks executed before it", _cli.Output,
            StringComparison.Ordinal);
        Assert.Contains("ERROR VM900", _cli.Error, StringComparison.Ordinal);
    }

    // Architecture 10, D97: analyze accepts --strict; the WARNINGs of INCREMENTAL_SUB, whose HOME finds no reference
    // point on the default machine (D100), exit 1 under it, and the reports are written all the same.
    [Fact]
    public void AnalyzeStrict_IncrementalSubWithItsWarnings_ExitsOneAndWrites()
    {
        string file = _cli.CopyExample("INCREMENTAL_SUB.ncx");

        int exitCode = _cli.Run("analyze", file);
        int strictExitCode = _cli.Run("analyze", file, "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.NotEqual("", _cli.Output);
    }

    // Architecture 10: the help of analyze names its options.
    [Fact]
    public void Analyze_Help_NamesItsOptions()
    {
        int exitCode = _cli.Run("analyze", "--help");

        Assert.Equal(0, exitCode);
        foreach (string option in new[] { "--machine", "--vars", "--from", "--to", "--analytic", "--static", "--format",
            "--skip-blocks", "--strict" })
        {
            Assert.Contains(option, _cli.Output, StringComparison.Ordinal);
        }
    }
}
