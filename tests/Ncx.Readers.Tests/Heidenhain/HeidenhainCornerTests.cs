using Ncx.Core.Machine;
using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The chamfer CHF and the rounding RND between two contour elements (controllers heidenhain.md 2) have no NCX word:
/// the reader expands them into the explicit LINE and ARC blocks they stand for (language 4.3, D58), next to lines and
/// arcs, and keeps RAW what NCX cannot express, what the reader cannot compute and what the documents leave open (D5).
/// </summary>
public sealed class HeidenhainCornerTests
{
    // The arc after the rounding of the line along X into the clockwise arc about (14, 0): it keeps its end and its
    // centre and starts where the rounding ends.
    private const string CenterArc = "ARC=CW X=14 Y=4 CENTER:X=14 CENTER:Y=0";

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
    // language 4.3; D255).
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

    // A line at FMAX next to a corner is a rapid move, and the corner runs at the modal feed: FMAX holds for its own
    // block, F stays (controller-mapping 2, F; controllers heidenhain.md 2; D58).
    [Theory]
    [InlineData("2 L X+10 FMAX\n3 CHF 2\n4 L Y+10", "RAPID X=8 Y=0", "LINE X=10 Y=2", "LINE Y=10")]
    [InlineData("2 L X+10\n3 RND R2\n4 L Y+10 FMAX", "LINE X=8 Y=0", "ARC=CCW X=10 Y=2 R=2", "RAPID Y=10")]
    public void ChfOrRnd_NextToARapidLine_IsExpandedAndRunsAtTheModalFeed(string snippet, string before,
        string corner, string after)
    {
        Assert.Equal(Lines("LINE X=0 Y=0 F=100 COMP=OFF", before, corner, after),
            Body("1 L X+0 Y+0 R0 F100\n" + snippet));
    }

    // The line after the corner may change the radius compensation: the corner keeps the compensation of the element
    // before it, as the RND block does in the source (controller-mapping 2, COMP; D58).
    [Fact]
    public void Rnd_BeforeALineThatChangesTheCompensation_IsExpanded()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=8 Y=0 F=100 COMP=LEFT", "ARC=CCW X=10 Y=2 R=2",
            "LINE Y=10 COMP=OFF"), Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 RL F100\n3 RND R2\n4 L Y+10 R0"));
    }

    // A rounding between a line and an arc is the arc of radius R tangent to both, turning with the corner: from the
    // line along X to (10, 0) into the clockwise arc of radius 4 about (14, 0), the rounding R2.5 about (8, 2.5)
    // touches the line at (8, 0) and the arc at (10.308, 1.538), and the arc keeps its end and its centre (D58,
    // language 4.3). The pole may stand before the line, before the corner or after it; a pole by IX and IY, the
    // polar angle IPA of a CP and the incremental end of a CR count from the corner point (D255), and
    // an axis a C leaves out stays there; an arc by its radius CR keeps R, an arc of more than a turn its sweep ANGLE
    // less the part the rounding took (D84).
    [Theory]
    [InlineData("1 CC X+14 Y+0\n2 L X+0 Y+0 R0 FMAX\n3 L X+10 F100\n4 RND R2.5\n5 C X+18 DR-",
        "ARC=CW X=18 Y=0 CENTER:X=14 CENTER:Y=0")]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CR IX+4 IY+4 R+4 DR-",
        "ARC=CW IX=3.692 IY=2.462 R=4")]
    [InlineData("1 CC X+14 Y+0\n2 L X+0 Y+0 R0 FMAX\n3 L X+10 F100\n4 RND R2.5\n5 CP IPA-450 DR-",
        "ARC=CW CENTER:X=14 CENTER:Y=0 ANGLE=427.385")]
    [InlineData("1 CC X+14 Y+0\n2 L X+0 Y+0 R0 FMAX\n3 L X+10 F100\n4 RND R2.5\n5 C X+14 Y+4 DR-", CenterArc)]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 CC X+14 Y+0\n4 RND R2.5\n5 C X+14 Y+4 DR-", CenterArc)]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CC X+14 Y+0\n5 C X+14 Y+4 DR-", CenterArc)]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CC IX+4 IY+0\n5 C X+14 Y+4 DR-", CenterArc)]
    [InlineData("1 CC X+14 Y+0\n2 L X+0 Y+0 R0 FMAX\n3 L X+10 F100\n4 RND R2.5\n5 CP IPA-90 DR-", CenterArc)]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CR X+14 Y+4 R+4 DR-", "ARC=CW X=14 Y=4 R=4")]
    public void Rnd_BetweenALineAndAnArc_IsAnArcTangentToBoth(string snippet, string arc)
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=8 Y=0 F=100", "ARC=CCW X=10.308 Y=1.538 R=2.5", arc),
            Body(snippet));
    }

    // The same rounding from the arc into the line: the arc ends where the rounding begins, at (10.308, 1.538), and the
    // rounding turns clockwise to (8, 0), where the line begins; an arc of more than a turn, CP IPA beyond 360 degrees,
    // keeps its sweep ANGLE less the part the rounding took (D58, language 4.3, D84).
    [Theory]
    [InlineData("3 C X+10 Y+0 DR+ F100", "ARC=CCW X=10.308 Y=1.538 CENTER:X=14 CENTER:Y=0 F=100")]
    [InlineData("3 CP IPA+450 DR+ F100", "ARC=CCW CENTER:X=14 CENTER:Y=0 ANGLE=427.385 F=100")]
    public void Rnd_BetweenAnArcAndALine_IsAnArcTangentToBoth(string arcBlock, string arc)
    {
        Assert.Equal(Lines("RAPID X=14 Y=4 COMP=OFF", arc, "ARC=CW X=8 Y=0 R=2.5", "LINE X=0"),
            Body("1 L X+14 Y+4 R0 FMAX\n2 CC X+14 Y+0\n" + arcBlock + "\n4 RND R2.5\n5 L X+0"));
    }

    // A rounding between two arcs that turn the same way as the rounding has its centre at the radius of each arc less
    // R from its centre: the arcs of radius 10 about (-6, 0) and (6, 0) meet at (0, 8), and the rounding R2.5 about
    // (0, 4.5) touches them at (2, 6) and (-2, 6) (D58, language 4.3).
    [Fact]
    public void Rnd_BetweenTwoArcs_IsAnArcTangentToBoth()
    {
        Assert.Equal(Lines("RAPID X=4 Y=0 COMP=OFF", "ARC=CCW X=2 Y=6 CENTER:X=-6 CENTER:Y=0 F=100",
            "ARC=CCW X=-2 Y=6 R=2.5", "ARC=CCW X=-4 Y=0 CENTER:X=6 CENTER:Y=0"),
            Body("1 L X+4 Y+0 R0 FMAX\n2 CC X-6 Y+0\n3 C X+0 Y+8 DR+ F100\n4 RND R2.5\n5 CC X+6 Y+0\n6 C X-4 Y+0 DR+"));
    }

    // An arc by its radius that loses a part to the rounding may become a half turn or less, and its R turns positive:
    // the arc R-4 of 196 degrees about (14, 0) between (10, 0) and (17.84, -1.12) keeps 174 of them, after the corner
    // and before it (language 4.3, R; D58).
    [Theory]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CR X+17.84 Y-1.12 R-4 DR-",
        "RAPID X=0 Y=0 COMP=OFF\nLINE X=8 Y=0 F=100\nARC=CCW X=10.308 Y=1.538 R=2.5\nARC=CW X=17.84 Y=-1.12 R=4\n")]
    [InlineData("1 L X+17.84 Y-1.12 R0 FMAX\n2 CR X+10 Y+0 R-4 DR+ F100\n3 RND R2.5\n4 L X+0",
        "RAPID X=17.84 Y=-1.12 COMP=OFF\nARC=CCW X=10.308 Y=1.538 R=4 F=100\nARC=CW X=8 Y=0 R=2.5\nLINE X=0\n")]
    public void Rnd_NextToARadiusArcOfMoreThanAHalfTurn_TurnsItsRadiusPositive(string snippet, string expected)
    {
        Assert.Equal(expected, Body(snippet));
    }

    // A CC alone after the corner takes the corner point as its pole, as an incremental word after the corner counts
    // from it (D255), and the polar line LP after the corner ends at its point about that pole (D58).
    [Fact]
    public void Rnd_BeforeAPoleAloneAndAPolarLine_TakesTheCornerPointAsThePole()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=8 Y=0 F=100", "ARC=CCW X=10 Y=2 R=2", "LINE X=10 Y=10"),
            Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 RND R2\n4 CC\n5 LP PR+10 PA+90"));
    }

    // A chamfer next to an arc stays RAW, the elements about it as the source writes them: no document says what its
    // length measures along an arc (the TODO(question) of HeidenhainCorners).
    [Theory]
    [InlineData("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n3 CHF 2\n4 CC X+14 Y+0\n5 C X+14 Y+4 DR-",
        "RAPID X=0 Y=0 COMP=OFF\nLINE X=10 F=100\nRAW:HEIDENHAIN=\"3 CHF 2\"\n" + CenterArc + "\n")]
    [InlineData("1 L X+14 Y+4 R0 FMAX\n2 CC X+14 Y+0\n3 C X+10 Y+0 DR+ F100\n4 CHF 2\n5 L X+0",
        "RAPID X=14 Y=4 COMP=OFF\nARC=CCW X=10 Y=0 CENTER:X=14 CENTER:Y=0 F=100\nRAW:HEIDENHAIN=\"4 CHF 2\"\n"
            + "LINE X=0\n")]
    public void Chf_NextToAnArc_IsKeptAsRaw(string snippet, string expected)
    {
        Assert.Equal(expected, Body(snippet));
        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(FramedProgram(snippet)));
    }

    // A CT after a chamfer or a rounding continues an element whose end it decides itself: no document says what it is
    // tangent to, so the corner stays RAW, and the CT after it too, whose direction the reader then does not know (the
    // TODO(question) of HeidenhainNextElement).
    [Theory]
    [InlineData("3 CHF 2")]
    [InlineData("3 RND R2")]
    public void ChfOrRnd_BeforeATangentArc_IsKeptAsRaw(string corner)
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LINE X=10 F=100", $"RAW:HEIDENHAIN=\"{corner}\"",
                "RAW:HEIDENHAIN=\"4 CT X+20 Y+10\""),
            Body("1 L X+0 Y+0 R0 FMAX\n2 L X+10 F100\n" + corner + "\n4 CT X+20 Y+10"));
    }

    // A rounding that does not fit the elements about it stays RAW: larger than the arc it rounds on the inside, or
    // longer than the part of the arc next to the corner (D58, D5).
    [Theory]
    [InlineData("1 L X+4 Y+0 R0 FMAX\n2 CC X-6 Y+0\n3 C X+0 Y+8 DR+ F100\n4 RND R12\n5 CC X+6 Y+0\n6 C X-4 Y+0 DR+")]
    [InlineData("1 CC X+14 Y+0\n2 L X+0 Y+0 R0 FMAX\n3 L X+10 F100\n4 RND R2.5\n5 C X+10.061 Y+0.695 DR-")]
    public void Rnd_ThatDoesNotFitTheArcItRounds_IsKeptAsRaw(string snippet)
    {
        Assert.Contains("RAW:HEIDENHAIN=\"4 RND", Body(snippet), StringComparison.Ordinal);
        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(FramedProgram(snippet)));
    }

    // An arc that ends where it starts is a full circle or nothing, which D122 leaves open: a rounding next to it stays
    // RAW (the TODO(question) of HeidenhainCorners).
    [Fact]
    public void Rnd_NextToAnArcThatEndsWhereItStarts_IsKeptAsRaw()
    {
        Assert.Contains("RAW:HEIDENHAIN=\"4 RND R2\"",
            Body("1 L X+10 Y+0 R0 FMAX\n2 CC X+14 Y+0\n3 C X+10 Y+0 DR+ F100\n4 RND R2\n5 L X+0"),
            StringComparison.Ordinal);
    }

    // A CHF or RND with its own F stays RAW, and the lines about it are read as the source writes them (the
    // TODO(question) D255 of HeidenhainCorners).
    [Theory]
    [InlineData("3 CHF 2 F50")]
    [InlineData("3 RND R2 F50")]
    public void ChfOrRnd_WithItsOwnFeed_IsKeptAsRaw(string corner)
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 F=100", $"RAW:HEIDENHAIN=\"{corner}\"", "LINE Y=10"),
            Body("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n" + corner + "\n4 L Y+10"));
    }

    // A corner the reader does not expand stays RAW with the elements about it as the source writes them: from a point
    // the reader does not know, before a line that leaves the working plane, longer than a line of the corner, before a
    // skipped line, after a move in the machine frame, before a line whose feed F AUTO NCX does not know, before a line
    // with an M function that a reader rule may take (D58, D5, D66).
    [Theory]
    [InlineData("2 L X+10 F100\n3 CHF 2\n4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 CHF 2\n4 L Y+10 Z-5")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 CHF 20\n4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n/4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100 M91\n3 CHF 2\n4 L Y+10")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L Y+10 F AUTO")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L Y+10 M250")]
    public void ChfOrRnd_WhereTheReaderDoesNotExpandTheCorner_IsKeptAsRaw(string snippet)
    {
        NcxProgram program = FramedProgram(snippet);

        Assert.Contains(DiagnosticCodes.KeptAsRaw, Codes(program));
        Assert.Contains("RAW:HEIDENHAIN=\"3 ", Body(snippet), StringComparison.Ordinal);
    }

    // An element after the corner that the reader keeps RAW keeps the corner RAW too, and the element before it ends at
    // the corner point: written as the source writes it, the element counts from the corner point, which an expanded
    // corner would not reach (D58, D5). A retract M140 in a block that moves, M128 with the feed of its compensating
    // motion, and M99 calling a definition kept RAW (an OEM cycle, CYCL DEF 12.0 PGM CALL) or none.
    [Theory]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L IY+10 M140", "3 RND R2", "4 L IY+10 M140")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2.5\n4 CR X+17.84 Y-1.12 R-4 DR- M140", "3 RND R2.5",
        "4 CR X+17.84 Y-1.12 R-4 DR- M140")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 CHF 2\n4 L IY+10 M128 F1000", "3 CHF 2",
        "4 L IY+10 M128 F1000")]
    [InlineData("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L IY+10 M99", "3 RND R2", "4 L IY+10 M99")]
    [InlineData("1 CYCL DEF 399 OEM Q1=+1\n2 L X+0 Y+0 FMAX\n3 L X+10 F100\n4 RND R2\n5 L IY+10 M99", "4 RND R2",
        "5 L IY+10 M99")]
    [InlineData("1 CYCL DEF 12.0 PGM CALL\n2 CYCL DEF 12.1 PGM TNC:\\SUB.H\n3 L X+0 Y+0 FMAX\n4 L X+10 F100\n5 RND R2\n"
        + "6 L IY+10 M99", "5 RND R2", "6 L IY+10 M99")]
    public void ChfOrRnd_BeforeAnElementTheReaderKeepsAsRaw_IsKeptAsRaw(string snippet, string corner, string next)
    {
        Assert.EndsWith(Lines("LINE X=10 F=100", $"RAW:HEIDENHAIN=\"{corner}\"", $"RAW:HEIDENHAIN=\"{next}\""),
            Body(snippet), StringComparison.Ordinal);
    }

    // The same for an M function of a table of the machine whose state NCX writes with a value the M function does not
    // carry, M19 for ORIENT on an iTNC 530 whose spindle table names it (wave-1 question #62; D58, D5).
    [Fact]
    public void Rnd_BeforeALineWithATableStateThatStaysRaw_IsKeptAsRaw()
    {
        MachineConfig machine = MillWith("OFF = \"M5\"", "OFF = \"M5\"\nORIENT = \"M19\"");

        Assert.EndsWith(Lines("LINE X=10 F=100", "RAW:HEIDENHAIN=\"3 RND R2\"", "RAW:HEIDENHAIN=\"4 L IY+10 M19\""),
            Body("1 L X+0 Y+0 FMAX\n2 L X+10 F100\n3 RND R2\n4 L IY+10 M19", machine), StringComparison.Ordinal);
    }

    // M99 on the element after the corner calls a definition of the cycle catalog at the end of the element, where the
    // source calls it: the corner is expanded and the CYCLE_CALL follows the element, a line or an arc (D58;
    // controllers heidenhain.md 5).
    [Theory]
    [InlineData("2 L X+0 Y+0 FMAX\n3 L X+10 F100\n4 RND R2\n5 L IY+10 M99", "ARC=CCW X=10 Y=2 R=2", "LINE IY=8")]
    [InlineData("2 CC X+14 Y+0\n3 L X+0 Y+0 FMAX\n4 L X+10 F100\n5 RND R2.5\n6 C X+14 Y+4 DR- M99",
        "ARC=CCW X=10.308 Y=1.538 R=2.5", CenterArc)]
    public void Rnd_BeforeAnElementWithM99OfACatalogCycle_IsExpandedAndTheCallFollowsTheElement(string snippet,
        string corner, string element)
    {
        string definition = "1 CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
            + "    Q204=2\n";

        Assert.EndsWith(Lines("LINE X=8 Y=0 F=100", corner, element, "CYCLE_CALL"), Body(definition + snippet),
            StringComparison.Ordinal);
    }

    // The expanded corners between lines check without an ERROR (virtual machine 3.1, 3.2).
    [Fact]
    public void Corners_CheckWithoutError()
    {
        string text = Text("0 BEGIN PGM M MM\n1 TOOL CALL 1 Z S1000\n2 M3\n3 L X+0 Y+0 R0 FMAX\n4 L X+10 F100\n"
            + "5 CHF 2\n6 L Y+10\n7 RND R3\n8 L IX-10\n9 M30\n10 END PGM M MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }

    // The roundings next to arcs, an arc between two of them, check without an ERROR and write nothing RAW (virtual
    // machine 3.1, 3.2; D58).
    [Fact]
    public void Corners_NextToArcs_CheckWithoutErrorAndWriteNoRaw()
    {
        string source = "0 BEGIN PGM M MM\n1 TOOL CALL 1 Z S1000\n2 M3\n3 CC X+14 Y+0\n4 L X+0 Y+0 R0 FMAX\n"
            + "5 L X+10 F100\n6 RND R2.5\n7 C X+14 Y+4 DR-\n8 RND R1\n9 L X+20 Y+0\n10 RND R2\n11 L X+30\n12 M30\n"
            + "13 END PGM M MM\n";
        string text = Text(source);
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
        Assert.DoesNotContain(DiagnosticCodes.KeptAsRaw, Codes(Program(source)));
        AssertFormatsToItself(text);
    }
}
