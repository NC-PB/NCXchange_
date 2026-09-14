using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The frames, the swivelled plane, the reference points, the diameter and the transformations (controllers siemens.md
/// 4, 6, 11 rule 4; controller-mapping 1; language 4.2; D31, D82, D83): the replacing instructions cut the chain at its
/// first entry and the additive ones append, G58 and G59 replace their part of the shift, CYCLE800 is TILT or TILT_AXIS
/// with MOVE and a RETRACT, $P_UIFR writes are RAW.
/// </summary>
public sealed class SiemensFrameTests
{
    // G54 to G57 are the datums 1 to 4, G505 to G599 5 and up, G500 cancels them (controller-mapping 1, ORIGIN).
    [Fact]
    public void G54G505AndG500_AreOrigins()
    {
        Assert.Equal(Lines("ORIGIN=1", "ORIGIN=5", "ORIGIN=0"), CheckedBody("G54\nG505\nG500"));
    }

    // Rule 4: TRANS cuts the chain at its first entry and appends, ATRANS appends, TRANS alone clears (D31).
    [Fact]
    public void TRANS_ThenATRANS_CutsChainThenAppends()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT Y=5", "SHIFT=RESET", "SHIFT=RESET", "SHIFT X=20", "SHIFT=RESET"),
            CheckedBody("TRANS X10\nATRANS Y5\nTRANS X20\nTRANS"));
    }

    // Rule 4: ROT and MIRROR cut the chain like TRANS, AROT and AMIRROR append; MIRROR=OFF resets a mirror (D124).
    [Fact]
    public void ROTAndMIRROR_CutTheChain_AROTAndAMIRROR_Append()
    {
        Assert.Equal(Lines("ROTATE=30", "ROTATE=10", "ROTATE=RESET", "ROTATE=RESET", "MIRROR=X", "MIRROR=Y",
                "MIRROR=OFF", "MIRROR=OFF"),
            CheckedBody("ROT Z30\nAROT Z10\nMIRROR X0\nAMIRROR Y0\nROT"));
    }

    // A replacing instruction removes every kind: TRANS after ROT removes the rotation too (D31).
    [Fact]
    public void TRANS_AfterARotation_RemovesTheRotation()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT=RESET", "ROTATE=30", "ROTATE=RESET", "SHIFT X=5 Y=5", "SHIFT X=1"),
            CheckedBody("TRANS X10\nROT Z30\nTRANS X5 Y5\nATRANS X1"));
    }

    // Rule 4: SCALE cuts the chain and its factors stay a RAW word; the scale stays in the frame of the control, so the
    // next instruction that deletes the frame stays RAW (D5).
    [Fact]
    public void SCALE_CutsTheChain_AndLeavesItUnknown()
    {
        NcxProgram program = FramedProgram("TRANS X10\nSCALE X2\nTRANS X5");

        Assert.Equal(Lines("SHIFT X=10", "SHIFT=RESET", Raw("SCALE X2"), Raw("TRANS X5")),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensWordsKeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // A frame instruction kept as RAW leaves the chain unknown: the RESETs of its cut are not in the output, so the
    // next replacing instruction stays RAW too (D5; language 4.2).
    [Fact]
    public void FrameInstruction_KeptAsRaw_LeavesTheChainUnknown()
    {
        Assert.Equal(Lines("ROTATE=30", Raw("MIRROR X"), Raw("TRANS X5")), CheckedBody("ROT Z30\nMIRROR X\nTRANS X5"));
    }

    // G58 replaces the absolute part of the shift and G59 the additive part, per axis (controller-mapping 1, ORIGIN):
    // appended where the chain holds no such part, replaced where it is the last entry, RAW otherwise.
    [Fact]
    public void G58AndG59_ReplaceTheirPartOfTheShift()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT X=5", "SHIFT=RESET", "SHIFT X=5 Y=2", Raw("G58 X20")),
            CheckedBody("G58 X10\nG59 X5\nG59 Y2\nG58 X20"));
    }

    // Rule 4: CYCLE800 with axis-wise angles in the order X Y Z (_MODE 57) is TILT, _DIR -1 MOVE=TURN, _FR=1 a RETRACT
    // in front of it; an additive swivel (_ST 1) appends, a new one replaces the tilts of CYCLE800, CYCLE800() resets
    // them (controller-mapping 1, TILT, MOVE, RETRACT; D82, D83).
    [Fact]
    public void CYCLE800Mode57_BecomesTiltWithMoveAndRetract()
    {
        Assert.Equal(Lines("RETRACT", "TILT A=0 B=30 C=0 MOVE=TURN", "RETRACT", "TILT A=0 B=0 C=10 MOVE=TURN",
                "RETRACT", "TILT=RESET", "TILT=RESET", "TILT A=0 B=20 C=0 MOVE=TURN", "TILT=RESET"),
            CheckedBody("CYCLE800(1,\"TABLE\",0,57,0,0,0,0,30,0,0,0,0,-1,100,1)\n"
                + "CYCLE800(1,\"TABLE\",1,57,0,0,0,0,0,10,0,0,0,-1,100,1)\n"
                + "CYCLE800(1,\"TABLE\",0,57,0,0,0,0,20,0,0,0,0,-1,100,1)\nCYCLE800()"));
    }

    // Rule 4: _MODE 39 with one turning angle is TILT whatever the order of the axes; _DIR 0 only computes the frame,
    // MOVE=STAY; _FR=5 retracts by _FR_I (controller-mapping 1, TILT; D83).
    [Fact]
    public void CYCLE800Mode39_WithOneTurningAngle_BecomesTiltWithRetract()
    {
        Assert.Equal(Lines("RETRACT=50", "TILT A=30 B=0 C=0 MOVE=STAY"),
            CheckedBody("CYCLE800(5,\"TABLE\",0,39,0,0,0,30,0,0,0,0,0,0,50,1)"));
    }

    // _MODE 39 with two turning angles, the form of the Hermle program of the corpus, stays RAW while the order of the
    // TILT angles against the axis-wise orders of CYCLE800 is an open question (SiemensTilt, TODO(question)).
    [Fact]
    public void CYCLE800Mode39_WithTwoTurningAngles_StaysRaw()
    {
        const string Hermle = "CYCLE800(0,\"HERMLE\",0,39,0,0,0,180,0,-90,0,0,0,-1,)";
        NcxProgram program = FramedProgram(Hermle);

        Assert.Equal(Lines(Raw(Hermle)), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // CYCLE800 in the rotary-axes mode (_MODE bits 7 and 6 = 11) is TILT_AXIS on the rotary axes of the machine,
    // CYCLE800() resets it (controller-mapping 1, TILT_AXIS; D82).
    [Fact]
    public void CYCLE800RotaryAxesMode_BecomesTiltAxis()
    {
        Assert.Equal(Lines("RETRACT", "TILT_AXIS A=-30 C=90 MOVE=TURN", "TILT_AXIS=RESET"),
            CheckedBody("CYCLE800(1,\"TABLE\",0,192,0,0,0,-30,90,0,0,0,0,-1,100,1)\nCYCLE800()", FiveAxis()));
    }

    // ROTS turns by two spatial angles, TILT with the third 0, and AROTS appends (siemens 4; controller-mapping 1,
    // TILT).
    [Fact]
    public void ROTSAndAROTS_AreTilts()
    {
        Assert.Equal(Lines("TILT A=10 B=20 C=0", "TILT A=5 B=0 C=0"), CheckedBody("ROTS X10 Y20\nAROTS X5"));
    }

    // Rule 4: a $P_UIFR write sets the datum table, a data write kept as RAW (controller-mapping 1, ORIGIN).
    [Fact]
    public void PUifrWrite_IsRaw()
    {
        Assert.Equal(Lines(Raw("$P_UIFR[1,X,TR]=10")), CheckedBody("$P_UIFR[1,X,TR]=10"));
    }

    // G74 is HOME, G75 FP=2 HOME with POINT, PRESETON is SETPOS (controller-mapping 1, HOME and SETPOS).
    [Fact]
    public void G74G75AndPreseton_AreHomeAndSetpos()
    {
        Assert.Equal(Lines("HOME Z", "HOME Z POINT=2", "SETPOS X=0"),
            CheckedBody("G74 Z1=0\nG75 FP=2 Z1=0\nPRESETON(X,0)"));
    }

    // DIAMON and DIAMOF switch the diameter programming, DIAM90 doubles the incremental X (controller-mapping 1,
    // DIAMETER).
    [Fact]
    public void DiamonDiamofAndDiam90_AreDiameter()
    {
        Assert.Equal(Lines("RAPID X=50 Z=2", "DIAMETER=ON", "LINE X=40 F=0.2", "DIAMETER=OFF", "LINE X=10",
                "DIAMETER=ON", "LINE IX=4"),
            CheckedBody("G0 X50 Z2\nDIAMON\nG1 X40 F0.2\nDIAMOF\nG1 X10\nDIAM90\nG91 G1 X2", MillTurn()));
    }

    // TRANSMIT is POLAR=ON, TRACYL(d) CYLINDER with the radius, TRAFOOF ends the transformation that is on (siemens 6;
    // controller-mapping 1, POLAR and CYLINDER).
    [Fact]
    public void TransmitAndTracyl_ArePolarAndCylinder()
    {
        Assert.Equal(Lines("POLAR=ON", "LINE X=10 Y=5 F=100", "POLAR=OFF", "CYLINDER=20", "LINE Y=10", "CYLINDER=OFF"),
            CheckedBody("TRANSMIT\nG1 X10 Y5 F100\nTRAFOOF\nTRACYL(40)\nG1 Y10\nTRAFOOF", MillTurn()));
    }
}
