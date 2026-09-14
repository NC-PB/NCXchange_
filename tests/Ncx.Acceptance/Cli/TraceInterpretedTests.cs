using Ncx.Acceptance.Examples;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx trace --interpreted [--vars &lt;toml&gt;]: the trace of an INTERPRETED run, the variables evaluated and the flow
/// followed (virtual machine 1, 3.6, 6), with the start values of the vars file (virtual machine 2.7; machine-config 8,
/// 10) and the exit codes of D97 (implementation 14, P4-01).
/// </summary>
public sealed class TraceInterpretedTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // VM 1, 3.6, D103: every example runs INTERPRETED without an ERROR and without a machine file, and trace writes its
    // table.
    [Theory]
    [MemberData(nameof(ExampleCheckTests.Examples), MemberType = typeof(ExampleCheckTests))]
    public void TraceInterpreted_Example_ExitsZeroAndWritesTheTable(string example)
    {
        string file = _cli.CopyExample(example);

        int exitCode = _cli.Run("trace", file, "--interpreted");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.StartsWith("channel  block  variable", _cli.Output, StringComparison.Ordinal);
    }

    // PATTERN_LOOP note 1, implementation 14 P4-01: the trace of the INTERPRETED run as an expected file.
    [Fact]
    public void TraceInterpreted_PatternLoop_EqualsTheExpectedFile()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        _cli.Run("trace", file, "--interpreted");

        CliHarness.AssertExpectedFile("PATTERN_LOOP.trace-interpreted.txt", _cli.Output);
        Assert.Equal("", _cli.Error);
    }

    // PATTERN_LOOP note 1: the loop runs five times with the call positions X = 10 (from the RAPID of line 13), 30, 50,
    // 70 and 90, and the trace shows Q1 and Q3 in every pass.
    [Fact]
    public void TraceInterpretedCsv_PatternLoop_ShowsTheCallPositionsAndQ1AndQ3InEveryPass()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--format", "csv");

        Assert.Equal(0, exitCode);
        Assert.Equal(["1,13,X,,10", "1,17,X,10,30", "1,17,X,30,50", "1,17,X,50,70", "1,17,X,70,90"],
            RowsOf(",X,"));
        Assert.Equal(
            ["1,8,VAR:Q1,,10", "1,18,VAR:Q1,10,30", "1,18,VAR:Q1,30,50", "1,18,VAR:Q1,50,70", "1,18,VAR:Q1,70,90",
                "1,18,VAR:Q1,90,110"],
            RowsOf(",VAR:Q1,"));
        Assert.Equal(
            ["1,10,VAR:Q3,,0", "1,19,VAR:Q3,0,1", "1,19,VAR:Q3,1,2", "1,19,VAR:Q3,2,3", "1,19,VAR:Q3,3,4",
                "1,19,VAR:Q3,4,5"],
            RowsOf(",VAR:Q3,"));
    }

    // Implementation 14 P4-01: INCREMENTAL_SUB calls the subprogram four times and ends at the expected position,
    // X = 0, Y = 60, Z = 50 before the parking section's HOME; the trace of the INTERPRETED run as an expected file.
    [Fact]
    public void TraceInterpreted_IncrementalSub_EqualsTheExpectedFile()
    {
        string file = _cli.CopyExample("INCREMENTAL_SUB.ncx");

        _cli.Run("trace", file, "--interpreted");

        CliHarness.AssertExpectedFile("INCREMENTAL_SUB.trace-interpreted.txt", _cli.Output);
    }

    // VM 2.7, 3.6; machine-config 8: --vars names the vars file with the start values of the variables.
    [Fact]
    public void TraceInterpreted_VarsOption_GivesTheStartValues()
    {
        string file = _cli.WriteFile("start.ncx", CliHarness.OneProgram("VAR:Q2={$Q1 + 1}"));
        string vars = _cli.WriteFile("values.toml", "Q1 = 4\n");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--vars", vars, "--format", "csv");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Contains("1,3,VAR:Q2,,5", _cli.Output.Split('\n'));
    }

    // VM 3.6, machine-config 10: without --vars, the vars file of the file, <file>.vars.toml next to it, gives the
    // start values when there is one.
    [Fact]
    public void TraceInterpreted_VarsFileNextToTheFile_IsReadWithoutTheOption()
    {
        string file = _cli.WriteFile("loop.ncx", CliHarness.OneProgram("VAR:Q2={$Q1 + 1}"));
        _cli.WriteFile("loop.vars.toml", "Q1 = 4\n");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--format", "csv");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Contains("1,3,VAR:Q2,,5", _cli.Output.Split('\n'));
    }

    // VM 2.7, 3.6, wave-1 question #89: a register the virtual machine does not hold is read from the vars file, whose
    // key is the name as the program writes it, with its index.
    [Fact]
    public void TraceInterpreted_RegisterFromTheVarsFile_IsRead()
    {
        string file = _cli.WriteFile("wear.ncx", CliHarness.OneProgram("VAR:Q1={$SYS_WEAR_Z[99]}"));
        string vars = _cli.WriteFile("wear.toml", "\"SYS_WEAR_Z[99]\" = 0.012\n");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--vars", vars, "--format", "csv");

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Contains("1,3,VAR:Q1,,0.012", _cli.Output.Split('\n'));
    }

    // VM 2.7, 3.6, phase 4 risks: without a vars file a register the virtual machine does not hold is an ERROR, whose
    // message names the vars file as the way out; exit code 1 (D97).
    [Fact]
    public void TraceInterpreted_RegisterWithoutAVarsFile_IsAnErrorNamingTheVarsFile()
    {
        string file = _cli.WriteFile("wear.ncx", CliHarness.OneProgram("VAR:Q1={$SYS_WEAR_Z[99]}"));

        int exitCode = _cli.Run("trace", file, "--interpreted");

        Assert.Equal(1, exitCode);
        Assert.Contains("ERROR VM903", _cli.Error, StringComparison.Ordinal);
        Assert.Contains("<file>.vars.toml", _cli.Error, StringComparison.Ordinal);
    }

    // VM 3.6, D97: the start values are those of an INTERPRETED run; --vars without --interpreted is a usage error.
    [Fact]
    public void Trace_VarsWithoutInterpreted_IsAUsageError()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");
        string vars = _cli.WriteFile("values.toml", "Q1 = 4\n");

        int exitCode = _cli.Run("trace", file, "--vars", vars);

        Assert.Equal(2, exitCode);
        Assert.Contains("ERROR CLI001", _cli.Error, StringComparison.Ordinal);
        Assert.Contains("--interpreted", _cli.Error, StringComparison.Ordinal);
    }

    // D97: a vars file that cannot be read decides exit code 2 before the run starts, and trace writes no table.
    [Fact]
    public void TraceInterpreted_VarsFileThatCannotBeRead_ExitsTwo()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--vars", _cli.PathOf("missing.toml"));

        Assert.Equal(2, exitCode);
        Assert.Contains("ERROR CLI101", _cli.Error, StringComparison.Ordinal);
        Assert.Equal("", _cli.Output);
    }

    // D97, machine-config 8: a vars file with an ERROR stops the run before it starts, exit code 1, the diagnostic on
    // the vars file.
    [Fact]
    public void TraceInterpreted_VarsFileWithAnError_ExitsOneWithTheDiagnosticOfTheVarsFile()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");
        string vars = _cli.WriteFile("values.toml", "Q1 = [1, 2]\n");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--vars", vars);

        Assert.Equal(1, exitCode);
        Assert.StartsWith(vars + "(1): ERROR", _cli.Error, StringComparison.Ordinal);
        Assert.Equal("", _cli.Output);
    }

    // Machine-config 7, VM 3.6, 5: the block cap of the machine file stops an endless loop, LABEL=1 and JUMP=1, with
    // the ERROR "possible endless loop"; exit code 1 (D97).
    [Fact]
    public void TraceInterpreted_BlockCapOfTheMachineFile_StopsAnEndlessLoop()
    {
        string file = _cli.WriteFile("endless.ncx", CliHarness.OneProgram("LABEL=1", "JUMP=1"));
        string machine = _cli.WriteFile("capped.toml", TestMachines.ClutchMill + "\n\n[variables]\nblock_cap = 100\n");

        int exitCode = _cli.Run("trace", file, "--interpreted", "--machine", machine);

        Assert.Equal(1, exitCode);
        Assert.Contains("ERROR VM750", _cli.Error, StringComparison.Ordinal);
        Assert.Contains("possible endless loop", _cli.Error, StringComparison.Ordinal);
    }

    // VM 3.6: an external program that the working directory does not hold is a missing call target: ERROR.
    [Fact]
    public void TraceInterpreted_ExternalProgramTheWorkingDirectoryDoesNotHold_IsAnError()
    {
        string file = _cli.WriteFile("caller.ncx", CliHarness.OneProgram("CALL=\"NCX_P401_NO_SUCH_PROGRAM\""));

        int exitCode = _cli.Run("trace", file, "--interpreted");

        Assert.Equal(1, exitCode);
        Assert.Contains("ERROR VM752", _cli.Error, StringComparison.Ordinal);
    }

    // The CSV rows of the last run that contain a text, in order.
    private List<string> RowsOf(string text)
    {
        var rows = new List<string>();
        foreach (string line in _cli.Output.Split('\n'))
        {
            if (line.Contains(text, StringComparison.Ordinal))
            {
                rows.Add(line);
            }
        }

        return rows;
    }
}
