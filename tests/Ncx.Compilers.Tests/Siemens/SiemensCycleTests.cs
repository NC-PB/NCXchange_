using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 5 of controllers siemens.md 12 and the cycles of the Siemens column of controller-mapping 5: the full signature
/// of the catalog entry, MCALL for the calls at positions, MCALL alone to end them, a direct call at the current
/// position, CYCLE_F as the modal F before the call, _AXN for AXIS, RTP for CYCLE_RETRACT.
/// </summary>
public sealed class SiemensCycleTests
{
    // Siemens 12 rule 5: the cycle with its signature from the catalog, RTP = CLEARANCE for CYCLE_RETRACT=CLEARANCE,
    // RFP the surface, SDIS the distance of the clearance from it, DP the depth; MCALL for the calls at positions,
    // which the position blocks repeat; the modal F before the MCALL is the CYCLE_F; MCALL alone for CYCLE=OFF
    // (controller-mapping 5, DRILL, CYCLE_CALL, CYCLE_F, CYCLE=OFF).
    [Fact]
    public void Drill_CalledAtTwoPositions_IsMcallWithTheSignatureThenThePositions()
    {
        Assert.Equal(Lines("F120", "MCALL CYCLE81(5,0,5,-20)", "G0 X10 Y10", "X30", "MCALL"),
            MillBody("CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=120", "CYCLE_CALL X=10 Y=10",
                "CYCLE_CALL X=30", "CYCLE=OFF"));
    }

    // Controller-mapping 5, CYCLE_F: the cycle's own feed never touches the motion feed of the program, so the program
    // feed stands again at the next feed motion (D29).
    [Fact]
    public void CycleFeed_AfterTheCycle_TheProgramFeedIsWrittenAgain()
    {
        Assert.Equal(Lines("G1 X0 F500", "F120", "MCALL CYCLE81(5,0,5,-20)", "G0 X10", "MCALL", "G1 X20 F500"),
            MillBody("LINE X=0 F=500", "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=120",
                "CYCLE_CALL X=10", "CYCLE=OFF", "LINE X=20"));
    }

    // Siemens 7: every block with a position calls the modal cycle, so a motion that is no call ends the MCALL first,
    // and the next call arms it again.
    [Fact]
    public void MotionBetweenCalls_EndsTheModalCallFirstAndTheNextCallArmsItAgain()
    {
        Assert.Equal(Lines("F120", "MCALL CYCLE81(5,0,5,-20)", "G0 X10", "MCALL", "G0 Z50",
                "MCALL CYCLE81(5,0,5,-20)", "X30", "MCALL"),
            MillBody("CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=120", "CYCLE_CALL X=10", "RAPID Z=50",
                "CYCLE_CALL X=30", "CYCLE=OFF"));
    }

    // Siemens 7: every block with a position calls the modal cycle, one in the text of a RAW line too, so MCALL alone
    // ends it before the RAW line; the motion after it writes its modal codes again (language 4.1, RAW).
    [Fact]
    public void ModalCall_ArmedBeforeARawLine_IsEndedBeforeIt()
    {
        Assert.Equal(Lines("MCALL CYCLE81(2,0,2,-10)", "G0 X20 Y10", "MCALL", "STOPRE", "G0 G90 Z50"),
            MillBody("CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10", "CYCLE_CALL X=20 Y=10", "RAW:SIEMENS=\"STOPRE\"",
                "RAPID Z=50"));
    }

    // Siemens 7 and 8: a RAW line that names MCALL may arm a modal call the compiler does not know, so the next motion
    // that is no call ends it with MCALL alone (controller-mapping 5, CYCLE=OFF).
    [Fact]
    public void RawLine_ThatNamesMcall_IsFollowedByMcallAloneBeforeTheNextMotion()
    {
        Assert.Equal(Lines("MCALL L706", "MCALL", "G0 G90 X10", "G0 X20"),
            MillBody("RAW:SIEMENS=\"MCALL L706\"", "RAPID X=10", "RAPID X=20"));
    }

    // Siemens 7 and virtual machine 1: the STATIC walk does not follow the jump back, which arrives at the label
    // without the modal call that the block before the label armed, so the call at a position after the label arms it
    // again (controller-mapping 5, CYCLE_CALL).
    [Fact]
    public void ModalCall_ArmedBeforeALoop_IsArmedAgainAfterTheLabel()
    {
        Assert.Equal(Lines("R1=0 R3=0", "MCALL CYCLE81(2,0,2,-10)", "G0 X5 Y5", "NEXT:", "MCALL CYCLE81(2,0,2,-10)",
                "G0 G90 X=R1 Y10", "MCALL", "G0 Z50", "R1=R1+10 R3=R3+1", "IF R3<3 GOTOB NEXT"),
            MillBody("VAR:R1=0 VAR:R3=0", "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10", "CYCLE_CALL X=5 Y=5",
                "LABEL=NEXT", "CYCLE_CALL X={$R1} Y=10", "RAPID Z=50", "VAR:R1={$R1 + 10} VAR:R3={$R3 + 1}",
                "JUMP=NEXT IF={$R3 < 3}"));
    }

    // Siemens 7: a jump back ends the modal call before it, so the motion after the label, which is no call, does not
    // call the cycle on the next pass.
    [Fact]
    public void ModalCall_ArmedAtTheJumpBack_IsEndedBeforeTheJump()
    {
        Assert.Equal(Lines("R1=0 R3=0", "NEXT:", "G0 G90 Z5", "MCALL CYCLE81(2,0,2,-10)", "X=R1",
                "R1=R1+10 R3=R3+1", "MCALL", "IF R3<3 GOTOB NEXT"),
            MillBody("VAR:R1=0 VAR:R3=0", "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10", "LABEL=NEXT", "RAPID Z=5",
                "CYCLE_CALL X={$R1}", "VAR:R1={$R1 + 10} VAR:R3={$R3 + 1}", "JUMP=NEXT IF={$R3 < 3}"));
    }

    // D29 and virtual machine 1: the F that a CYCLE_F wrote is the feed of the control at the jump back, so the feed
    // motion after the label writes the program feed again (controller-mapping 5, CYCLE_F).
    [Fact]
    public void CycleFeed_InALoop_TheProgramFeedStandsAgainAfterTheLabel()
    {
        Assert.Equal(Lines("R3=0", "G1 Z-1 F500", "PASS:", "G1 G90 G94 X10 F500", "F100", "CYCLE81(2,0,2,-10)",
                "R3=R3+1", "IF R3<3 GOTOB PASS"),
            MillBody("VAR:R3=0", "LINE Z=-1 F=500", "LABEL=PASS", "LINE X=10",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10 CYCLE_F=100", "CYCLE_CALL", "VAR:R3={$R3 + 1}",
                "JUMP=PASS IF={$R3 < 3}"));
    }

    // Controller-mapping 5, CYCLE_CALL: a call without axis words is a direct call once at the current position.
    [Fact]
    public void CycleCall_WithoutPosition_IsADirectCall()
    {
        Assert.Equal(Lines("G0 X10 Y10", "F120", "CYCLE81(5,0,5,-20)"),
            MillBody("RAPID X=10 Y=10", "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=120", "CYCLE_CALL"));
    }

    // Controller-mapping 5, CYCLE_RETRACT: the cycle always returns to RTP, which is SAFE for CYCLE_RETRACT=SAFE; the
    // dwell DTB in its position (siemens 7, CYCLE82).
    [Fact]
    public void DrillDwell_RetractToSafe_IsCycle82WithRtpTheSafePlaneAndTheDwell()
    {
        Assert.Equal(Lines("F80", "MCALL CYCLE82(50,0,2,-12,,0.5)", "G0 X10"),
            Before("MCALL", MillBody(
                "CYCLE=DRILL_DWELL SURFACE=0 CLEARANCE=2 DEPTH=-12 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_DWELL=0.5 "
                + "CYCLE_F=80", "CYCLE_CALL X=10", "CYCLE=OFF")));
    }

    // D188: without SAFE, CYCLE_RETRACT=SAFE gives RTP no value, which the compiler must write, an ERROR.
    [Fact]
    public void CycleRetractSafe_WithoutSafe_IsAnError()
    {
        Assert.Equal(["CMP572"], CompilerCodes(Run(Program(MillHeader,
            "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-12 CYCLE_RETRACT=SAFE CYCLE_F=80", "CYCLE_CALL X=10"), Mill())));
    }

    // Machine-config 6, fixed: CYCLE83 with VARI=1 is PECK, the fixed value at its position; without PECK the cycle
    // takes its own infeeds (controller-mapping 5, PECK).
    [Fact]
    public void Peck_WithoutPeckDepth_IsCycle83WithVariOne()
    {
        Assert.Equal(Lines("F100", "MCALL CYCLE83(5,0,5,-40,,,,,,,,1)", "G0 X10"),
            Before("MCALL", MillBody("CYCLE=PECK SURFACE=0 CLEARANCE=5 DEPTH=-40 CYCLE_F=100", "CYCLE_CALL X=10",
                "CYCLE=OFF")));
    }

    // D181: PECK has no position of CYCLE83 in the catalog, an ERROR.
    [Fact]
    public void Peck_WithPeckDepth_IsAnError()
    {
        Assert.Equal(["CMP571"], CompilerCodes(Run(Program(MillHeader,
            "CYCLE=PECK SURFACE=0 CLEARANCE=5 DEPTH=-40 PECK=5 CYCLE_F=100", "CYCLE_CALL X=10"), Mill())));
    }

    // Controller-mapping 5, AXIS: _AXN 1, 2, 3 is the first, second and third geometry axis of the plane; the tool axis
    // stays empty, the default of the cycle (D59).
    [Fact]
    public void TapAlongX_WritesAxnOne()
    {
        Assert.Equal(Lines("MCALL CYCLE84(2,0,2,-15,,,,,1.5,,,,1)", "G0 Y10"),
            Before("MCALL", MillBody("CYCLE=TAP AXIS=X SURFACE=0 CLEARANCE=2 DEPTH=-15 PITCH=1.5",
                "CYCLE_CALL Y=10", "CYCLE=OFF")));
    }

    // Language 4.7.1: a catalog name the catalog of the machine has no entry for, an ERROR.
    [Fact]
    public void CycleNotInTheCatalog_IsAnError()
    {
        Assert.Equal(["CMP570"], CompilerCodes(Run(Program(MillHeader,
            "CYCLE=RECT_POCKET DEPTH=-10", "CYCLE_CALL X=10"), Mill())));
    }

    // The lines before the first line of a text.
    private static string Before(string line, string text)
    {
        int end = text.IndexOf("\n" + line + "\n", StringComparison.Ordinal);
        return end < 0 ? text : text.Substring(0, end + 1);
    }
}
