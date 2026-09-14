using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// Each SUB section written once, not once per CALL, from an unknown target state; walks that write different lines
/// are an ERROR; under program_layout = "file_per_program" once per calling program (virtual machine 3.9, D99;
/// language 4.13; machine-config 2, D48).
/// </summary>
public sealed class SubprogramTests
{
    // One program that calls subprogram 100 twice from the same feed.
    private static readonly string s_twoCalls = FakeCompile.Lines(
        "FILE=BEGIN NCX=1",
        "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
        "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
        "RAPID X=0 Y=0 Z=5",
        "LINE Z=2 F=100",
        "CALL=100",
        "RAPID X=50",
        "CALL=100",
        "PROGRAM=END",
        "SUB=BEGIN NAME=100",
        "LINE X=10",
        "RAPID Z=5",
        "SUB=END",
        "FILE=END");

    // Virtual machine 3.9, D99; language 4.13: the section is written once, after the program that calls it; it is
    // written from an unknown target state, so G1 and the feed stand at their first use inside it; after the return
    // the control has active what the section wrote, so the RAPID of the caller writes no G0.
    [Fact]
    public void Sub_CalledTwice_IsWrittenOnceAfterTheProgram()
    {
        string text = FakeCompile.TextOf(FakeCompile.Run(s_twoCalls, FakeMachines.Mill()));

        Assert.Equal(
            "%\nO1 (T)\nN10 G0 X0. Y0. Z5.\nN20 G1 Z2. F100.\nN30 M98 P100\nN40 X50.\nN50 M98 P100\nN60 M30\n"
            + "O100\nN70 G1 X10. F100.\nN80 G0 Z5.\nN90 M99\n%\n",
            text);
    }

    // Virtual machine 3.9, D99: two walks that would write different lines are an ERROR naming the section and the
    // calls, here the feed the section takes from two callers.
    [Fact]
    public void Sub_WalksThatWriteDifferentLines_IsTheErrorCmp002NamingTheCalls()
    {
        string program = FakeCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
            "RAPID X=0 Y=0 Z=5",
            "LINE Z=2 F=100",
            "CALL=100",
            "LINE Z=2 F=200",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "LINE X=10",
            "SUB=END",
            "FILE=END");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill());

        Assert.Empty(result.Files);
        Diagnostic error = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.SubprogramWalksDiffer, error.Code);
        Assert.Equal(8, error.Line);
        Assert.Equal(
            "SUB 100 is written once for every caller, but its walk from the CALL on line 8 writes \"G1 X10. F200.\" "
            + "where its walk from the CALL on line 6 writes \"G1 X10. F100.\": the text of a word that depends on "
            + "the caller cannot stand in the section (virtual machine 3.9, D99).",
            error.Message);
    }

    // Machine-config 2, D48; language 4.13, D99: under "file_per_program" every program has a file of its own, named
    // after it, with the subprograms it calls copied after its end.
    [Fact]
    public void FilePerProgram_SubCalledByTwoPrograms_StandsInTheFileOfEach()
    {
        string program = FakeCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"A\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
            "CALL=100",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"B\" NUMBER=2",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "RAPID X=10 Y=0 Z=5",
            "SUB=END",
            "FILE=END");
        string machine = FakeMachines.Mill(format: FakeMachines.PointFormat.Replace(
            "\"one_file\"", "\"file_per_program\"", StringComparison.Ordinal));

        CompileResult result = FakeCompile.Run(program, machine);

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(2, result.Files.Count);
        Assert.Equal("A.nc", result.Files[0].Name);
        Assert.Equal("%\nO1 (A)\nN10 M98 P100\nN20 M30\nO100\nN30 G0 X10. Y0. Z5.\nN40 M99\n%\n", result.Files[0].Text);
        Assert.Equal("B.nc", result.Files[1].Name);
        Assert.Equal("%\nO2 (B)\nN10 M98 P100\nN20 M30\nO100\nN30 G0 X10. Y0. Z5.\nN40 M99\n%\n", result.Files[1].Text);
    }

    // D99 and the TODO(question) of OneFile: a subprogram that no program calls is walked once from the default entry
    // state and written after the last program.
    [Fact]
    public void OneFile_SubThatNoProgramCalls_FollowsTheLastProgram()
    {
        string program = FakeCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
            "RAPID X=0 Y=0 Z=5",
            "PROGRAM=END",
            "SUB=BEGIN NAME=200",
            "RAPID Z=50",
            "SUB=END",
            "FILE=END");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill()));

        Assert.Equal("%\nO1 (T)\nN10 G0 X0. Y0. Z5.\nN20 M30\nO200\nN30 G0 Z50.\nN40 M99\n%\n", text);
    }

    // D99 and the TODO(question) of FilePerProgram: under "file_per_program" a subprogram that no program calls stands
    // in no file, with a WARNING.
    [Fact]
    public void FilePerProgram_SubThatNoProgramCalls_IsTheWarningCmp003()
    {
        string program = FakeCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
            "PROGRAM=END",
            "SUB=BEGIN NAME=200",
            "RAPID Z=50",
            "SUB=END",
            "FILE=END");
        string machine = FakeMachines.Mill(format: FakeMachines.PointFormat.Replace(
            "\"one_file\"", "\"file_per_program\"", StringComparison.Ordinal));

        CompileResult result = FakeCompile.Run(program, machine);

        Assert.Equal("%\nO1 (T)\nN10 M30\n%\n", Assert.Single(result.Files).Text);
        Diagnostic warning = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.UncalledSubprogramNotWritten, warning.Code);
        Assert.Equal(5, warning.Line);
    }
}
