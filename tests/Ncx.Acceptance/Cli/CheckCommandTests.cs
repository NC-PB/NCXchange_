using Ncx.Acceptance.Examples;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx check &lt;file&gt; [--machine &lt;toml&gt;] [--strict] [--skip-blocks none|all|1,3] [--expand-cycles]: parse,
/// expand, the STATIC run of the virtual machine, the diagnostics on the standard error in the form of D98, the exit
/// codes of D97; without --machine against the built-in default machine of D103 (architecture 10; P1-07).
/// </summary>
public sealed class CheckCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // D103, the phases table: every example checks with no ERROR without a machine file, so ncx check exits 0 (D97)
    // and writes nothing but the diagnostics.
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Check_Example_ExitsZero(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("check", file);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("", _cli.Output);
    }

    // VM 5, D100, D103: the command reports the diagnostics of the expected file of each example, the parser's, the
    // expander's and the virtual machine's in the order reported (P1-04, D98).
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void Check_Example_ReportsTheExpectedDiagnostics(string example)
    {
        string file = _cli.CopyExample(example);

        _cli.Run("check", file);

        CliHarness.AssertExpectedFile(
            Path.GetFileNameWithoutExtension(example) + ".check.txt", _cli.Error.Replace(file, example));
    }

    // D97, P1-07: the one WARNING of PATTERN_LOOP (its unresolved expressions) exits 1 under --strict and 0 without.
    [Fact]
    public void CheckStrict_PatternLoopWithItsWarning_ExitsOne()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        int strictExitCode = _cli.Run("check", file, "--strict");
        int exitCode = _cli.Run("check", file);

        Assert.Equal(1, strictExitCode);
        Assert.Equal(0, exitCode);
        Assert.Contains("WARNING VM540", _cli.Error, StringComparison.Ordinal);
    }

    // D97: without a WARNING --strict changes nothing.
    [Fact]
    public void CheckStrict_ExampleWithoutAWarning_ExitsZero()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("check", file, "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal("", _cli.Error);
    }

    // An ERROR of the parser stops the run and exits 1, the diagnostic on the standard error (D97, D98).
    [Fact]
    public void Check_UnknownKey_ExitsOneWithTheDiagnostic()
    {
        string file = _cli.WriteFile("unknown.ncx", CliHarness.Lines(
            "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"U\"", "LINE X=1 SPEED=5", "PROGRAM=END", "FILE=END"));

        int exitCode = _cli.Run("check", file);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{file}(3): ERROR PAR010: ", _cli.Error, StringComparison.Ordinal);
    }

    // An ERROR of the virtual machine exits 1: LINE without feed (VM 3.1, 5).
    [Fact]
    public void Check_LineWithoutFeed_ExitsOne()
    {
        string file = _cli.WriteFile("nofeed.ncx", CliHarness.OneProgram("UNITS=MM", "LINE X=10"));

        int exitCode = _cli.Run("check", file);

        Assert.Equal(1, exitCode);
        Assert.Contains($"{file}(4): ERROR ", _cli.Error, StringComparison.Ordinal);
    }

    // An input that cannot be read decides exit code 2 before the run starts (D97).
    [Fact]
    public void Check_MissingFile_ExitsTwoWithTheFileName()
    {
        string file = _cli.PathOf("missing.ncx");

        int exitCode = _cli.Run("check", file);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"{file}(1): ERROR CLI002: ", _cli.Error, StringComparison.Ordinal);
    }

    // D97, P1-07: a --machine that names a file that cannot be found is exit code 2, decided before the run starts.
    [Fact]
    public void CheckMachine_FileThatDoesNotExist_ExitsTwo()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string machine = _cli.PathOf("no-such-machine.toml");

        int exitCode = _cli.Run("check", file, "--machine", machine);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"{machine}(1): ERROR CLI100: ", _cli.Error, StringComparison.Ordinal);
    }

    // P1-07: a machine file that reads but loads with a CFG ERROR exits 1 like any other ERROR.
    [Fact]
    public void CheckMachine_FileWithAnError_ExitsOneWithItsDiagnostic()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string machine = _cli.WriteFile("broken.toml", TestMachines.WithoutController);

        int exitCode = _cli.Run("check", file, "--machine", machine);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{machine}(", _cli.Error, StringComparison.Ordinal);
        Assert.Contains(": ERROR CFG", _cli.Error, StringComparison.Ordinal);
    }

    // VM 3.8 rule 1, D103: an unknown role is the WARNING "not checked: no machine file" without a machine file and an
    // ERROR against a machine file given by path.
    [Fact]
    public void CheckMachine_UnknownRole_IsAnErrorWithTheMachineFileAndAWarningWithout()
    {
        string file = _cli.WriteFile("turret.ncx", CliHarness.OneProgram("UNITS=MM", "TOOL:TURRET1=1"));
        string machine = _cli.WriteFile("clutch.toml", TestMachines.ClutchMill);

        int withMachine = _cli.Run("check", file, "--machine", machine);
        string withMachineError = _cli.Error;
        int withoutMachine = _cli.Run("check", file);

        Assert.Equal(1, withMachine);
        Assert.Contains($"{file}(4): ERROR ", withMachineError, StringComparison.Ordinal);
        Assert.Equal(0, withoutMachine);
        Assert.Contains($"{file}(4): WARNING VM003: ", _cli.Error, StringComparison.Ordinal);
    }

    // A machine file given by path is what the run is checked against: the example that the default machine checks
    // clean checks clean against the clutch mill too (D103; machine-config 1 to 5a).
    [Fact]
    public void CheckMachine_MachineFileByPath_ChecksAgainstIt()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string machine = _cli.WriteFile("clutch.toml", TestMachines.ClutchMill);

        int exitCode = _cli.Run("check", file, "--machine", machine);

        Assert.True(exitCode == 0, _cli.Error);
    }

    // D53: SKIP blocks are executed by default, so the LINE without feed in a SKIP block is an ERROR.
    [Fact]
    public void CheckSkipBlocks_Default_ExecutesTheSkipBlock()
    {
        string file = _cli.WriteFile("skip.ncx", CliHarness.OneProgram("UNITS=MM", "SKIP LINE X=10"));

        int exitCode = _cli.Run("check", file);

        Assert.Equal(1, exitCode);
    }

    // D53: --skip-blocks all skips every SKIP block, so its LINE without feed is never executed.
    [Fact]
    public void CheckSkipBlocks_All_SkipsEverySkipBlock()
    {
        string file = _cli.WriteFile("skip.ncx",
            CliHarness.OneProgram("UNITS=MM", "SKIP LINE X=10", "SKIP=2 LINE Y=10"));

        int exitCode = _cli.Run("check", file, "--skip-blocks", "all");

        Assert.True(exitCode == 0, _cli.Error);
    }

    // D53, VM 3.6: skip_blocks = [1, 3] skips the blocks of the switches that are on and executes the others.
    [Theory]
    [InlineData("1,3", 0)]
    [InlineData("3", 1)]
    [InlineData("none", 1)]
    public void CheckSkipBlocks_SwitchList_SkipsTheBlocksOfItsSwitches(string skipBlocks, int expectedExitCode)
    {
        string file = _cli.WriteFile("skip.ncx", CliHarness.OneProgram("UNITS=MM", "SKIP=1 LINE X=10"));

        int exitCode = _cli.Run("check", file, "--skip-blocks", skipBlocks);

        Assert.True(exitCode == expectedExitCode, _cli.Error);
    }

    // --skip-blocks takes none, all or switch numbers 1 to 9 (language 4.1, D53); anything else is a usage error,
    // exit 2 before the run starts (D97).
    [Theory]
    [InlineData("some")]
    [InlineData("0")]
    [InlineData("1,10")]
    [InlineData("1,,3")]
    [InlineData("")]
    public void CheckSkipBlocks_ValueThatIsNoSwitchList_IsAUsageError(string skipBlocks)
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = _cli.Run("check", file, "--skip-blocks", skipBlocks);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _cli.Error, StringComparison.Ordinal);
    }

    // --expand-cycles is a run option of check as well (D37); the check of the drilling example stays as it was.
    [Fact]
    public void CheckExpandCycles_PatternLoop_ReportsWhatTheCheckReports()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        int exitCode = _cli.Run("check", file, "--expand-cycles");

        Assert.Equal(0, exitCode);
        CliHarness.AssertExpectedFile("PATTERN_LOOP.check.txt", _cli.Error.Replace(file, "PATTERN_LOOP.ncx"));
    }

    // The command line without a file is a usage error, exit 2 (D97).
    [Fact]
    public void Check_WithoutAFile_IsAUsageError()
    {
        int exitCode = _cli.Run("check");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _cli.Error, StringComparison.Ordinal);
    }

    // The help of check names the shared options (architecture 10, F28).
    [Fact]
    public void Check_Help_NamesTheSharedOptions()
    {
        int exitCode = _cli.Run("check", "--help");

        Assert.Equal(0, exitCode);
        foreach (string option in new[] { "--machine", "--strict", "--skip-blocks", "--expand-cycles" })
        {
            Assert.Contains(option, _cli.Output, StringComparison.Ordinal);
        }
    }
}
