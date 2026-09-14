using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The motion of Klartext (controllers heidenhain.md 2, 7 rules 1 and 5; controller-mapping 2; language 4.3, 6): L,
/// FMAX and F, IX, R0 RL RR, M91, CC with C, CR, CT, LP, CP, CP IPA beyond 360 degrees, LN, and CHF and RND expanded
/// into lines and arcs (D58).
/// </summary>
public sealed class HeidenhainMotionTests
{
    // Every block carries its verb: FMAX is RAPID, L with F or the modal feed LINE (heidenhain 7 rule 1;
    // controller-mapping 2).
    [Fact]
    public void L_WithFmaxFAndTheModalFeed_IsRapidAndLine()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=10 F=500", "LINE Y=10"),
            Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F500\n3 L Y+10"));
    }

    // IX is incremental, never modal, one form per axis (heidenhain 2; language 4.3).
    [Fact]
    public void IxPrefix_IsIncrementalPerWord()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE Y=5 IX=30 F=200"), Body("1 L X+0 Y+0 FMAX\n2 L IX+30 Y+5 F200"));
    }

    // RL, RR and R0 are COMP=LEFT, RIGHT and OFF (controller-mapping 2, COMP).
    [Fact]
    public void RlRrR0_AreCompensation()
    {
        Assert.Equal(Lines("LINE X=10 F=100 COMP=LEFT", "LINE X=20 COMP=RIGHT", "LINE X=30 COMP=OFF"),
            Body("1 L X+10 RL F100\n2 L X+20 RR\n3 L X+30 R0"));
    }

    // M91 in the block: coordinates from the machine datum, FRAME=MACHINE (controller-mapping 1).
    [Fact]
    public void M91_IsFrameMachine()
    {
        Assert.Equal(Lines("RAPID Z=0 FRAME=MACHINE", "RAPID X=0 Y=0 FRAME=MACHINE"),
            Body("1 L Z0 FMAX M91\n2 L X0 Y0 FMAX M91"));
    }

    // A Q parameter stands wherever a number stands, X+Q1 as {$Q1} (heidenhain 6; language 4.12).
    [Fact]
    public void QParameter_InACoordinate_IsAnExpression()
    {
        Assert.Equal(Lines("RAPID X={$Q1} Y={-$Q2}"), Body("1 L X+Q1 Y-Q2 FMAX"));
    }

    // CC and C become one ARC with an absolute CENTER (heidenhain 7 rule 5; language 6).
    [Fact]
    public void CcAndC_BecomeOneArcWithAnAbsoluteCenter()
    {
        Assert.Equal(Lines("LINE X=50.534 Y=69.993 F=100", "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50"),
            Body("1 L X+50,534 Y+69,993 F100\n2 CC X+50 Y+50\n3 C X+70 Y+50 DR+"));
    }

    // CC alone takes the current position as the pole, CC IX IY is incremental from it (heidenhain 2).
    [Fact]
    public void CcAloneAndIncremental_TakeThePoleFromTheCurrentPosition()
    {
        Assert.Equal(Lines(
            "LINE X=10 Y=10 F=100",
            "LINE X=20 Y=10",
            "ARC=CW X=0 Y=10 CENTER:X=10 CENTER:Y=10",
            "ARC=CCW X=20 Y=10 CENTER:X=5 CENTER:Y=10"),
            Body("1 L X+10 Y+10 F100\n2 CC\n3 L X+20 Y+10\n4 C X+0 Y+10 DR-\n5 CC IX+5 IY+0\n6 C X+20 Y+10 DR+"));
    }

    // C without a pole, and an arc without DR, stay RAW.
    [Theory]
    [InlineData("1 C X+10 Y+0 DR+")]
    [InlineData("1 CC X+0 Y+0\n2 C X+10 Y+0")]
    public void C_WithoutPoleOrDirection_IsKeptAsRaw(string snippet)
    {
        NcxProgram program = FramedProgram(snippet);

        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(program));
    }

    // CR becomes an ARC with R, positive for at most 180 degrees and negative for more, as in NCX (heidenhain 7 rule
    // 5; language 4.3).
    [Fact]
    public void Cr_BecomesArcWithR()
    {
        Assert.Equal(Lines("LINE X=7 Y=2 F=100", "ARC=CW X=2 Y=7 R=5", "ARC=CCW X=12 Y=7 R=-5"),
            Body("1 L X+7 Y+2 F100\n2 CR X+2 Y+7 R+5 DR-\n3 CR X+12 Y+7 R-5 DR+"));
    }

    // CT continues the element before it tangentially: from (10, 0) in the direction +X to (20, 10) it is the
    // counterclockwise quarter about (10, 10) (controller-mapping 2, CT converted to a CENTER arc).
    [Fact]
    public void Ct_IsConvertedToACenterArc()
    {
        Assert.Equal(Lines("LINE X=0 Y=0 F=100", "LINE X=10 Y=0", "ARC=CCW X=20 Y=10 CENTER:X=10 CENTER:Y=10"),
            Body("1 L X+0 Y+0 F100\n2 L X+10 Y+0\n3 CT X+20 Y+10"));
    }

    // CT after a motion whose direction the reader does not know stays RAW.
    [Fact]
    public void Ct_WithoutADirectionBeforeIt_IsKeptAsRaw()
    {
        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(FramedProgram("1 CT X+20 Y+10")));
    }

    // LP is converted to Cartesian with the pole: PR 10 at PA 30 about (0, 0) is (8.66, 5) (heidenhain 7 rule 5).
    [Fact]
    public void Lp_IsConvertedToCartesianWithThePole()
    {
        Assert.Equal(Lines("RAPID X=8.66 Y=5 COMP=OFF"), Body("1 CC X+0 Y+0\n2 LP PR+10 PA+30 R0 FMAX"));
    }

    // CP PA is converted to the Cartesian end point about the pole, with the radius of the current position
    // (heidenhain 2, 7 rule 5).
    [Fact]
    public void CpPa_IsConvertedToCartesian()
    {
        Assert.Equal(Lines("LINE X=60 Y=50 F=500", "ARC=CCW X=50 Y=60 CENTER:X=50 CENTER:Y=50"),
            Body("1 L X+60 Y+50 F500\n2 CC X+50 Y+50\n3 CP PA+90 DR+"));
    }

    // CP IPA beyond 360 degrees becomes ARC with ANGLE, the helix of TopSolid (heidenhain 7 rule 5; language 6; D84).
    [Fact]
    public void CpIpaBeyond360Degrees_BecomesArcWithAngle()
    {
        string text = Text("0 BEGIN PGM H MM\n1 L X+60 Y+50 Z+0 F500\n2 CC X+50 Y+50\n3 CP IPA+737.956 IZ-5.4 DR+\n"
            + "4 M30\n5 END PGM H MM\n");

        Assert.Equal(Lines("LINE X=60 Y=50 Z=0 F=500", "ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956"),
            BodyOf(text));
        AssertFormatsToItself(text);
    }

    // CP IPA with a sign against its DR stays RAW.
    [Fact]
    public void CpIpa_WithASignAgainstDr_IsKeptAsRaw()
    {
        Assert.Contains(DiagnosticCodes.KeptAsRaw,
            Codes(FramedProgram("1 L X+60 Y+50 F500\n2 CC X+50 Y+50\n3 CP IPA-90 DR+")));
    }

    // LN under M128 is LINE with the tool vector and the surface normal (controller-mapping 2; language 6; D81).
    [Fact]
    public void Ln_UnderM128_IsLineWithToolVectorAndNormal()
    {
        Assert.Equal(Lines("TCPM=ON",
            "LINE X=41.786 Y=-57.382 Z=95.488 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841"),
            Body("1 M128\n2 LN X+41.786 Y-57.382 Z+95.488 NX+0 NY+0 NZ+1 TX+0 TY+0.5 TZ+0.866 F2841"));
    }

    // LN without M128, and LN without its tool vector, stay RAW (D81).
    [Theory]
    [InlineData("1 LN X+1 Y+2 Z+3 TX+0 TY+0 TZ+1 F100")]
    [InlineData("1 M128\n2 LN X+1 Y+2 Z+3 NX+0 NY+0 NZ+1 F100")]
    public void Ln_WithoutM128OrToolVector_IsKeptAsRaw(string snippet)
    {
        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(FramedProgram(snippet)));
    }

    // F AUTO takes the feed of the tool table, which NCX does not know: RAW (heidenhain 2).
    [Fact]
    public void FAuto_IsKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram("1 L X+10 F AUTO")));
    }

    // A chamfer CHF between two lines is a line cutting the corner: the line before ends where the chamfer begins, and
    // the chamfer goes to where the line after it begins (D58, language 4.3; controllers heidenhain.md 2).
    [Fact]
    public void Chf_BetweenTwoLines_IsExpandedIntoALine()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=8 Y=0 F=100", "LINE X=10 Y=2", "LINE Y=10"),
            Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 CHF 2\n4 L Y+10"));
    }

    // A rounding RND between two lines is the arc of radius R tangent to both, turning with the corner: CCW for a turn
    // to the left, CW for a turn to the right (D58, language 4.3).
    [Theory]
    [InlineData("4 L Y+10", "ARC=CCW X=10 Y=2 R=2", "LINE Y=10")]
    [InlineData("4 L Y-10", "ARC=CW X=10 Y=-2 R=2", "LINE Y=-10")]
    public void Rnd_BetweenTwoLines_IsExpandedIntoAnArcTangentToBoth(string next, string arc, string line)
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=8 Y=0 F=100", arc, line),
            Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2\n" + next));
    }

    // The line after a rounding starts where the arc ends, and its IY counts from the corner point the line before
    // ends at: the reader writes it from the end of the arc, so that the path ends where the source's does (D58,
    // language 4.3).
    [Fact]
    public void Rnd_BeforeAnIncrementalLine_WritesItsIncrementalWordFromTheEndOfTheArc()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=8 Y=0 F=100", "ARC=CCW X=10 Y=2 R=2", "LINE IY=8", "LINE X=30"),
            Body("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L IY+10\n5 L X+30"));
    }

    // The same for a chamfer: IX+10 from the corner (30, 30) ends at X40, IX=9 from the end of the chamfer (D58).
    [Fact]
    public void Chf_BeforeAnIncrementalLine_WritesItsIncrementalWordFromTheEndOfTheChamfer()
    {
        Assert.Equal(
            Lines("RAPID X=0 Y=0", "LINE X=29.293 Y=29.293 F=100", "LINE X=31 Y=30", "LINE IX=9", "LINE Y=0"),
            Body("1 L X+0 Y+0 FMAX\n2 L X+30 Y+30 F100\n3 CHF 1\n4 L IX+10\n5 L Y+0"));
    }

    // A chamfer and a rounding on one contour: the line between them starts at the end of the chamfer and ends where
    // the rounding begins, and nothing stays RAW (D58).
    [Fact]
    public void ChfAndRnd_OnOneContour_AreExpandedWithoutRaw()
    {
        string source = "0 BEGIN PGM H MM\n1 L X+0 Y+0 F100\n2 L X+10\n3 CHF 2\n4 L Y+10\n5 RND R3\n6 L X+0\n7 M30\n"
            + "8 END PGM H MM\n";
        string text = Text(source);

        Assert.Equal(Lines("LINE X=0 Y=0 F=100", "LINE X=8 Y=0", "LINE X=10 Y=2", "LINE X=10 Y=7",
            "ARC=CCW X=7 Y=10 R=3", "LINE X=0"), BodyOf(text));
        Assert.DoesNotContain(DiagnosticCodes.KeptAsRaw, Codes(Program(source)));
        AssertFormatsToItself(text);
    }

    // A rounding between two lines in one direction has no corner to round and writes nothing (D58).
    [Fact]
    public void Rnd_BetweenTwoLinesInOneDirection_WritesNothing()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 F=100", "LINE X=20"),
            Body("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L X+20"));
    }

    // A CHF or RND with its own F stays RAW, and the lines about it are read as the source writes them (the
    // TODO(question) of HeidenhainCorners).
    [Theory]
    [InlineData("3 CHF 2 F50")]
    [InlineData("3 RND R2 F50")]
    public void ChfOrRnd_WithItsOwnFeed_IsKeptAsRaw(string corner)
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 F=100", $"RAW:HEIDENHAIN=\"{corner}\"", "LINE Y=10"),
            Body("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n" + corner + "\n4 L Y+10"));
    }

    // A corner the reader does not expand stays RAW with the lines about it as the source writes them: after a rapid
    // move, from a point the reader does not know, before an arc, before a line that leaves the working plane, longer
    // than a line of the corner, before a skipped line, before a line that changes the radius compensation (D58, D5).
    [Theory]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 FMAX\n3 CHF 2\n4 L Y+10")]
    [InlineData("2 L X+10 F100\n3 CHF 2\n4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 CR X+20 Y+0 R+5 DR+")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 CHF 2\n4 L Y+10 Z-5")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 CHF 20\n4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n/4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L Y+10 RL")]
    public void ChfOrRnd_WhereTheReaderDoesNotExpandTheCorner_IsKeptAsRaw(string snippet)
    {
        NcxProgram program = FramedProgram(snippet);

        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(program));
        Assert.Contains("RAW:HEIDENHAIN=\"3 ", Body(snippet), StringComparison.Ordinal);
    }

    // The expanded corners check without an ERROR (virtual machine 3.1, 3.2).
    [Fact]
    public void Corners_CheckWithoutError()
    {
        string text = Text("0 BEGIN PGM M MM\n1 TOOL CALL 1 Z S1000\n2 M3\n3 L X+0 Y+0 R0 FMAX\n4 L X+10 F100\n"
            + "5 CHF 2\n6 L Y+10\n7 RND R3\n8 L IX-10\n9 M30\n10 END PGM M MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }

    // The motion checks without an ERROR (virtual machine 3.1, 3.2).
    [Fact]
    public void Motion_ChecksWithoutError()
    {
        string text = Text("0 BEGIN PGM M MM\n1 TOOL CALL 1 Z S1000\n2 M3\n3 L X+60 Y+50 Z+0 R0 FMAX\n4 L X+60 F500\n"
            + "5 CC X+50 Y+50\n6 C X+40 Y+50 DR+\n7 CR X+50 Y+40 R+10 DR+\n8 CP IPA+720 IZ-2 DR+\n9 M30\n"
            + "10 END PGM M MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }
}
