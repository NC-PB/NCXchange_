using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 3 of controllers siemens.md 12 and the Siemens column of controller-mapping 3 and 4: T and M6 per
/// [tool_change], D from OFFSET, SETMS in a block of its own, S2= and M2=3 for the other spindles, the spindle modes,
/// COUPON and COUPOF, the functions of the machine.
/// </summary>
public sealed class SiemensToolTests
{
    // Siemens 12 rule 3: T1 and M6 per [tool_change] of the mill, D from OFFSET; T alone is the preload (controller-
    // mapping 3, PRELOAD and TOOL).
    [Fact]
    public void ToolChangeOnAMill_IsTAndM6ThenDAndThePreload()
    {
        Assert.Equal(Lines("T5 M6", "D5", "T6"), MillBody("TOOL=5 OFFSET=5", "PRELOAD=6"));
    }

    // Controller-mapping 3, TOOL=0: T0 M6 per unload of the mill.
    [Fact]
    public void ToolZero_IsTheUnloadTemplate()
    {
        Assert.Equal(Lines("T5 M6", "T0 M6"), MillBody("TOOL=5", "TOOL=0"));
    }

    // Siemens 12 rule 3 on the turret of millturn1.toml: T3 D3 from change = "T{tool} D{offset}", the D the one
    // register of OFFSET:LEN and OFFSET:RAD (controller-mapping 3; offsets_with_change).
    [Fact]
    public void ToolChangeOnATurret_IsTWithTheDOfTheTemplate()
    {
        Assert.Equal(Lines("T3 D3"), MillTurnBody("TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3"));
    }

    // Controller-mapping 3, OFFSET:LEN: the D of the control is one register; two registers write the length one with a
    // WARNING (the TODO(question) of SiemensTools).
    [Fact]
    public void OffsetLenAndRad_TwoRegisters_WriteTheLengthWithAWarning()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=13"), MillTurn());

        Assert.Equal(Lines("T3 D3"), Body(result));
        Assert.Equal(["CMP510"], CompilerCodes(result));
    }

    // Controller-mapping 1, FRAME=MACHINE: G0 G53 Z0 D0, the offset cancelled in the block of the retract.
    [Fact]
    public void OffsetZeroInAMachineFrameRetract_IsD0InTheSameBlock()
    {
        Assert.Equal(Lines("T1 M6", "D1", "G0 G53 Z0 D0"),
            MillBody("TOOL=1 OFFSET=1", "RAPID Z=0 FRAME=MACHINE OFFSET=0"));
    }

    // Controller-mapping 1, SKIP: the change is written from [tool_change] without the skip mark, an ERROR.
    [Fact]
    public void SkipOnAToolChange_IsAnError()
    {
        Assert.Equal(["CMP502"], CompilerCodes(Run(Program(MillHeader, "SKIP TOOL=1"), Mill())));
    }

    // Siemens 12 rule 3: S and M3 of the master spindle, S<n>= and M<n>=3 for the other spindles (controller-mapping 4,
    // SPINDLE:role; millturn1.toml).
    [Fact]
    public void SpindlesOfTheRoles_AreThePlainWordsAndTheExtensions()
    {
        Assert.Equal(Lines("S1500 M3", "S3=6000 M3=3", "M3=5", "M2=70"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1500", "SPINDLE:TOOL=CW RPM:TOOL=6000", "SPINDLE:TOOL=OFF",
                "SPINDLE_MODE:SUB=AXIS"));
    }

    // Siemens 12 rule 3: SETMS(n) in its own block when the master spindle changes, G96 acting on the master spindle
    // (siemens 5; millturn1.toml, [spindle.SUB]); SETMS alone returns before a plain S of the configured one.
    [Fact]
    public void SurfaceSpeedOnTheSubSpindle_SelectsItAsMasterInABlockOfItsOwn()
    {
        Assert.Equal(Lines("SETMS(2)", "G96 S2=200 LIMS[2]=3000", "SETMS", "S1500 M3"),
            MillTurnBody("CSS:SUB=ON VC:SUB=200 RPM_MAX:SUB=3000", "SPINDLE:MAIN=CW RPM:MAIN=1500"));
    }

    // Language 4.11: under CSS RPM is ignored and not written; when CSS goes off the spindle turns at RPM again, G97
    // and S with the last RPM (controller-mapping 4, CSS).
    [Fact]
    public void SurfaceSpeedOff_WritesG97AndTheSpeedOfRpm()
    {
        Assert.Equal(Lines("S1000 M3", "G96 S180 LIMS=3000", "G97 S1200"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1000", "CSS:MAIN=ON VC:MAIN=180 RPM_MAX:MAIN=3000",
                "RPM:MAIN=1200", "CSS:MAIN=OFF"));
    }

    // Siemens 1: X=10 when the value is an expression; the plain S of the templates S{rpm} and G96 S{value} of the
    // master spindle takes the equals sign too, S=R1*2 (machine-config 5; controller-mapping 4, RPM and CSS).
    [Fact]
    public void SpeedAsAnExpression_InThePlainSOfTheTemplate_IsWrittenAfterAnEqualsSign()
    {
        Assert.Equal(Lines("R1=1000", "S=R1*2 M3", "G96 S=R1/10 LIMS=3000"),
            MillTurnBody("VAR:R1=1000", "SPINDLE:MAIN=CW RPM:MAIN={$R1 * 2}",
                "CSS:MAIN=ON VC:MAIN={$R1 / 10} RPM_MAX:MAIN=3000"));
    }

    // Siemens 2 and 5: G94, G95, G96 and G961 are one group, so G94 would switch the constant surface speed off; under
    // FEED_MODE=PER_MIN the surface speed is G961, and the feed motion after it writes no G94 (controller-mapping 4,
    // CSS; the TODO(question) of SiemensSpindles).
    [Fact]
    public void SurfaceSpeedUnderFeedPerMinute_IsG961AndTheFeedMotionKeepsIt()
    {
        string program = Program("FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000", "LINE X=40 F=100");

        Assert.Equal(Lines("G961 S200 LIMS=3000 M3", "G1 X40 F100"), Body(Run(program, MillTurn())));
    }

    // Siemens 2 and 5: a FEED_MODE under the constant surface speed is the surface speed code of the new feed type,
    // G961 per minute and G96 per revolution, never G94 or G95, which would switch it off; after G97 the feed type
    // stays (controller-mapping 4, CSS).
    [Fact]
    public void FeedModeChange_UnderSurfaceSpeed_IsTheSurfaceSpeedCodeOfTheFeedType()
    {
        Assert.Equal(
            Lines("S1500 M3", "G96 S200 LIMS=3000", "G1 G961 X40 F100", "G1 G96 X38 F0.2", "G97 S1500", "G1 X36"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1500", "CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000",
                "FEED_MODE=PER_MIN LINE X=40 F=100", "FEED_MODE=PER_REV LINE X=38 F=0.2", "CSS:MAIN=OFF",
                "LINE X=36"));
    }

    // D99 with siemens 2 and 5: a subprogram is written from an unknown state of the control, and its first feed motion
    // under the caller's constant surface speed writes G96, since G95 would switch the surface speed off.
    [Fact]
    public void FeedMotionInASubprogram_UnderSurfaceSpeed_IsG96()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF
            SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000
            RAPID X=42 Z=2
            CALL=FACE
            PROGRAM=END
            SUB=BEGIN NAME=FACE
            LINE X=0 F=0.1
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC FACE", "G1 G90 G96 X0 F0.1", "RET"), TextOf(Run(program, MillTurn())),
            StringComparison.Ordinal);
    }

    // Siemens 5 and 12 rule 3: what a RAW line leaves active is not known, the master spindle neither, so the plain S
    // after it selects the configured master spindle again with SETMS (language 4.1, RAW).
    [Fact]
    public void MasterSpindle_AfterARawLine_IsSelectedAgain()
    {
        Assert.Equal(Lines("SETMS(2)", "G96 S2=200 M2=3", "STOPRE", "SETMS", "S1000 M3"),
            MillTurnBody("CSS:SUB=ON VC:SUB=200 SPINDLE:SUB=CW", "RAW:SIEMENS=\"STOPRE\"",
                "SPINDLE:MAIN=CW RPM:MAIN=1000"));
    }

    // Siemens 5 and virtual machine 1: the jump back arrives at the label with the master spindle of its own block,
    // SETMS(2) here, so the plain S after the label selects the configured master spindle again.
    [Fact]
    public void MasterSpindle_InALoop_IsSelectedAgainAfterTheLabel()
    {
        Assert.Equal(Lines("R3=0", "S1000 M3", "PASS:", "SETMS", "S1200", "SETMS(2)", "G96 S2=200 M2=3", "R3=R3+1",
                "IF R3<3 GOTOB PASS"),
            MillTurnBody("VAR:R3=0", "SPINDLE:MAIN=CW RPM:MAIN=1000", "LABEL=PASS", "RPM:MAIN=1200",
                "CSS:SUB=ON VC:SUB=200 SPINDLE:SUB=CW", "VAR:R3={$R3 + 1}", "JUMP=PASS IF={$R3 < 3}"));
    }

    // Siemens 5: a machine with one spindle has no other master spindle, so no SETMS stands after a RAW line
    // (machine-config 4; siemens-840dsl-mill.toml, the spindle S1 alone).
    [Fact]
    public void MasterSpindle_OnAMachineWithOneSpindle_IsNeverSelectedAgain()
    {
        Assert.Equal(Lines("S1000 M3", "STOPRE", "S1200"),
            MillBody("SPINDLE=CW RPM=1000", "RAW:SIEMENS=\"STOPRE\"", "RPM=1200"));
    }

    // D99: a subprogram is written from an unknown state of the control, so its first plain S selects the configured
    // master spindle with SETMS in a block of its own (siemens 5).
    [Fact]
    public void MasterSpindleInASubprogram_IsSelectedAtItsFirstUse()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF
            CALL=START_MAIN
            PROGRAM=END
            SUB=BEGIN NAME=START_MAIN
            SPINDLE:MAIN=CW RPM:MAIN=800
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC START_MAIN", "SETMS", "S800 M3", "RET"), TextOf(Run(program, MillTurn())),
            StringComparison.Ordinal);
    }

    // D172: the change template of the mill writes {tool}, and a tool by name has no number for it; the template is
    // unusable, the ERROR of the template (machine-config introduction).
    [Fact]
    public void ToolByName_WithANumericTemplate_IsTheErrorOfTheTemplate()
    {
        Assert.Equal(["CFG100"], Codes(Run(Program(MillHeader, "TOOL=\"DRILL_D8\""), Mill())));
    }

    // Controller-mapping 4, ORIENT: SPOS= of the configured master spindle.
    [Fact]
    public void Orient_IsSposOfTheTemplate()
    {
        Assert.Equal(Lines("SPOS=90", "SPOS[2]=45"), MillTurnBody("ORIENT:MAIN=90", "ORIENT:SUB=45"));
    }

    // Controller-mapping 4, SPINDLE_SYNC: COUPON(S2,S1) of [spindle_sync], with PHASE the angle, COUPOF off
    // (millturn1.toml; D154).
    [Fact]
    public void SpindleSync_IsCouponWithThePhaseAndCoupof()
    {
        Assert.Equal(Lines("COUPON(S2,S1)", "COUPOF(S2,S1)", "COUPON(S2,S1,113.5)"),
            MillTurnBody("SPINDLE_SYNC=MAIN,SUB", "SPINDLE_SYNC=OFF", "SPINDLE_SYNC=MAIN,SUB PHASE=113.5"));
    }

    // D154: [spindle_sync] couples the default spindle with the other work spindle; another pair is an ERROR.
    [Fact]
    public void SpindleSync_OfAnotherPair_IsAnError()
    {
        Assert.Equal(["CMP512"],
            CompilerCodes(Run(Program(MillTurnHeader, "SPINDLE_SYNC=SUB,MAIN"), MillTurn())));
    }

    // Controller-mapping 4, COOLANT and FUNC: the M codes of the tables of the machine in the main line; MFUNC with a
    // WARNING (language 4.6); STOP as M0 and M1 (controller-mapping 1).
    [Fact]
    public void Functions_AreTheMCodesOfTheTables()
    {
        CompileResult result = Run(Program(MillTurnHeader, "COOLANT=ON FUNC:SUB_CHUCK=OPEN", "MFUNC=123",
            "STOP=OPTIONAL", "STOP=PROGRAM"), MillTurn());

        Assert.Equal(Lines("M8 M68", "M123", "M1", "M0"), Body(result));
        Assert.Equal(["CMP513"], CompilerCodes(result));
    }

    // Language 4.10, WORKPIECE: the [workpiece] template of the role, G55 on millturn1.toml, and an ORIGIN of the same
    // datum is not written again (the TODO(question) of SiemensFunctions, D222).
    [Fact]
    public void WorkpieceSub_IsTheTemplateAndTheOriginAfterItIsNotWrittenAgain()
    {
        Assert.Equal(Lines("G55"), MillTurnBody("WORKPIECE=SUB", "ORIGIN=2"));
    }
}
