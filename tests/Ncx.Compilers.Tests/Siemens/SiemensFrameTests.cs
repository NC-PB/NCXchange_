using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 4 of controllers siemens.md 12 and the frames of the Siemens column of controller-mapping 1: ORIGIN, the chain
/// in program order as TRANS and ATRANS, AROT, AMIRROR, CYCLE800 from [transform], G74 and G75, PRESETON, DIAMON, the
/// transformations and CYCLE832 for TOLERANCE, the sub spindle side whose datum runs Z the other way, and the end of a
/// programmable frame or a swivel that a RAW line made.
/// </summary>
public sealed class SiemensFrameTests
{
    // The swivel of the Hermle C22 U program of the corpus, which the reader keeps as RAW (controllers siemens.md 4).
    private const string RawHermleSwivel = "RAW:SIEMENS=\"CYCLE800(0,\\\"HERMLE\\\",0,39,0,0,0,180,0,-90,0,0,0,-1,)\"";

    // The line the RAW swivel writes.
    private const string HermleSwivel = "CYCLE800(0,\"HERMLE\",0,39,0,0,0,180,0,-90,0,0,0,-1,)";

    // Controller-mapping 1, ORIGIN: G54 to G57 are 1 to 4, G505 to G599 5 and up, G500 0 (siemens 4).
    [Fact]
    public void Origin_OneFiveAndZero_IsG54G505AndG500()
    {
        Assert.Equal(Lines("G54", "G505", "G500"), MillBody("ORIGIN=1", "ORIGIN=5", "ORIGIN=0"));
    }

    // Siemens 4: G599 is the last settable frame; a datum beyond it has no code, an ERROR.
    [Fact]
    public void Origin_Beyond99_IsAnError()
    {
        Assert.Equal(["CMP530"], CompilerCodes(Run(Program(MillHeader, "ORIGIN=100"), Mill())));
    }

    // Siemens 12 rule 4: the chain in program order, TRANS for the first entry and ATRANS, AROT, AMIRROR for the rest
    // (D31; controller-mapping 1, SHIFT, ROTATE, MIRROR).
    [Fact]
    public void Chain_ShiftRotationShiftMirror_IsTransThenTheAdditiveInstructions()
    {
        Assert.Equal(Lines("G54", "TRANS X10 Y5", "AROT RPL=30", "ATRANS Z-2", "AMIRROR X0"),
            MillBody("ORIGIN=1", "SHIFT X=10 Y=5", "ROTATE=30", "SHIFT Z=-2", "MIRROR=X"));
    }

    // Siemens 4: a replacing instruction deletes all earlier programmable frame instructions, so a chain cut from its
    // end is written again from its first entry, TRANS alone where no entry stays (language 4.2: RESET removes its
    // entry and what follows).
    [Fact]
    public void ChainReset_FromItsEnd_IsWrittenAgainFromItsFirstEntry()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "AROT RPL=30", "TRANS X10", "TRANS"),
            MillBody("ORIGIN=1", "SHIFT X=10", "ROTATE=30", "ROTATE=RESET", "SHIFT=RESET"));
    }

    // Language 4.2, ORIGIN starts an empty chain: TRANS alone clears the programmable frame.
    [Fact]
    public void Origin_AfterAShift_ClearsTheProgrammableFrameWithTrans()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "TRANS", "G55"), MillBody("ORIGIN=1", "SHIFT X=10", "ORIGIN=2"));
    }

    // Siemens 12 rule 4: CYCLE800(...) from the [transform] template of millturn1.toml, {dir} from MOVE, TURN -1 and
    // STAY 0, the angles of the tilt; TILT=RESET is TILT_OFF, CYCLE800() (machine-config 5; controller-mapping 1,
    // TILT).
    [Fact]
    public void Tilt_TurnThenReset_IsCycle800WithDirMinusOneThenCycle800()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,57,0,0,0,0,45,0,0,0,0,-1,100,1)", "CYCLE800()"),
            MillTurnBody("TILT B=45 MOVE=TURN", "TILT=RESET"));
    }

    // Machine-config 5: {dir} of MOVE=STAY is 0, the frame only computed.
    [Fact]
    public void Tilt_Stay_IsCycle800WithDirZero()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,57,0,0,0,10,20,30,0,0,0,0,100,1)"),
            MillTurnBody("TILT A=10 B=20 C=30 MOVE=STAY"));
    }

    // The TODO(question) of SiemensFrames: MOVE=MOVE has no {dir} of machine-config 5, an ERROR.
    [Fact]
    public void Tilt_Move_IsAnError()
    {
        Assert.Equal(["CMP532"], CompilerCodes(Run(Program(MillTurnHeader, "TILT B=45 MOVE=MOVE"), MillTurn())));
    }

    // D82: TILT_AXIS without TILT_AXIS_ON needs the kinematics module, an ERROR.
    [Fact]
    public void TiltAxis_WithoutTemplate_IsAnError()
    {
        Assert.Equal(["CMP532"], CompilerCodes(Run(Program(MillHeader, "TILT_AXIS A=45 MOVE=TURN"), FiveAxis())));
    }

    // Controller-mapping 1, MOVE and ROT: ROT is not available on Siemens, a WARNING.
    [Fact]
    public void Tilt_WithRot_WarnsThatRotIsNotWritten()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TILT B=45 MOVE=STAY ROT=COORD"), MillTurn());

        Assert.Equal(["CMP533"], CompilerCodes(result));
    }

    // Siemens 4: CYCLE800() cancels the swivel; TILT=RESET removes the tilt (language 4.2). After a RAW swivel the
    // chain of the virtual machine holds no tilt, and the control stays swivelled unless the reset is written:
    // CYCLE800() of TILT_OFF (machine-config 5), the CYCLE800() of the Hermle program the reader reads as TILT=RESET.
    [Fact]
    public void TiltReset_AfterARawSwivel_IsCycle800()
    {
        Assert.Equal(Lines(HermleSwivel, "CYCLE800()"), MillTurnBody(RawHermleSwivel, "TILT=RESET"));
    }

    // Siemens 4: TRANS alone clears the programmable frame; SHIFT=RESET removes the shift (language 4.2), so after a
    // RAW TRANS the control is shifted until TRANS alone is written, although the chain of the virtual machine is
    // empty.
    [Fact]
    public void ShiftReset_AfterARawTrans_IsTransAlone()
    {
        Assert.Equal(Lines("TRANS X10", "TRANS"), MillBody("RAW:SIEMENS=\"TRANS X10\"", "SHIFT=RESET"));
    }

    // Siemens 4, every replacing instruction deletes all earlier programmable frame instructions: the part of the chain
    // that stays after the reset is written again from its first entry, which ends what the RAW text left.
    [Fact]
    public void ShiftReset_AfterARawTrans_WritesTheRotationThatStaysAgain()
    {
        Assert.Equal(Lines("ROT RPL=30", "TRANS X10", "ROT RPL=30"),
            MillBody("ROTATE=30", "RAW:SIEMENS=\"TRANS X10\"", "SHIFT=RESET"));
    }

    // Language 4.2, ORIGIN starts an empty chain: after a RAW TRANS, TRANS alone clears the programmable frame before
    // the datum (siemens 4), which the RAW line left unknown too.
    [Fact]
    public void Origin_AfterARawTrans_ClearsTheProgrammableFrameWithTrans()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "TRANS", "G54"),
            MillBody("ORIGIN=1", "RAW:SIEMENS=\"TRANS X10\"", "ORIGIN=1"));
    }

    // Language 4.2, ORIGIN starts an empty chain: after a RAW swivel, CYCLE800() of TILT_OFF ends it (siemens 4;
    // machine-config 5); the programmable frame, which the RAW text does not name, stays as it was.
    [Fact]
    public void Origin_AfterARawSwivel_EndsTheSwivelWithCycle800()
    {
        Assert.Equal(Lines("G54", HermleSwivel, "CYCLE800()", "G54"),
            MillTurnBody("ORIGIN=1", RawHermleSwivel, "ORIGIN=1"));
    }

    // Virtual machine 1: the STATIC walk does not follow the jump back, which arrives at the label with the swivel of
    // the RAW line after it, so TILT=RESET after the label writes CYCLE800() on every pass (siemens 4, 8).
    [Fact]
    public void TiltReset_AfterALabelThatAJumpReachesFromARawSwivel_IsCycle800()
    {
        Assert.Equal(Lines("R1=0", "AGAIN:", "CYCLE800()", HermleSwivel, "R1=R1+1", "IF R1<2 GOTOB AGAIN"),
            MillTurnBody("VAR:R1=0", "LABEL=AGAIN", "TILT=RESET", RawHermleSwivel, "VAR:R1={$R1 + 1}",
                "JUMP=AGAIN IF={$R1 < 2}"));
    }

    // D99: a subprogram is written once for every caller from an unknown target state, so a caller's RAW swivel may be
    // active where it starts, and its TILT=RESET writes CYCLE800() (siemens 4; virtual machine 3.9).
    [Fact]
    public void TiltReset_InASubprogramCalledAfterARawSwivel_IsCycle800()
    {
        string program = $"""
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            {MillTurnHeader}
            {RawHermleSwivel}
            CALL=UNTILT
            PROGRAM=END
            SUB=BEGIN NAME=UNTILT
            TILT=RESET
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("%_N_UNTILT_SPF", "PROC UNTILT", "CYCLE800()", "RET"),
            TextOf(Run(program, MillTurn())), StringComparison.Ordinal);
    }

    // Machine-config 5: a machine without TILT_OFF in [transform] cannot write the end of a swivel that a RAW line may
    // have made, which TILT=RESET asks for: the ERROR of the missing template, not a program that stays swivelled.
    [Fact]
    public void TiltReset_AfterARawSwivel_OnAMachineWithoutTiltOff_IsAnError()
    {
        Assert.Equal(["CMP010"], CompilerCodes(Run(Program(MillHeader, RawHermleSwivel, "TILT=RESET"), Mill())));
    }

    // Siemens 4: MSG changes no frame, and the text of its message names no instruction, so ORIGIN after it writes the
    // datum and no reset (controller-mapping 1, COMMENT: MSG is an operator message).
    [Fact]
    public void Origin_AfterAMessageThatNamesTrans_WritesNoReset()
    {
        Assert.Equal(Lines("G54", "MSG(\"TRANS X10\")", "G54"),
            MillTurnBody("ORIGIN=1", "RAW:SIEMENS=\"MSG(\\\"TRANS X10\\\")\"", "ORIGIN=1"));
    }

    // Controller-mapping 1, HOME: G74 with the machine axis names and 0 (siemens 3), G75 with the axes, FP= the point.
    [Fact]
    public void Home_ReferencePointAndPoint2_IsG74WithMachineNamesAndG75WithFp()
    {
        Assert.Equal(Lines("G74 X1=0 Z1=0", "G75 X0 Z0 FP=2"), MillTurnBody("HOME X Z", "HOME X Z POINT=2"));
    }

    // Controller-mapping 1, SETPOS: PRESETON(C, 0), one call per axis of [setpos] (D55).
    [Fact]
    public void Setpos_IsPresetonPerAxis()
    {
        Assert.Equal(Lines("G74 X1=0 Z1=0", "PRESETON(X,300)", "PRESETON(Z,10)"),
            MillTurnBody("HOME X Z", "SETPOS X=300 Z=10"));
    }

    // Controller-mapping 1, DIAMETER: DIAMOF and DIAMON of [diameter] on the switchable X axis of millturn1.toml, and
    // the X words as the control has them: radii after DIAMOF (D60).
    [Fact]
    public void Diameter_OffThenOn_IsDiamofAndDiamonWithTheX()
    {
        Assert.Equal(Lines("DIAMOF", "G0 X20", "DIAMON", "G0 X40"),
            MillTurnBody("DIAMETER=OFF", "RAPID X=20", "DIAMETER=ON", "RAPID X=40"));
    }

    // Controller-mapping 1, TCPM, CYLINDER and POLAR: TRAORI, TRACYL with the working diameter, TRANSMIT and TRAFOOF of
    // [transform] (millturn1.toml; D96).
    [Fact]
    public void Transformations_AreTheTemplatesOfTheMachine()
    {
        Assert.Equal(Lines("TRACYL(60)", "TRAFOOF", "TRANSMIT", "TRAFOOF", "TRAORI", "TRAFOOF"),
            MillTurnBody("CYLINDER=30", "CYLINDER=OFF", "POLAR=ON", "POLAR=OFF", "TCPM=ON", "TCPM=OFF"));
    }

    // Controller-mapping 1, TOLERANCE: CYCLE832(tol, mode, otol) of [tolerance], OFF as CYCLE832(0,0,1) (D85).
    [Fact]
    public void Tolerance_OnAndOff_IsCycle832()
    {
        Assert.Equal(Lines("CYCLE832(0.02,3,0.05)", "CYCLE832(0,0,1)"),
            MillTurnBody("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=ROUGH", "TOLERANCE=OFF"));
    }

    // D151: the ON template needs {rotary}; without TOLERANCE:ROTARY the template is unusable, the ERROR of the
    // template (machine-config introduction).
    [Fact]
    public void Tolerance_WithoutRotary_IsTheErrorOfTheTemplate()
    {
        Assert.Equal(["CFG100"], Codes(Run(Program(MillTurnHeader, "TOLERANCE=0.02"), MillTurn())));
    }

    // Controller-mapping 1, RETRACT: without a template of [retract] only the kinematics module could compute it, an
    // ERROR (D83).
    [Fact]
    public void Retract_WithoutTemplate_IsAnError()
    {
        Assert.Equal(["CMP534"], CompilerCodes(Run(Program(MillHeader, "RETRACT"), Mill())));
    }

    // Machine-config 5, SUB_frame = "datum": on the sub spindle side the datum runs Z the other way and the compiler
    // negates Z, G55 the datum of that side on millturn1.toml (D57; virtual machine 3.4).
    [Fact]
    public void ZOnTheSubSpindleSide_WithADatum_IsNegated()
    {
        Assert.Equal(Lines("G0 X40 Z5", "G55", "G0 X40 Z-5", "G1 Z=IC(2) F0.1"),
            MillTurnBody("RAPID X=40 Z=5", "WORKPIECE=SUB", "RAPID X=40 Z=5", "LINE IZ=-2 F=0.1"));
    }

    // The TODO(question) of SiemensArcs: with Z negated, an arc in the ZX plane turns the other way round.
    [Fact]
    public void ArcOnTheSubSpindleSide_WithADatum_TurnsTheOtherWay()
    {
        Assert.Equal(Lines("G55", "G0 X40 Z-5", "G3 X50 Z-10 CR=5 F0.1"),
            MillTurnBody("WORKPIECE=SUB", "RAPID X=40 Z=5", "ARC=CW X=50 Z=10 R=5 F=0.1"));
    }
}
