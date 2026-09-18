using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The canned cycles of the Fanuc compiler (controllers fanuc.md 6; controller-mapping 5; language 4.7): the
/// definition written with its first call, R and Z from the absolute planes, G98 and G99 from CYCLE_RETRACT, F where
/// the control's feed differs, the positions of the later calls, G80.
/// </summary>
public sealed class FanucCyclesTests
{
    // The drilling of BOHREN.fanuc.nc: tool, spindle, the position and the approach before the cycle.
    private const string Approach = "TOOL=1\nSPINDLE=CW RPM=1000\nRAPID X=10 Y=10\nRAPID Z=5";

    // Controller-mapping 5, fanuc 6: G81 G99 Z-21.732 R5. F565 defines and calls at its position; every later call is
    // its position alone; CYCLE=OFF is G80, after which the motion writes G0 again (fanuc 3, group 01).
    [Fact]
    public void Drill_DefinedAndCalled_IsG81WithRZAndFThenThePositions()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565\nCYCLE_CALL"
            + "\nCYCLE_CALL X=30\nCYCLE=OFF\nRAPID Z=50");

        Assert.Equal(
            "T1 M6\nS1000 M3\nG0 G17 X10. Y10.\nZ5.\nG81 G99 Z-21.732 R5. F565.\nX30.\nG80\nG0 Z50.\n", body);
    }

    // D218, BOHREN.fanuc.nc N170: the F of a cycle block is the control's feed, so a later cycle with the same CYCLE_F
    // writes none; PECK is Q.
    [Fact]
    public void Peck_WithTheFeedOfTheControl_WritesNoF()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=565\nCYCLE_CALL\nCYCLE=OFF"
            + "\nCYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=1.2 CYCLE_F=565\nCYCLE_CALL X=30\nCYCLE=OFF");

        Assert.EndsWith("G81 G99 Z-20. R5. F565.\nG80\nG83 G99 X30. Z-20. R5. Q1.2\nG80\n", body,
            StringComparison.Ordinal);
    }

    // Controller-mapping 5, CYCLE_RETRACT: SAFE is G98, the initial level where the drilling axis stood.
    [Fact]
    public void Retract_ToSafe_IsG98()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 SAFE=5 CYCLE_RETRACT=SAFE CYCLE_F=100\nCYCLE_CALL");

        Assert.EndsWith("G81 G98 Z-10. R2. F100.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5: G98 returns to the initial level, so a SAFE plane elsewhere is a WARNING.
    [Fact]
    public void Retract_ToSafeAwayFromTheInitialLevel_IsAWarningCmp382()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 SAFE=20 CYCLE_RETRACT=SAFE CYCLE_F=100\nCYCLE_CALL").Split('\n')),
            FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucSafeIsNotTheInitialLevel);
    }

    // Fanuc 6: a position block under an active G81 drills, so a motion after a call without CYCLE=OFF writes G80
    // first.
    [Fact]
    public void Motion_AfterACallWithoutCycleOff_EndsTheCycleWithG80()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=100\nCYCLE_CALL\nRAPID Z=50");

        Assert.EndsWith("G81 G99 Z-20. R5. F100.\nG80\nG0 Z50.\n", body, StringComparison.Ordinal);
    }

    // BOHREN.fanuc.nc N3100 and N3110, controller-mapping 5 TAP: M29 S500 before G84 G99 Z-20. R5. P0 F750.
    [Fact]
    public void Tap_WithDwellAndFeed_IsG84WithPAndF()
    {
        string body = FanucCompile.Body(Approach
            + "\nRPM=500 FUNC:RIGID_TAP=ON\nCYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE "
            + "CYCLE_F=750 CYCLE_DWELL=0 PITCH=1.5\nCYCLE_CALL");

        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. P0 F750.\n", body, StringComparison.Ordinal);
    }

    // Language 4.7 CYCLE_DWELL (seconds), controllers fanuc.md 6 (P in milliseconds) and 7: an expression dwell is P
    // with the expression times 1000.
    [Fact]
    public void CycleDwell_AsAnExpression_IsPWithTheExpressionTimes1000()
    {
        string body = FanucCompile.Body(Approach
            + "\nVAR:V1=2\nCYCLE=DRILL_DWELL CLEARANCE=5 DEPTH=-20 CYCLE_DWELL={$V1} CYCLE_F=100\nCYCLE_CALL");

        Assert.EndsWith("#1 = 2\nG82 G99 Z-20. R5. P[#1 * 1000] F100.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP: without CYCLE_F the feed of G84 is the pitch times the speed per minute.
    [Fact]
    public void Tap_WithPitchOnly_FeedsThePitchTimesTheSpeed()
    {
        string body = FanucCompile.Body(Approach + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 PITCH=1.5\nCYCLE_CALL");

        Assert.EndsWith("G84 G99 Z-20. R5. F1500.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP, controllers fanuc.md 7: a pitch from an expression feeds with the expression times the
    // known speed, the speed a constant of the expression.
    [Fact]
    public void Tap_WithAPitchExpression_FeedsTheExpressionTimesTheSpeed()
    {
        string body = FanucCompile.Body(
            Approach + "\nVAR:V2=1.5\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 PITCH={$V2}\nCYCLE_CALL");

        Assert.EndsWith("#2 = 1.5\nG84 G99 Z-20. R5. F[#2 * 1000]\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP, language 2 rule 8: the feed of G84 is the pitch times the speed, and a speed from an
    // expression is not known in STATIC mode (virtual machine 1), so the pitch cannot be written and is an ERROR.
    [Fact]
    public void Tap_WithPitchAndASpeedFromAnExpression_IsTheErrorCmp386()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program(
            "VAR:V1=500", "TOOL=1", "SPINDLE=CW RPM={$V1}", "RAPID X=10 Y=10", "RAPID Z=4",
            "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PITCH=1.5", "CYCLE_CALL"),
            FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucTapFeedNotKnown, error.Code);
    }

    // D99, fanuc 6 (a position block under an active G81 drills; the TODO(question) of D247): a subprogram is written
    // from an unknown target state, so G80 stands before its first motion, and after its return the caller writes the
    // definition of the cycle again with its next call.
    [Fact]
    public void Call_OfASubprogramBetweenTwoCycleCalls_EndsTheCycleAndDefinesItAgain()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "TOOL=1",
            "SPINDLE=CW RPM=1000",
            "RAPID X=0 Y=0 Z=5",
            "CYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100",
            "CYCLE_CALL X=5 Y=5",
            "CALL=100",
            "CYCLE_CALL X=10 Y=5",
            "CYCLE=OFF",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "RAPID X=20 Y=20",
            "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.Contains("\nG81 G99 X5. Y5. Z-10. R2. F100.\nM98 P0100\nG81 G99 X10. Y5. Z-10. R2.\nG80\nM30\n"
            + "O0100\nG80\nG0 G17 G90 X20. Y20.\nM99\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 6, D59, D160: a lathe drills along X with G87, without G98 and G99 in system A, whose X
    // stays a diameter.
    [Fact]
    public void Drill_AlongXOnALatheOfSystemA_IsG87()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=60 Z=-10",
            "CYCLE=DRILL AXIS=X CLEARANCE=54 DEPTH=30 CYCLE_F=50",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Contains("\nG87 X30. R54. F50.\nG80\n", text, StringComparison.Ordinal);
    }

    // Language 4.7.1, controller-mapping 5: the simple turning cycle G90 of system A is modal, one call per block.
    [Fact]
    public void TurnOd_OnALatheOfSystemA_IsG90WithOneCallPerBlock()
    {
        const string TurnOd =
            "[[cycle]]\nname = \"TURN_OD\"\nnative = \"G90\"\nmodal = true\nparams = { CYCLE_F = \"F\" }";
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3",
            "CYCLE_CALL X=60 Z=-70",
            "CYCLE_CALL X=50 Z=-70",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program,
            FanucCompile.Lathe(FanucCompile.DefaultSync, TurnOd)));

        Assert.Contains("\nG90 X60. Z-70. F0.3\nX50. Z-70.\nG80\n", text, StringComparison.Ordinal);
    }
}
