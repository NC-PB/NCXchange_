using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 2 of controllers siemens.md 12 and the motion of the Siemens column of controller-mapping 2: the equals sign,
/// the arcs, the sweeps; the modal words, the feed, the tool vector, the dwell and the machine frame.
/// </summary>
public sealed class SiemensMotionTests
{
    // Siemens 12 rule 2: = after every address with an extension, the axis Z2 of the sub spindle slide (siemens 1;
    // controller-mapping 2, IX; millturn1.toml).
    [Fact]
    public void ExtendedAxisZ2_Rapid_IsWrittenWithTheEqualsSign()
    {
        Assert.Equal(Lines("G0 Z2=-58"), MillTurnBody("RAPID Z2=-58"));
    }

    // Every motion block writes its motion code, as the comments of MILLTURN_TRANSFER write G0 Z2=-58 after a G0 (the
    // TODO(question) of SiemensMotion); an X of the mill-turn under DIAMETER=ON is a diameter, as its X axis is written
    // (D60).
    [Fact]
    public void MotionCode_OnEveryMotionBlock_IsWrittenAgain()
    {
        Assert.Equal(Lines("G0 X42 Z2", "G1 X40 F0.2", "G1 Z-60", "G0 X45"),
            MillTurnBody("RAPID X=42 Z=2", "LINE X=40 F=0.2", "LINE Z=-60", "RAPID X=45"));
    }

    // Siemens 1 and 12 rule 2: a value that is an expression stands after an equals sign, X=R1*2 (controller-mapping 2,
    // F; language 4.12).
    [Fact]
    public void Expression_AsAxisValue_IsWrittenAfterAnEqualsSign()
    {
        Assert.Equal(Lines("R1=10", "G0 X=R1*2"), MillBody("VAR:R1=10", "RAPID X={$R1 * 2}"));
    }

    // Controller-mapping 2, IX: an incremental word is IC() per word; the compiler writes no G91.
    [Fact]
    public void IncrementalWord_IsWrittenWithIC()
    {
        Assert.Equal(Lines("G0 X10 Y0", "G1 X=IC(5) F100"), MillBody("RAPID X=10 Y=0", "LINE IX=5 F=100"));
    }

    // Siemens 12 rule 2: I=AC() for an absolute center (controller-mapping 2, CENTER:X).
    [Fact]
    public void AbsoluteCenter_IsWrittenAsIAC()
    {
        Assert.Equal(Lines("G0 X60 Y50", "G3 X50 Y60 I=AC(50) J=AC(50) F200"),
            MillBody("RAPID X=60 Y=50", "ARC=CCW X=50 Y=60 CENTER:X=50 CENTER:Y=50 F=200"));
    }

    // Controller-mapping 2, CENTER:IX: I J K incremental from the start point.
    [Fact]
    public void IncrementalCenter_IsWrittenAsIJ()
    {
        Assert.Equal(Lines("G0 X60 Y50", "G2 X40 Y50 I-10 J0 F200"),
            MillBody("RAPID X=60 Y=50", "ARC=CW X=40 Y=50 CENTER:IX=-10 CENTER:IY=0 F=200"));
    }

    // Siemens 12 rule 2: CR= for the R form, negative for the arc over 180 degrees.
    [Fact]
    public void RadiusForm_IsWrittenAsCR()
    {
        Assert.Equal(Lines("G0 X60 Y50", "G2 X50 Y40 CR=-10 F200"),
            MillBody("RAPID X=60 Y=50", "ARC=CW X=50 Y=40 R=-10 F=200"));
    }

    // Controller-mapping 2, ANGLE: AR= the opening angle with the center, for a sweep below one turn (D84).
    [Fact]
    public void SweepBelowOneTurn_IsWrittenAsAR()
    {
        Assert.Equal(Lines("G0 X60 Y50", "G3 I=AC(50) J=AC(50) AR=90 F200"),
            MillBody("RAPID X=60 Y=50", "ARC=CCW CENTER:X=50 CENTER:Y=50 ANGLE=90 F=200"));
    }

    // Siemens 12 rule 2: TURN= for sweeps beyond a turn, with the end point the virtual machine computes (siemens 3:
    // TURN=2 adds two full turns before the end point; D84).
    [Fact]
    public void SweepBeyondOneTurn_IsWrittenWithTheEndPointAndTurn()
    {
        Assert.Equal(Lines("G0 X60 Y50 Z0", "G3 X59.513 Y53.083 Z-5.4 I=AC(50) J=AC(50) TURN=2 F200"),
            MillBody("RAPID X=60 Y=50 Z=0", "ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956 F=200"));
    }

    // Siemens 12 rule 2: on an older 840D the sweep is split into turns, each back to its start with the tool axis
    // advanced by its share, then the rest.
    [Fact]
    public void SweepBeyondOneTurnOnAnOlder840D_IsSplitIntoTurns()
    {
        CompileResult result = Run(Program(MillHeader, "RAPID X=60 Y=50 Z=0",
            "ARC=CCW IZ=-6 CENTER:X=50 CENTER:Y=50 ANGLE=720 F=200"),
            Replaced(MillFile, "dialect = \"840Dsl\"", "dialect = \"840D\""));

        Assert.Equal(Lines("G0 X60 Y50 Z0", "G3 X60 Y50 Z-3 I=AC(50) J=AC(50) F200", "X60 Y50 Z-6 I=AC(50) J=AC(50)"),
            Body(result));
    }

    // Controller-mapping 1 and 2: G17 to G19, G94 and G95, G40 to G42 and G70 or G71 where the control has another
    // active; a modal word is written only on change (phase 3, P3-03).
    [Fact]
    public void ModalWords_WrittenAgain_AreWrittenOnlyOnChange()
    {
        Assert.Equal(Lines("G18", "G95"), MillBody("WORKPLANE=XY FEED_MODE=PER_MIN", "WORKPLANE=ZX",
            "FEED_MODE=PER_REV"));
    }

    // Language 4.3, F: the feed where the control has another active.
    [Fact]
    public void Feed_OfTheSameValue_IsWrittenOnce()
    {
        Assert.Equal(Lines("G1 X10 F100", "G1 X20", "G1 X30 F200"),
            MillBody("LINE X=10 F=100", "LINE X=20 F=100", "LINE X=30 F=200"));
    }

    // Controller-mapping 2, COMP: G41 left, G42 right, G40 off, from the motion of the same block.
    [Fact]
    public void Compensation_LeftThenOff_IsG41ThenG40()
    {
        Assert.Equal(Lines("T1 M6", "D1", "G1 G41 X10 F100", "G1 G40 X20"),
            MillBody("TOOL=1 OFFSET=1", "LINE X=10 F=100 COMP=LEFT", "LINE X=20 COMP=OFF"));
    }

    // Controller-mapping 2, tool vectors: TX TY TZ as A3= B3= C3=, NX NY NZ as the normal at the end of the block A5=
    // B5= C5= under TRAORI (siemens 6; D81).
    [Fact]
    public void ToolVector_UnderTcpm_IsA3B3C3WithTheNormalA5B5C5()
    {
        CompileResult result = Run(Program(MillHeader, "TCPM=ON",
            "LINE X=41.786 Y=-57.382 Z=95.488 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841"), FiveAxis());

        Assert.Equal(Lines("TRAORI", "G1 X41.786 Y-57.382 Z95.488 A3=0 B3=0.5 C3=0.866 A5=0 B5=0 C5=1 F2841"),
            Body(result));
    }

    // Machine-config 5, ROTARY_PATH_SHORTEST: "rotary axes with DC() by the compiler" (D86).
    [Fact]
    public void RotaryPathShortest_RotaryAxis_IsWrittenWithDC()
    {
        Assert.Equal(Lines("G0 C=DC(270)"), Body(Run(Program(MillHeader, "ROTARY_PATH=SHORTEST", "RAPID C=270"),
            FiveAxis())));
    }

    // Controller-mapping 1, DWELL: G4 F in seconds in a block of its own; the F of the dwell is not the feed.
    [Fact]
    public void Dwell_IsG4FAndLeavesTheFeed()
    {
        Assert.Equal(Lines("G1 X10 F100", "G4 F1.5", "G1 X20"),
            MillBody("LINE X=10 F=100", "DWELL=1.5", "LINE X=20"));
    }

    // Controller-mapping 1, UNITS: G71 for millimetres, G70 for inches (the TODO(question) of SiemensMotion, the pair).
    [Fact]
    public void UnitsInch_InTheHeader_IsG70()
    {
        CompileResult result = Run(Program(MillHeader.Replace("UNITS=MM", "UNITS=INCH", StringComparison.Ordinal)),
            Mill());

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G70", "M30"), TextOf(result));
    }
}
