using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// One test per rule of controllers fanuc.md 10, the rules for the writer.
/// </summary>
public sealed class FanucWriterRulesTests
{
    // Fanuc 10 rule 1: G0 and G1 are written only when the verb changes.
    [Fact]
    public void Rule1_G0AndG1_AreWrittenOnlyWhenTheVerbChanges()
    {
        string body = FanucCompile.Body("RAPID X=1 Y=1\nRAPID Z=2\nLINE Z=-1 F=100\nLINE X=5\nRAPID Z=2");

        Assert.Equal("G17 X1. Y1.\nZ2.\nG1 Z-1. F100.\nX5.\nG0 Z2.\n", body);
    }

    // Fanuc 10 rule 1: G90 is written once in the header, and the words of an NCX block with incremental words are
    // written in a G91 block; the next absolute block writes G90 again.
    [Fact]
    public void Rule1_IncrementalWords_AreWrittenInAG91Block()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=0 Y=0", "LINE IX=30 F=800", "RAPID X=0"), FanucCompile.Mill("")));

        Assert.Equal(
            "%\nO0001 (T)\nG90\nG17 G94 G40 G80\nG0 X0. Y0.\nG1 G91 X30. F800.\nG0 G90 X0.\nM30\n%\n", text);
    }

    // Fanuc 10 rule 2, controller-mapping 4: M3 and S stand in one block; a speed set while the spindle stands waits
    // for its start, a speed of the running spindle is written in its block.
    [Fact]
    public void Rule2_SpeedAndDirection_StandInOneBlock()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000\nRPM=1500\nSPINDLE=OFF\nRPM=800\nSPINDLE=CW");

        Assert.Equal("S1000 M3\nS1500\nM5\nS800 M3\n", body);
    }

    // Fanuc 10 rule 2, fanuc 5: S belongs to the spindle whose M code stands in its block, so the speed of the main
    // spindle after the code of the driven tool is written with the main spindle's M code, never alone.
    [Fact]
    public void Rule2_SpeedAfterTheCodeOfAnotherSpindle_CarriesTheCodeOfItsSpindle()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "SPINDLE:TOOL=CW RPM:TOOL=1000",
            "RPM:MAIN=600",
            "SPINDLE:TOOL=OFF",
            "SPINDLE:MAIN=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Contains("S500 M3\nS1000 M88\nS600 M3\nM90\nM5\n", text, StringComparison.Ordinal);
    }

    // Fanuc 10 rule 3, D49: the program end is the program_end of the machine; the subprograms follow it as O programs
    // ending with M99, each once however often it is called, written from an unknown target state so that every modal
    // code stands at its first use inside it (D99), G80 before its first motion, since a caller may have left a canned
    // cycle active (the TODO(question) of D247).
    [Fact]
    public void Rule3_ProgramEndAndSubprograms_StandAfterTheEndAsOPrograms()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "RAPID X=0 Y=0 Z=2",
            "CALL=100 TIMES=2",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "LINE IZ=-3 F=200",
            "RAPID IZ=3",
            "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill(extra: "")
            .Replace("program_end = \"M30\"", "program_end = \"M2\"", StringComparison.Ordinal)));

        Assert.Equal(
            "%\nO0001 (T)\nG0 G40\nG80 G90 G94 G98\nG17 X0. Y0. Z2.\nM98 P0100 L2\nM98 P0100\nM2\n"
            + "O0100\nG80\nG1 G17 G91 G94 Z-3. F200.\nG0 Z3.\nM99\n%\n", text);
    }

    // Fanuc 10 rule 4, fanuc 8: a wait mark is the wait template of [sync] with the participating paths in the form
    // the machine uses, a path list P12 or a bitmask P3.
    [Theory]
    [InlineData("list", "M110 P12")]
    [InlineData("bitmask", "M110 P3")]
    public void Rule4_WaitMark_UsesThePathFormOfTheMachine(string paths, string expected)
    {
        string sync = "[sync]\nwait = \"M{mark} P{paths}\"\npaths = \"" + paths + "\"\nmark_range = [100, 199]";
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1 CHANNEL=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SYNC=110 WITH=1,2",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe(sync)));

        Assert.Contains("\n" + expected + "\n", text, StringComparison.Ordinal);
    }

    // Fanuc 10 rule 5: the decimal point stands on every real number per [format], X70. and Y-0.534, and a comment
    // stands in parentheses, transliterated to ASCII.
    [Fact]
    public void Rule5_RealNumbersAndComments_FollowTheFormat()
    {
        string body = FanucCompile.Body("RAPID X=70 Y=-0.534\nCOMMENT=\"ÄNDERUNG\"");

        Assert.Equal("G17 X70. Y-0.534\n(AENDERUNG)\n", body);
    }

    // Fanuc 10 rule 5, fanuc 2 (without calculator-type input R30 is 0.03): the angle of G68, the tool vector of G43.5
    // and the numbers the compiler gives a template carry the point as well, whether [format] decimals lists their
    // address or not.
    [Fact]
    public void Rule5_AnglesVectorsAndTemplateValues_CarryTheDecimalPoint()
    {
        string body = FanucCompile.Body("ROTATE=30\nROTATE=RESET\nCYLINDER=30\nCYLINDER=OFF\nTOOL=1 OFFSET:LEN=1"
            + "\nTCPM=ON\nLINE X=1 Y=2 Z=3 TX=0 TY=0 TZ=1 F=100\nTCPM=OFF");

        Assert.Equal("G68 X0 Y0 R30.\nG69\nG7.1 C30.\nG7.1 C0\nT1 M6\nG43.5 H1\n"
            + "G1 G17 X1. Y2. Z3. I0. J0. K1. F100.\nG49\nG43 H1\n", body);
    }

    // Fanuc 10 rule 6, machine-config 2: block numbers per block_numbers, the % and O lines and the comments without.
    [Fact]
    public void Rule6_BlockNumbers_FollowBlockNumbersOfTheFormat()
    {
        string machine = FanucCompile.Mill().Replace("block_numbers = { enabled = false }",
            "block_numbers = { enabled = true, start = 10, step = 10 }", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("SECTION=\"SIDE MILL\"", "RAPID X=1 Y=1"), machine));

        Assert.Equal("%\nO0001 (T)\nN10 G0 G40\nN20 G80 G90 G94 G98\n(SIDE MILL)\nN30 G17 X1. Y1.\nN40 M30\n%\n",
            text);
    }

    // Fanuc 10 rule 5, machine-config 2: a program's name after O stands in the comment charset of the machine.
    [Fact]
    public void Rule5_ProgramName_IsTheCommentAfterO()
    {
        string program = FanucCompile.Program("RAPID X=1 Y=1").Replace("NAME=\"T\"", "NAME=\"FRÄSEN\"",
            StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(program, FanucCompile.Mill());

        Assert.StartsWith("%\nO0001 (FRAESEN)\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    // Controllers fanuc.md 1: a program is named by its O number; a program without NUMBER has none (the
    // TODO(question) of FanucProgramFrame).
    [Fact]
    public void Rule3_ProgramWithoutNumber_IsTheErrorCmp300()
    {
        string program = FanucCompile.Program("RAPID X=1 Y=1").Replace(" NUMBER=1", "", StringComparison.Ordinal);

        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucProgramWithoutNumber, error.Code);
    }
}
