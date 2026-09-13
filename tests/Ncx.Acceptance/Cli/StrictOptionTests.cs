namespace Ncx.Acceptance.Cli;

/// <summary>
/// Every command accepts --strict, under which a WARNING sets the exit code 1 (architecture 10, D97); an INFO never
/// changes it (virtual machine 2.9).
/// </summary>
public sealed class StrictOptionTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // The mixed line endings are a WARNING of the parser (PAR001): format exits 0 without --strict and 1 with it, and
    // writes the canonical text either way (D97; P0-06: --strict arrives with P1-07 and applies to format as well).
    [Fact]
    public void FormatStrict_Warning_ExitsOneAndStillWrites()
    {
        string file = _cli.WriteFile("mixed.ncx",
            "FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\nPROGRAM=END\r\nFILE=END\r\n");

        int exitCode = _cli.Run("format", file);
        int strictExitCode = _cli.Run("format", file, "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.Equal("FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\r\nPROGRAM=END\r\nFILE=END\r\n", _cli.Output);
        Assert.Contains("WARNING PAR001", _cli.Error, StringComparison.Ordinal);
    }

    // A canonical file without a WARNING exits 0 under --strict.
    [Fact]
    public void FormatStrict_CanonicalExample_ExitsZero()
    {
        string file = _cli.CopyExample("POLAR_FACE.ncx");

        int exitCode = _cli.Run("format", file, "--check", "--strict");

        Assert.Equal(0, exitCode);
    }

    // The INFO of format --check names the first line that differs; the difference exits 1, the INFO itself changes
    // nothing, also under --strict (virtual machine 2.9, D98).
    [Fact]
    public void FormatStrict_InfoOfADifference_IsNoWarning()
    {
        string file = _cli.WriteFile("canonical.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _cli.Run("format", file, "--check", "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal("", _cli.Error);
    }

    // trace and annotate exit 1 under --strict on the WARNING of PATTERN_LOOP and write their output all the same:
    // the run has ended (D97).
    [Theory]
    [InlineData("trace")]
    [InlineData("annotate")]
    public void HistoryStrict_PatternLoopWithItsWarning_ExitsOneAndWrites(string command)
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        int exitCode = _cli.Run(command, file);
        int strictExitCode = _cli.Run(command, file, "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.NotEqual("", _cli.Output);
        Assert.Contains("WARNING VM540", _cli.Error, StringComparison.Ordinal);
    }

    // A WARNING of the machine file counts under --strict like any other (D97): an unknown key of [machine] is a CFG
    // WARNING that names the nearest known key (P2-01).
    [Fact]
    public void CheckStrict_WarningOfTheMachineFile_ExitsOne()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string machine = _cli.WriteFile("clutch.toml", TestMachines.ClutchMillWithUnknownKey);

        int exitCode = _cli.Run("check", file, "--machine", machine);
        int strictExitCode = _cli.Run("check", file, "--machine", machine, "--strict");

        Assert.Equal(0, exitCode);
        Assert.Equal(1, strictExitCode);
        Assert.StartsWith($"{machine}(", _cli.Error, StringComparison.Ordinal);
        Assert.Contains(": WARNING CFG", _cli.Error, StringComparison.Ordinal);
    }

    // The help of format names --strict (architecture 10: every command accepts it).
    [Fact]
    public void Format_Help_NamesStrict()
    {
        int exitCode = _cli.Run("format", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--strict", _cli.Output, StringComparison.Ordinal);
    }
}
