using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The motion of the Fanuc compiler (controllers fanuc.md 3, 4, 10 rule 1; controller-mapping 2; language 4.3): arcs
/// with R or I J, a sweep split into turns, the plane, the compensation with D, the feed mode, G53.
/// </summary>
public sealed class FanucMotionTests
{
    // Controller-mapping 2, R: an arc with R keeps its signed radius; G2 or G3 stands on every arc block (the
    // TODO(question) of FanucMotion), as the sources write N300 to N330 of 2.5D_FRAESEN.
    [Fact]
    public void Arc_WithRadius_WritesR_AndTheArcCodeOnEveryBlock()
    {
        string body = FanucCompile.Body(
            "RAPID X=0 Y=0\nLINE X=10 F=100\nARC=CCW X=20 Y=10 R=10\nARC=CCW X=10 Y=20 R=-10");

        Assert.Equal("G17 X0. Y0.\nG1 X10. F100.\nG3 X20. Y10. R10.\nG3 X10. Y20. R-10.\n", body);
    }

    // Controller-mapping 2, CENTER:X; language 6: the absolute centre is written as I J from the start point, G3 X70.
    // Y50. I-.534 J-19.993 of 2.5D_FRAESEN.
    [Fact]
    public void Arc_WithAbsoluteCentre_WritesIJFromTheStartPoint()
    {
        string body = FanucCompile.Body(
            "RAPID X=50.534 Y=69.993\nARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50 F=100");

        Assert.Equal("G17 X50.534 Y69.993\nG3 X70. Y50. I-0.534 J-19.993 F100.\n", body);
    }

    // Controller-mapping 2, CENTER:IX: the incremental centre is I J as the block writes it.
    [Fact]
    public void Arc_WithIncrementalCentre_WritesIJAsWritten()
    {
        string body = FanucCompile.Body("RAPID X=10 Y=0\nARC=CW X=-10 Y=0 CENTER:IX=-10 CENTER:IY=0 F=50");

        Assert.Equal("G17 X10. Y0.\nG2 X-10. Y0. I-10. J0. F50.\n", body);
    }

    // Language 4.3 ANGLE, D84; controller-mapping 2: a sweep of 720 degrees and a rest becomes two full turns and the
    // rest, the travel of the tool axis shared out over the sweep.
    [Fact]
    public void Arc_WithASweepBeyond360_IsSplitIntoTurns()
    {
        string body = FanucCompile.Body(
            "RAPID X=10 Y=0 Z=0\nARC=CCW IZ=-5 CENTER:X=0 CENTER:Y=0 ANGLE=900 F=100");

        Assert.Equal(
            "G17 X10. Y0. Z0.\nG3 X10. Y0. Z-2. I-10. J0. F100.\nG3 X10. Y0. Z-4. I-10. J0.\n"
            + "G3 X-10. Y0. Z-5. I-10. J0.\n", body);
    }

    // Controllers fanuc.md 4: G41 and G42 apply from the motion of the same block with D, which stands again only
    // where the register changes; G40 ends the compensation (controller-mapping 2, COMP).
    [Fact]
    public void Comp_LeftWithRadiusOffset_WritesG41WithD()
    {
        string body = FanucCompile.Body(
            "TOOL=1\nRAPID X=0 Y=0\nLINE Y=2 OFFSET:RAD=1 COMP=LEFT F=100\nLINE Y=10 COMP=OFF\nLINE X=5 COMP=LEFT");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG1 G41 Y2. D1 F100.\nG40 Y10.\nG41 X5.\n", body);
    }

    // Controllers fanuc.md 3, controller-mapping 2: FEED_MODE is G94 or G95 where the control has the other active.
    [Fact]
    public void FeedMode_PerRevolution_IsG95InItsBlock()
    {
        string body = FanucCompile.Body("FEED_MODE=PER_REV\nRAPID X=0 Y=0\nLINE Z=-1 F=0.2");

        Assert.Equal("G95\nG17 X0. Y0.\nG1 Z-1. F0.2\n", body);
    }

    // Language 4.2, WORKPLANE: G18 stands in the first motion after it under plane_with_first_motion.
    [Fact]
    public void Workplane_ZX_IsG18InTheNextMotion()
    {
        string body = FanucCompile.Body("RAPID X=0 Y=0\nWORKPLANE=ZX\nRAPID Z=5");

        Assert.Equal("G17 X0. Y0.\nG18 Z5.\n", body);
    }

    // Controller-mapping 1, FRAME=MACHINE: G53 for the block, which moves at rapid (D242).
    [Fact]
    public void Rapid_InTheMachineFrame_WritesG53()
    {
        string body = FanucCompile.Body("RAPID X=0 Y=0\nRAPID Z=0 FRAME=MACHINE");

        Assert.Equal("G17 X0. Y0.\nG53 Z0.\n", body);
    }

    // D242: G53 moves at rapid, so a LINE in the machine frame has no Fanuc form.
    [Fact]
    public void Line_InTheMachineFrame_IsTheErrorCmp320()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=0 Y=0 Z=0", "LINE Z=-5 F=100 FRAME=MACHINE"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucFeedMotionInMachineFrame, error.Code);
    }

    // Controllers fanuc.md 3: G90 and G91 act on the whole block, so a block that mixes absolute and incremental words
    // is written absolute, from the point the virtual machine reached.
    [Fact]
    public void Line_MixingAbsoluteAndIncrementalWords_IsWrittenAbsolute()
    {
        string body = FanucCompile.Body("RAPID X=10 Y=10\nLINE X=20 IY=5 F=100");

        Assert.Equal("G17 X10. Y10.\nG1 X20. Y15. F100.\n", body);
    }

    // Language 4.1 DWELL, controller-mapping 1: G4 with P in milliseconds (D159).
    [Fact]
    public void Dwell_InSeconds_IsG4PInMilliseconds()
    {
        string body = FanucCompile.Body("DWELL=1.5");

        Assert.Equal("G4 P1500\n", body);
    }

    // Language 4.1 DWELL (seconds), controllers fanuc.md 4 and 7 (D159): an expression dwell is the expression times
    // 1000 in brackets, an operation in brackets of its own.
    [Fact]
    public void Dwell_AsAnExpression_IsG4PWithTheExpressionTimes1000()
    {
        string body = FanucCompile.Body("VAR:V1=2\nDWELL={$V1}\nDWELL={$V1 + 0.5}");

        Assert.Equal("#1 = 2\nG4 P[#1 * 1000]\nG4 P[[#1 + 0.5] * 1000]\n", body);
    }

    // Controller-mapping 1, SKIP; language 2 rules 2 and 3: the codes of a skipped block reach the control only while
    // the switch is off, so the next block writes G1 and the feed again, which the skipped block wrote.
    [Fact]
    public void Skip_LineAfterASkippedLine_WritesItsCodeAndFeedAgain()
    {
        string body = FanucCompile.Body("RAPID Z=2\nSKIP LINE Z=-3 F=50\nLINE X=5");

        Assert.Equal("G17 Z2.\n/G1 Z-3. F50.\nG1 X5. F50.\n", body);
    }

    // Controller-mapping 1, SKIP; language 2 rule 3: after a skipped G91 block the control may still have G90, so the
    // next incremental block writes G91 again.
    [Fact]
    public void Skip_IncrementalLineAfterASkippedOne_WritesG91Again()
    {
        string body = FanucCompile.Body("LINE X=0 F=100\nSKIP LINE IX=5\nLINE IX=5");

        Assert.Equal("G1 G17 X0. F100.\n/G91 X5.\nG91 X5.\n", body);
    }

    // Controller-mapping 1, SKIP; D99: a skipped CALL runs its subprogram only while the switch is off, so what the
    // subprogram wrote is unknown after it, and the LINE writes G1 and F again.
    [Fact]
    public void Skip_CallOfASubprogram_LeavesWhatTheSubprogramWroteUnknown()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "RAPID X=0 Y=0",
            "SKIP CALL=100",
            "LINE X=5 F=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "LINE Z=-1 F=100",
            "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.Contains("\nG17 X0. Y0.\n/M98 P0100\nG1 X5. F100.\nM30\n", text, StringComparison.Ordinal);
    }

    // Language 2 rule 8: a word no concern writes is never dropped silently.
    [Fact]
    public void Word_WithoutFanucForm_IsTheErrorCmp308()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("TOOL=1", "OFFSET=5"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucWordNotWritten, error.Code);
    }
}
