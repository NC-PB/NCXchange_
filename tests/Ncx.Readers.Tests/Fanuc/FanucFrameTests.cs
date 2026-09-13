using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The frames of the Fanuc column of controller-mapping 1 (controllers fanuc.md 4, 9 rules 3 and 6; language 4.2):
/// SHIFT, FRAME=MACHINE, HOME, SETPOS, RPM_MAX, ROTATE, MIRROR, TILT, TILT_AXIS, TCPM, POLAR, CYLINDER, TOLERANCE.
/// </summary>
public sealed class FanucFrameTests
{
    // G52 replaces the previous G52, so the reader writes SHIFT=RESET before the new SHIFT; G52 X0 Y0 cancels
    // (controllers fanuc.md 9 rule 6; controller-mapping 1, SHIFT).
    [Fact]
    public void G52_ReplacesEarlierShift_EmitsReset()
    {
        Assert.Equal(
            Lines("SHIFT X=10 Y=5", "SHIFT=RESET", "SHIFT X=20", "SHIFT=RESET"),
            Body("G52 X10. Y5.\nG52 X20.\nG52 X0 Y0"));
    }

    // G53 is FRAME=MACHINE on its block (controllers fanuc.md 9 rule 3).
    [Fact]
    public void G53_IsFrameMachineOnItsBlock()
    {
        Assert.Equal(Lines("RAPID Z=0 FRAME=MACHINE"), Body("G53 G0 Z0"));
    }

    // G91 G28 Z0 is HOME Z; G28 X0 Y0 under G91 HOME X Y (controllers fanuc.md 9 rule 3; examples/2.5D_FRAESEN note 4).
    [Fact]
    public void G91G28Z0_BecomesHomeZ()
    {
        Assert.Equal(Lines("HOME Z", "HOME X Y"), Body("G91 G28 Z0\nG28 X0 Y0"));
    }

    // G30 P2 is HOME with POINT=2 (controllers fanuc.md 9 rule 3).
    [Fact]
    public void G30P2_BecomesHomeWithPoint2()
    {
        Assert.Equal(Lines("HOME Z POINT=2"), Body("G91 G30 P2 Z0"));
    }

    // G28 with a real distance moves to the intermediate point first (controllers fanuc.md 4).
    [Fact]
    public void G28_WithADistance_MovesToTheIntermediatePointFirst()
    {
        Assert.Equal(Lines("RAPID IZ=10", "HOME Z"), Body("G91 G28 Z10."));
    }

    // G28 U0 W0 of system A is HOME X Z; G28 H0 HOME C (controller-mapping 1, HOME).
    [Fact]
    public void G28U0W0_OnSystemA_BecomesHomeXZ()
    {
        Assert.Equal(Lines("HOME X Z", "HOME C"), Body("G28 U0 W0\nG28 H0", Lathe()));
    }

    // G92 X Y declares the current position on a mill, SETPOS (controller-mapping 1, D55).
    [Fact]
    public void G92_OnAMill_IsSetpos()
    {
        Assert.Equal(Lines("SETPOS X=0 Y=0"), Body("G92 X0 Y0"));
    }

    // G50 of system A is SETPOS with axis words and RPM_MAX with S (controller-mapping 1 SETPOS, 4 RPM_MAX).
    [Fact]
    public void G50_OnSystemA_IsTheSpeedLimitOrSetpos()
    {
        Assert.Equal(Lines("RPM_MAX=2500", "SETPOS C=0"), Body("G50 S2500\nG50 C0.", Lathe()));
    }

    // G68 R rotates the plane, G69 cancels (controller-mapping 1, ROTATE).
    [Fact]
    public void G68R30_IsRotate_AndG69Resets()
    {
        Assert.Equal(Lines("ROTATE=30", "ROTATE=RESET"), Body("G68 R30.\nG69"));
    }

    // ROTATE turns about the origin; G68 about another point stays RAW (language 4.2).
    [Fact]
    public void G68_AboutAnotherPoint_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68 X10. Y0 R30.\""), Body("G68 X10. Y0 R30."));
    }

    // G51.1 X0 mirrors X, G50.1 cancels (controller-mapping 1, MIRROR).
    [Fact]
    public void G511X0_IsMirrorX_AndG501Cancels()
    {
        Assert.Equal(Lines("MIRROR=X", "MIRROR=OFF"), Body("G51.1 X0\nG50.1 X0"));
    }

    // G68.2 with Euler angles is TILT with spatial angles, G53.1 in the next block MOVE=TURN (controller-mapping 1,
    // TILT and MOVE; D82).
    [Fact]
    public void G682_FollowedByG531_IsTiltWithMoveTurn()
    {
        Assert.Equal(Lines("TILT A=45 B=0 C=0 MOVE=TURN"), Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG53.1"));
    }

    // The origin of G68.2 is a SHIFT before the TILT, which G69 resets with it; the Euler angles I90 J45 K-90 turn
    // about Y by 45 degrees (language 4.2, D31).
    [Fact]
    public void G682_WithAnOrigin_IsShiftThenTilt()
    {
        Assert.Equal(
            Lines("SHIFT X=10", "TILT A=0 B=45 C=0", "SHIFT=RESET"),
            Body("G68.2 X10. Y0 Z0 I90. J45. K-90.\nG69"));
    }

    // G69 cancels the tilted plane and keeps the G52 before it (controllers fanuc.md 4). The origin of the G68.2 and
    // the G52 shift stand in one SHIFT, so that the chain holds one shift and SHIFT=RESET means the same under both
    // readings of language 4.2 (wave-1 question #95); after the G69 the chain holds the G52 shift again, which the
    // next G52 resets.
    [Fact]
    public void G69_AfterAG682WithAnOriginOverAG52_KeepsTheG52Shift()
    {
        Assert.Equal(
            Lines(
                "SHIFT X=5",
                "SHIFT=RESET",
                "SHIFT X=15",
                "TILT A=45 B=0 C=0 MOVE=TURN",
                "SHIFT=RESET",
                "SHIFT X=5",
                "RAPID X=0 Y=0",
                "SHIFT=RESET"),
            Body("G52 X5.\nG68.2 X10. Y0 Z0 I0 J45. K0\nG53.1\nG69\nG0 X0 Y0\nG52 X0"));
    }

    // fanuc 4 does not say where the local coordinate system of a G52 stands against an active tilted plane: the G52
    // stays RAW and the tilted plane with its origin stays in the chain until the G69 (D5).
    [Fact]
    public void G52_WhileAG682IsActive_IsKeptAsRaw()
    {
        Assert.Equal(
            Lines(
                "SHIFT X=10",
                "TILT A=45 B=0 C=0",
                "RAW:FANUC=\"G52 X5.\"",
                "RAW:FANUC=\"G52 X20.\"",
                "RAPID X=0 Y=0",
                "SHIFT=RESET"),
            Body("G68.2 X10. Y0 Z0 I0 J45. K0\nG52 X5.\nG52 X20.\nG0 X0 Y0\nG69"));
    }

    // A G52 while a G68 rotates stays RAW for the same reason, and the rotation stays in the chain: the SHIFT=RESET of
    // a replacing G52 would remove it (language 4.2).
    [Fact]
    public void G52_ReplacedWhileAG68Rotates_IsKeptAsRawAndTheRotationStays()
    {
        Assert.Equal(
            Lines("SHIFT X=5", "ROTATE=30", "RAW:FANUC=\"G52 X20.\"", "ROTATE=RESET", "SHIFT=RESET"),
            Body("G52 X5.\nG68 R30.\nG52 X20.\nG69\nG52 X0"));
    }

    // fanuc 4 does not say what a second G68.2 or G68 does before the G69, a new plane or one in the active plane: it
    // stays RAW, and the G69 resets the one the chain holds.
    [Fact]
    public void G682AndG68_WhileOneIsActive_AreKeptAsRaw()
    {
        Assert.Equal(
            Lines("TILT A=45 B=0 C=0", "RAW:FANUC=\"G68.2 X0 Y0 Z0 I0 J30. K0\"", "TILT=RESET"),
            Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG68.2 X0 Y0 Z0 I0 J30. K0\nG69"));
        Assert.Equal(
            Lines("ROTATE=30", "RAW:FANUC=\"G68 R45.\"", "ROTATE=RESET"),
            Body("G68 R30.\nG68 R45.\nG69"));
    }

    // G68.1 stays RAW until its words are mapped to the axis angles of TILT_AXIS.
    [Fact]
    public void G681_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68.1 X0 Y0 Z0 I0 J0 K1 R30.\""), Body("G68.1 X0 Y0 Z0 I0 J0 K1 R30."));
    }

    // G43.4 H1 is TCPM=ON with its length offset, G49 ends both (controller-mapping 1, TCPM; 3, OFFSET).
    [Fact]
    public void G434H1_IsTcpmOnWithTheLengthOffset_AndG49EndsIt()
    {
        Assert.Equal(Lines("OFFSET:LEN=1 TCPM=ON", "OFFSET:LEN=0 TCPM=OFF"), Body("G43.4 H1\nG49"));
    }

    // Under G43.5 the I J K of a line are the tool vector TX TY TZ (controller-mapping 2, D81).
    [Fact]
    public void G435_LineWithIJK_IsLineWithTheToolVector()
    {
        Assert.Equal(
            Lines("OFFSET:LEN=1 TCPM=ON", "LINE X=10 TX=0 TY=0.5 TZ=0.866 F=100"),
            Body("G43.5 H1\nG1 X10. I0 J0.5 K0.866 F100"));
    }

    // G12.1 and G13.1 switch POLAR, and the builder's G112 and G113 of [transform] too (controller-mapping 1).
    [Fact]
    public void G121AndG112_ArePolarOnAndOff()
    {
        Assert.Equal(Lines("POLAR=ON", "POLAR=OFF"), Body("G12.1\nG13.1"));
        Assert.Equal(Lines("POLAR=ON", "POLAR=OFF"), Body("G112\nG113", Lathe()));
    }

    // G7.1 C30. is CYLINDER=30, C0 off; the builder's G107 of [transform] too (controller-mapping 1, D96).
    [Fact]
    public void G71AndG107_AreCylinderWithTheRadius()
    {
        Assert.Equal(Lines("CYLINDER=30", "CYLINDER=OFF"), Body("G7.1 C30.\nG7.1 C0"));
        Assert.Equal(Lines("CYLINDER=25", "CYLINDER=OFF"), Body("G107 C25.\nG107 C0", Lathe()));
    }

    // G5.1 Q0 is TOLERANCE=OFF; G5.1 Q1 with the tolerance in a parameter stays RAW (controller-mapping 1).
    [Fact]
    public void G51Q0_IsToleranceOff_AndQ1StaysRaw()
    {
        Assert.Equal(Lines("TOLERANCE=OFF", "RAW:FANUC=\"G5.1 Q1\""), Body("G5.1 Q0\nG5.1 Q1"));
    }
}
