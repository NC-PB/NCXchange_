using Ncx.Acceptance.Jobs;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx check --job &lt;name.ncxjob.toml&gt; and ncx analyze --job &lt;name.ncxjob.toml&gt;: the channel programs of a
/// job manifest run as one job in rounds, on the machine the manifest names unless --machine names another, with the
/// exit codes of D97 (architecture 10; virtual machine 3.7; machine-config 8; implementation 16, P6-01).
/// </summary>
public sealed class JobCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // P6-01 done when: the Nakamura WY250L pair, converted by ncx convert, checks as one job without deadlock.
    [Fact]
    public void CheckJob_NakamuraPair_ExitsZeroWithoutDeadlock()
    {
        string manifest = NakamuraJob();

        int exitCode = _cli.Run("check", "--job", manifest);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain("VM571", _cli.Error, StringComparison.Ordinal);
        Assert.Equal("", _cli.Output);
    }

    // P6-01 done when: the pair analyzes without deadlock; the reports of each channel follow a line naming it, in the
    // order of the manifest, on the machine the manifest names.
    [Fact]
    public void AnalyzeJob_NakamuraPair_WritesTheReportsOfBothChannels()
    {
        string manifest = NakamuraJob();

        int exitCode = _cli.Run("analyze", "--job", manifest, "--skip-blocks", "all");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.DoesNotContain("VM571", _cli.Error, StringComparison.Ordinal);
        Assert.StartsWith("Channel 1: NAKAMURA_WY250L_O1000.path1.ncx\n\nTool list: NAKAMURA_WY250L_O1000.path1.ncx, "
            + "the whole file, on nakamura-ntjx.toml, INTERPRETED run\n", _cli.Output, StringComparison.Ordinal);
        Assert.Contains("\nChannel 2: NAKAMURA_WY250L_O1000.path2.ncx\n\nTool list: NAKAMURA_WY250L_O1000.path2.ncx, "
            + "the whole file, on nakamura-ntjx.toml", _cli.Output, StringComparison.Ordinal);
    }

    // VM 3.7, D97: two channels waiting at different marks are the deadlock ERROR naming both marks, exit code 1.
    [Fact]
    public void CheckJob_ChannelsWaitingAtDifferentMarks_ExitsOneWithTheDeadlock()
    {
        string manifest = SmallJob(
            CliHarness.OneProgram("SYNC=100", "SYNC=200"), CliHarness.OneProgram("SYNC=200", "SYNC=100"));

        int exitCode = _cli.Run("check", "--job", manifest);

        Assert.Equal(1, exitCode);
        string deadlock = Assert.Single(_cli.Error.Split('\n'),
            line => line.Contains("VM571", StringComparison.Ordinal));
        Assert.Contains("one.ncx(3): ERROR VM571: Deadlock: channel 1 waits at SYNC=100", deadlock,
            StringComparison.Ordinal);
        Assert.Contains("channel 2 waits at SYNC=200", deadlock, StringComparison.Ordinal);
    }

    // D97, as for one file: a job that an ERROR stops before its first round writes no report, and exits 1.
    [Fact]
    public void AnalyzeJob_ParserErrorInAFile_WritesNoReportAndExitsOne()
    {
        string manifest = SmallJob(CliHarness.OneProgram("SYNC=100"), CliHarness.OneProgram("SYNC=100 SYNC=200"));

        int exitCode = _cli.Run("analyze", "--job", manifest);

        Assert.Equal(1, exitCode);
        Assert.Equal("", _cli.Output);
        Assert.Contains("two.ncx(3): ERROR PAR", _cli.Error, StringComparison.Ordinal);
    }

    // Architecture 10: --machine names the machine in place of the manifest's.
    [Fact]
    public void CheckJob_MachineOption_TakesThePlaceOfTheManifestsMachine()
    {
        string manifest = SmallJob(CliHarness.OneProgram("SYNC=100"), CliHarness.OneProgram("SYNC=100"),
            machine: "missing.toml");
        string machine = _cli.CopyExample("machines/nakamura-ntjx.toml");

        int exitCode = _cli.Run("check", "--job", manifest, "--machine", machine);

        Assert.True(exitCode == 0, _cli.Error);
    }

    // D97, D98: a machine that the manifest names and no folder holds is a missing machine file, exit code 2, on the
    // manifest.
    [Fact]
    public void CheckJob_MachineOfTheManifestMissing_ExitsTwoOnTheManifest()
    {
        string manifest = SmallJob(CliHarness.OneProgram("SYNC=100"), CliHarness.OneProgram("SYNC=100"),
            machine: "missing.toml");

        int exitCode = _cli.Run("check", "--job", manifest);

        Assert.Equal(2, exitCode);
        Assert.StartsWith(manifest + "(1): ERROR CLI200: The machine missing.toml of the job manifest " + manifest,
            _cli.Error, StringComparison.Ordinal);
    }

    // D97: a job manifest that cannot be read decides exit code 2 before the run starts.
    [Fact]
    public void CheckJob_ManifestMissing_ExitsTwo()
    {
        string manifest = _cli.PathOf("missing.ncxjob.toml");

        int exitCode = _cli.Run("check", "--job", manifest);

        Assert.Equal(2, exitCode);
        Assert.StartsWith(manifest + "(1): ERROR CLI500: The job manifest cannot be read", _cli.Error,
            StringComparison.Ordinal);
    }

    // D97, machine-config 8: the file of a channel that cannot be read decides exit code 2 before the run starts.
    [Fact]
    public void CheckJob_ChannelFileMissing_ExitsTwo()
    {
        _cli.CopyExample("machines/nakamura-ntjx.toml");
        string manifest = _cli.WriteFile("job.ncxjob.toml", CliHarness.Lines(
            "[job]", "machine = \"nakamura-ntjx.toml\"", "[[channel]]", "id = 1", "file = \"missing.ncx\""));

        int exitCode = _cli.Run("check", "--job", manifest);

        Assert.Equal(2, exitCode);
        Assert.Contains("missing.ncx(1): ERROR CLI501: The file of channel 1 of the job cannot be read", _cli.Error,
            StringComparison.Ordinal);
    }

    // Architecture 10, D97: a file and --job together, or neither, are usage errors, exit code 2.
    [Theory]
    [InlineData("check", "both")]
    [InlineData("check", "neither")]
    [InlineData("analyze", "both")]
    [InlineData("analyze", "neither")]
    [InlineData("compile", "both")]
    [InlineData("compile", "neither")]
    public void Job_FileAndJobTogetherOrNeither_IsUsageError(string command, string form)
    {
        string file = _cli.WriteFile("one.ncx", CliHarness.OneProgram());
        string[] args = form == "both" ? [command, file, "--job", "job.ncxjob.toml"] : [command];

        int exitCode = _cli.Run(args);

        Assert.Equal(2, exitCode);
        Assert.Contains("ERROR CLI001: ", _cli.Error, StringComparison.Ordinal);
        Assert.Contains(form == "both" ? "takes a file or --job, not both" : "needs a file or --job", _cli.Error,
            StringComparison.Ordinal);
    }

    // Architecture 10, implementation 16 (P6-02): ncx compile --job writes one NC file per channel of the job into the
    // folder of --output, on the machine the manifest names, whose start_mark M199 is the first block of both programs
    // (machine-config 5).
    [Fact]
    public void CompileJob_TwoChannels_WritesOneFilePerChannel()
    {
        string manifest = SmallJob(NumberedProgram("SYNC=110"), NumberedProgram("SYNC=110"));
        string folder = _cli.PathOf("out");

        int exitCode = _cli.Run("compile", "--job", manifest, "--output", folder);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("%\r\nO1000 (T)\r\nM199\r\nM110\r\nM30\r\n%\r\n",
            File.ReadAllText(Path.Combine(folder, "one.nc")));
        Assert.Equal("%\r\nO1000 (T)\r\nM199\r\nM110\r\nM30\r\n%\r\n",
            File.ReadAllText(Path.Combine(folder, "two.nc")));
    }

    // Virtual machine 3.8 rule 2a, D56: on the Nakamura the spindle synchronization is accepted only from path 2
    // ([spindle_sync] channel = 2); the job compiler moves M96 from the program of path 1 into that of path 2 at the
    // same mark.
    [Fact]
    public void CompileJob_SpindleSyncInPath1_StandsInThePathThatOwnsIt()
    {
        string manifest = SmallJob(NumberedProgram("SYNC=110", "SPINDLE_SYNC=MAIN,SUB", "SYNC=111"),
            NumberedProgram("SYNC=110", "SYNC=111"));
        string folder = _cli.PathOf("out");

        int exitCode = _cli.Run("compile", "--job", manifest, "--output", folder);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("%\r\nO1000 (T)\r\nM199\r\nM110\r\nM111\r\nM30\r\n%\r\n",
            File.ReadAllText(Path.Combine(folder, "one.nc")));
        Assert.Equal("%\r\nO1000 (T)\r\nM199\r\nM110\r\nM96\r\nM111\r\nM30\r\n%\r\n",
            File.ReadAllText(Path.Combine(folder, "two.nc")));
    }

    // Virtual machine 3.8 rule 2a, D56: the same program of path 1 compiled alone is the ERROR of a single-channel
    // compile, exit code 1, and no file is written.
    [Fact]
    public void Compile_SpindleSyncInAProgramOfPath1_IsCmp700()
    {
        string machine = _cli.CopyExample("machines/nakamura-ntjx.toml");
        string file = _cli.WriteFile("one.ncx", NumberedProgram("SYNC=110", "SPINDLE_SYNC=MAIN,SUB"));
        string folder = _cli.PathOf("out");

        int exitCode = _cli.Run("compile", file, "--machine", machine, "--output", folder);

        Assert.Equal(1, exitCode);
        Assert.Contains(file + "(5): ERROR CMP700: SPINDLE_SYNC=MAIN,SUB: the machine accepts [spindle_sync] only "
            + "from channel 2", _cli.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(folder));
    }

    // Virtual machine 3.6, machine-config 8: --vars names the start values of one file and does not go with --job.
    [Fact]
    public void AnalyzeJob_WithVars_IsUsageError()
    {
        int exitCode = _cli.Run("analyze", "--job", "job.ncxjob.toml", "--vars", "values.vars.toml");

        Assert.Equal(2, exitCode);
        Assert.Contains("--vars gives the start values of one file and does not go with --job", _cli.Error,
            StringComparison.Ordinal);
    }

    // The Nakamura pair in the temporary folder: both sources converted by ncx convert with nakamura-ntjx.toml, the
    // machine file and the manifest of Fixtures/ next to them.
    private string NakamuraJob()
    {
        string machine = _cli.CopyExample("machines/nakamura-ntjx.toml");
        foreach (string path in new[] { "path1", "path2" })
        {
            string source = _cli.CopyExample("sources/NAKAMURA_WY250L_O1000." + path + ".nc");
            string converted = _cli.PathOf("NAKAMURA_WY250L_O1000." + path + ".ncx");
            int convertExit = _cli.Run("convert", source, "--machine", machine, "--output", converted);
            Assert.True(convertExit == 0, _cli.Error);
        }

        return _cli.WriteFile(JobFixture.NakamuraJob, JobFixture.ReadText(JobFixture.NakamuraJob));
    }

    // A file of one program "T" with the program number 1000, which a Fanuc program needs (controllers fanuc.md 1),
    // in millimetres, and the given blocks from line 4 on.
    private static string NumberedProgram(params string[] blocks)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1000", "UNITS=MM" };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return CliHarness.Lines(lines.ToArray());
    }

    // A job of two channels, one.ncx and two.ncx, on nakamura-ntjx.toml next to its manifest unless the test names
    // another machine.
    private string SmallJob(string first, string second, string machine = "nakamura-ntjx.toml")
    {
        _cli.CopyExample("machines/nakamura-ntjx.toml");
        _cli.WriteFile("one.ncx", first);
        _cli.WriteFile("two.ncx", second);
        return _cli.WriteFile("job.ncxjob.toml", CliHarness.Lines(
            "[job]", $"machine = \"{machine}\"",
            "[[channel]]", "id = 1", "file = \"one.ncx\"",
            "[[channel]]", "id = 2", "file = \"two.ncx\""));
    }
}
