using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The frames, the tilted plane, the tolerance and the dwell (controllers heidenhain.md 2, 3, 5, 7 rule 6;
/// controller-mapping 1; language 4.1, 4.2; D31, D82, D83, D85).
/// </summary>
public sealed class HeidenhainFrameTests
{
    // CYCL DEF 247 Q339=n is ORIGIN=n (controller-mapping 1, ORIGIN).
    [Fact]
    public void CyclDef247_IsOrigin()
    {
        Assert.Equal(Lines(Commented("ORIGIN=1", "; REF.PUNKTNUMMER")),
            Body("1 CYCL DEF 247 INIT. REF.PKT ~\n    Q339=1 ;REF.PUNKTNUMMER"));
    }

    // CYCL DEF 7 replaces the previous shift: SHIFT=RESET before the new SHIFT, an omitted axis unchanged
    // (heidenhain 3, 7 rule 6; language 4.2; D31).
    [Fact]
    public void CyclDef7_ReplacesTheEarlierShiftWithReset()
    {
        Assert.Equal(Lines("SHIFT X=60 Y=40", "SHIFT=RESET", "SHIFT X=10 Y=40", "SHIFT=RESET"),
            Body("1 CYCL DEF 7.0 NULLPUNKT\n2 CYCL DEF 7.1 X+60\n3 CYCL DEF 7.2 Y+40\n4 CYCL DEF 7.0 NULLPUNKT\n"
                + "5 CYCL DEF 7.1 X+10\n6 CYCL DEF 7.0 NULLPUNKT\n7 CYCL DEF 7.1 X+0\n8 CYCL DEF 7.2 Y+0"));
    }

    // A new cycle 7 while a rotation follows the shift it replaces stays RAW: where it stands against the rotation is
    // not said (language 4.2, D31).
    [Fact]
    public void CyclDef7_WhileARotationFollowsTheShift_IsKeptAsRaw()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 7.0 NULLPUNKT\n2 CYCL DEF 7.1 X+10\n3 CYCL DEF 10.0 DREHUNG\n"
            + "4 CYCL DEF 10.1 ROT+30\n5 CYCL DEF 7.0 NULLPUNKT\n6 CYCL DEF 7.1 X+20");

        Assert.Equal(2, program.Blocks.Count(block => block.Find("RAW") is not null));
    }

    // CYCL DEF 8 mirrors the named axes, 8.1 without axes cancels; CYCL DEF 10 rotates, ROT+0 cancels (heidenhain 3;
    // controller-mapping 1, MIRROR and ROTATE).
    [Fact]
    public void CyclDef8And10_AreMirrorAndRotate()
    {
        Assert.Equal(Lines("MIRROR=X,Y", "MIRROR=OFF", "ROTATE=30", "ROTATE=RESET"),
            Body("1 CYCL DEF 8.0 SPIEGELN\n2 CYCL DEF 8.1 X Y\n3 CYCL DEF 8.0 SPIEGELN\n4 CYCL DEF 8.1\n"
                + "5 CYCL DEF 10.0 DREHUNG\n6 CYCL DEF 10.1 ROT+30\n7 CYCL DEF 10.0 DREHUNG\n8 CYCL DEF 10.1 ROT+0"));
    }

    // Cycle 19 is TILT_AXIS, 19.1 without axes cancels (heidenhain 3, 7 rule 6; D82).
    [Fact]
    public void CyclDef19_IsTiltAxis()
    {
        Assert.Equal(Lines("TILT_AXIS A=0 B=45 C=0", "TILT_AXIS=RESET"),
            Body("1 CYCL DEF 19.0 BEARBEITUNGSEBENE\n2 CYCL DEF 19.1 A+0 B+45 C+0\n3 CYCL DEF 19.0 BEARBEITUNGSEBENE\n"
                + "4 CYCL DEF 19.1"));
    }

    // PLANE SPATIAL is TILT with MOVE and ROT from the options (heidenhain 7 rule 6; D82).
    [Fact]
    public void PlaneSpatial_IsTiltWithMoveAndRot()
    {
        Assert.Equal(Lines("TILT A=0 B=45 C=0 MOVE=TURN ROT=TABLE"),
            Body("1 PLANE SPATIAL SPA+0 SPB+45 SPC+0 TURN FMAX TABLE ROT"));
    }

    // PLANE AXIAL is TILT_AXIS; a new PLANE replaces the earlier one, PLANE RESET STAY removes it (heidenhain 3, 7 rule
    // 6; language 6; D82).
    [Fact]
    public void PlaneAxial_IsTiltAxisAndANewPlaneReplacesIt()
    {
        Assert.Equal(Lines("TILT_AXIS A=-90 C=180 MOVE=STAY", "TILT_AXIS=RESET", "TILT A=0 B=0 C=0 MOVE=STAY",
            "TILT=RESET"),
            Body("1 PLANE AXIAL A-90 C+180 STAY\n2 PLANE SPATIAL SPA+0 SPB+0 SPC+0 STAY\n3 PLANE RESET STAY"));
    }

    // MB MAX of a PLANE that turns the rotary axes is the RETRACT in front of the tilt (D83; controller-mapping 1).
    [Fact]
    public void PlaneAxial_WithMbMax_RetractsBeforeTheTilt()
    {
        Assert.Equal(Lines("RETRACT", "TILT_AXIS A=-90 C=180 MOVE=TURN"),
            Body("1 PLANE AXIAL A-90 C+180 TURN MB MAX FMAX"));
    }

    // PLANE RESET where no tilt is active writes TILT=RESET all the same.
    [Fact]
    public void PlaneReset_WithoutATilt_WritesTheReset()
    {
        Assert.Equal(Lines("TILT=RESET"), Body("1 PLANE RESET STAY"));
    }

    // The other PLANE forms, and the options without an NCX word, stay RAW (heidenhain 7 rule 6; controller-mapping 1
    // and 9).
    [Theory]
    [InlineData("1 PLANE EULER EULPR+0 EULNU45 EULROT0 STAY")]
    [InlineData("1 PLANE SPATIAL SPA+0 SPB+45 SPC+0 TURN MB MAX FMAX SEQ- TABLE ROT")]
    [InlineData("1 PLANE RESET TURN MB MAX FMAX")]
    public void PlaneOtherFormsOrOptions_AreKeptAsRaw(string snippet)
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram(snippet)));
    }

    // Cycle 32 is TOLERANCE with the rotary tolerance and the mode; T0 is TOLERANCE=OFF (heidenhain 2; language 6;
    // D85).
    [Fact]
    public void CyclDef32_IsTolerance()
    {
        Assert.Equal(Lines("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF"),
            Body("1 CYCL DEF 32.0 TOLERANZ\n2 CYCL DEF 32.1 T0.02\n3 CYCL DEF 32.2 HSC-MODE:0 TA0.05\n"
                + "4 CYCL DEF 32.0 TOLERANZ\n5 CYCL DEF 32.1 T0"));
    }

    // Cycle 9 is DWELL (controller-mapping 1, DWELL).
    [Fact]
    public void CyclDef9_IsDwell()
    {
        Assert.Equal(Lines("DWELL=1.5"), Body("1 CYCL DEF 9.0 VERWEILZEIT\n2 CYCL DEF 9.1 V.ZEIT 1.5"));
    }

    // The frames format to themselves and check without an ERROR on the three-axis mill (D91; virtual machine 3.4).
    [Fact]
    public void Frames_FormatToThemselvesAndCheckWithoutError()
    {
        string text = Text("0 BEGIN PGM F MM\n1 CYCL DEF 247 INIT. REF.PKT ~\n    Q339=1\n2 CYCL DEF 7.0 NULLPUNKT\n"
            + "3 CYCL DEF 7.1 X+60\n4 CYCL DEF 10.0 DREHUNG\n5 CYCL DEF 10.1 ROT+30\n6 CYCL DEF 8.0 SPIEGELN\n"
            + "7 CYCL DEF 8.1 X\n8 PLANE RESET STAY\n9 CYCL DEF 32.0 TOLERANZ\n10 CYCL DEF 32.1 T0.02\n11 M30\n"
            + "12 END PGM F MM\n");
        Diagnostics check = Check(text);

        AssertFormatsToItself(text);
        Assert.False(check.HasErrors, check.ToText());
    }
}
