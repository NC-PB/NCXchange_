using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 2, motion (controllers fanuc.md 3, 4, 9 rule 1; language 4.3): the verb on
/// every block, X= and IX=, the centre and the radius of an arc, the helix, F and FEED_MODE, COMP, G16 polar
/// coordinates, chamfers and roundings (D58).
/// </summary>
public sealed class FanucMotionTests
{
    // G0 is modal and the reader writes the verb on every block (controller-mapping 2, RAPID).
    [Fact]
    public void G0_IsRapidOnEveryBlock()
    {
        Assert.Equal(Lines("RAPID X=10", "RAPID Y=5"), Body("G0 X10.\nY5."));
    }

    // G1 is LINE at F (controller-mapping 2).
    [Fact]
    public void G1_IsLineWithItsFeed()
    {
        Assert.Equal(Lines("LINE X=10 F=200"), Body("G1 X10. F200"));
    }

    // G2 is ARC=CW, R the signed radius (controller-mapping 2).
    [Fact]
    public void G2_WithR_IsArcCwWithTheRadius()
    {
        Assert.Equal(Lines("ARC=CW X=10 Y=0 R=5 F=100"), Body("G2 X10. Y0 R5. F100"));
    }

    // CENTER:X is computed from I J K and the start point (controller-mapping 2; language 6, the centre arc of
    // 2.5D_FRAESEN).
    [Fact]
    public void G3_WithIJFromAKnownStart_HasTheAbsoluteCentre()
    {
        Assert.Equal(
            Lines("RAPID X=50.534 Y=69.993", "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50 F=100"),
            Body("G0 X50.534 Y69.993\nG3 X70. Y50. I-.534 J-19.993 F100"));
    }

    // Under G91 I J K stay the incremental centre, CENTER:IX (controller-mapping 2).
    [Fact]
    public void G3_UnderG91_KeepsTheIncrementalCentre()
    {
        Assert.Equal(
            Lines("ARC=CCW IX=10 IY=10 CENTER:IX=10 CENTER:IY=0 F=100"),
            Body("G91 G3 X10. Y10. I10. J0 F100"));
    }

    // A centre word left out is 0, and a third axis in the arc block makes a helix (controllers fanuc.md 4).
    [Fact]
    public void G3_WithoutJAndWithZ_IsAHelixAboutTheCentre()
    {
        Assert.Equal(
            Lines("RAPID X=10 Y=0 Z=0", "ARC=CCW X=10 Y=0 Z=-5 CENTER:X=0 CENTER:Y=0 F=100"),
            Body("G0 X10. Y0 Z0\nG3 X10. Y0 Z-5. I-10. F100"));
    }

    // A G91 block becomes IX, IY words; G90 is absolute again and writes no word (controller-mapping 2).
    [Fact]
    public void G91_WordsAreIncremental_UntilG90()
    {
        Assert.Equal(Lines("LINE IX=10 F=100", "LINE X=5"), Body("G91 G1 X10. F100\nG90 X5."));
    }

    // U W H V are the incremental X Z C Y of system A (controller-mapping 2, IX=).
    [Fact]
    public void UAndW_OfSystemA_AreIncrementalXAndZ()
    {
        Assert.Equal(Lines("LINE IX=5 IZ=-2 F=0.1"), Body("G1 U5. W-2. F0.1", Lathe()));
    }

    // An extended axis name, C2=, is the machine axis C2 (controller-mapping 2, D30, D93).
    [Fact]
    public void ExtendedAxisName_C2_IsTheMachineAxisC2()
    {
        Assert.Equal(Lines("RAPID C2=90"), Body("G0 C2=90.", Lathe()));
    }

    // G95 is FEED_MODE=PER_REV on a mill, G99 on system A (controller-mapping 2, FEED_MODE).
    [Fact]
    public void G95AndG99OfSystemA_AreFeedModePerRev()
    {
        Assert.Equal(Lines("RAPID X=1", "FEED_MODE=PER_REV"), Body("G0 X1.\nG95"));
        Assert.Equal(Lines("RAPID X=1", "FEED_MODE=PER_REV"), Body("G0 X1.\nG99", Lathe()));
    }

    // G41 D1 is COMP=LEFT with the radius offset where D stands (controller-mapping 2, COMP; D7).
    [Fact]
    public void G41D1_IsCompLeftWithTheRadiusOffset()
    {
        Assert.Equal(Lines("LINE X=10 F=100 OFFSET:RAD=1 COMP=LEFT"), Body("G1 G41 X10. D1 F100"));
    }

    // A modal code that changes nothing writes no word: G40 while the compensation is off.
    [Fact]
    public void G40_WhileCompIsOff_WritesNoWord()
    {
        Assert.Equal(Lines("RAPID X=1", "RAPID X=2"), Body("G0 X1.\nG40 X2."));
    }

    // Under G16 X is the radius and Y the angle, converted to Cartesian (controllers fanuc.md 4).
    [Fact]
    public void G16_PolarCoordinates_AreConvertedToCartesian()
    {
        Assert.Equal(Lines("LINE X=0 Y=10 F=100", "LINE X=-10 Y=0"), Body("G16\nG1 X10. Y90. F100\nY180.\nG15"));
    }

    // A block before any G0 to G3 moves at RAPID.
    [Fact]
    public void AxisWords_BeforeAnyMotionCode_AreRapid()
    {
        Assert.Equal(Lines("RAPID X=10"), Body("X10."));
    }

    // A chamfer ,C between two lines is a line cutting the corner (D58, language 4.3).
    [Fact]
    public void Chamfer_C2BetweenTwoLines_IsExpandedIntoALine()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "LINE X=8 Y=0 F=100", "LINE X=10 Y=2", "LINE Y=10"),
            Body("G0 X0 Y0\nG1 X10. F100 ,C2.\nY10."));
    }

    // A rounding ,R between two lines is an arc tangent to both, turning with the corner (D58).
    [Fact]
    public void Rounding_R2BetweenTwoLines_IsExpandedIntoAnArc()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "LINE X=8 Y=0 F=100", "ARC=CCW X=10 Y=2 R=2", "LINE Y=10"),
            Body("G0 X0 Y0\nG1 X10. F100 ,R2.\nY10."));
    }

    // The line after a rounding starts where the arc ends, and the source's G91 word counts from the corner: the reader
    // writes it from the end of the arc, so that the path ends where the source's does (D58, language 4.3).
    [Fact]
    public void Rounding_BeforeAG91Line_WritesItsIncrementalWordFromTheEndOfTheArc()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "LINE X=8 Y=0 F=100", "ARC=CCW X=10 Y=2 R=2", "LINE IY=8", "LINE X=30"),
            Body("G0 X0 Y0\nG1 X10. ,R2. F100\nG91 Y10.\nG90 X30."));
    }

    // The same for a chamfer: G91 X10. from the corner (30, 30) ends at X40, IX=9 from the end of the chamfer (D58).
    [Fact]
    public void Chamfer_BeforeAG91Line_WritesItsIncrementalWordFromTheEndOfTheChamfer()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "LINE X=29.293 Y=29.293 F=100", "LINE X=31 Y=30", "LINE IX=9", "LINE Y=0"),
            Body("G0 X0 Y0\nG1 X30. Y30. ,C1. F100\nG91 X10.\nG90 Y0"));
    }

    // An incremental line after a corner that a jump also enters counts from the corner on one way and from elsewhere
    // on the other; no expansion stands for both, and the corner block stays RAW (D58, D5).
    [Fact]
    public void Rounding_BeforeAnIncrementalLineAJumpEnters_IsKeptAsRaw()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0",
                "RAW:FANUC=\"G1 X10. ,R2. F100\"",
                "LABEL=5",
                "LINE IY=10 F=100",
                "LINE X=30",
                "JUMP=5"),
            Body("G0 X0 Y0\nG1 X10. ,R2. F100\nN5 G91 Y10.\nG90 X30.\nGOTO 5"));
    }

    // A block skip may leave the line after a corner out, and the control then turns the corner toward another line; no
    // expansion stands for both, and the corner block stays RAW (D58, D5).
    [Fact]
    public void Rounding_BeforeASkippedLine_IsKeptAsRaw()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "RAW:FANUC=\"G1 X10. ,R2. F100\"", "SKIP LINE Y=10 F=100", "LINE X=20"),
            Body("G0 X0 Y0\nG1 X10. ,R2. F100\n/Y10.\nX20."));
    }

    // A chamfer before an arc is not expanded; the block stays RAW and the arc after it moves with the F of the
    // control.
    [Fact]
    public void Chamfer_BeforeAnArc_IsKeptAsRaw()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "RAW:FANUC=\"G1 X10. F100 ,C2.\"", "ARC=CW X=20 Y=0 R=5 F=100"),
            Body("G0 X0 Y0\nG1 X10. F100 ,C2.\nG2 X20. Y0 R5."));
    }
}
