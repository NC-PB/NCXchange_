using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The machining cycles (controllers heidenhain.md 5, 7 rule 7; controller-mapping 5; language 4.7, 4.7.1): CYCL DEF
/// into CYCLE with absolute coordinates, CYCL CALL, M99 and CYCL CALL PAT into CYCLE_CALL, CYCLE=OFF before the next
/// non-cycle motion, the native form, RAW.
/// </summary>
public sealed class HeidenhainCycleTests
{
    private const string Drill = "2 CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
        + "    Q204=2";

    private const string DrillWords =
        "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10 SAFE=2 CYCLE_RETRACT=SAFE CYCLE_F=100";

    // The definition becomes CYCLE with SURFACE = Q203, CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201, SAFE = Q203 +
    // Q204 (heidenhain 5, 7 rule 7); the Q parameters no word carries are reported.
    [Fact]
    public void CyclDef200_BecomesDrillWithCoordinatesAbsoluteFromQ203()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 200 BOHREN ~\n    Q200=+2 ~\n    Q201=-20 ~\n    Q206=+150 ~\n"
            + "    Q202=+5 ~\n    Q210=+0 ~\n    Q203=-10 ~\n    Q204=+50 ~\n    Q211=+0,5\n2 CYCL CALL");

        Assert.Equal(Lines(
            "CYCLE=DRILL SURFACE=-10 CLEARANCE=-8 DEPTH=-30 SAFE=40 CYCLE_RETRACT=SAFE CYCLE_F=150 CYCLE_DWELL=0.5",
            "CYCLE_CALL"), BodyOf(Ncx.Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.HeidenhainCycleParameterNotCarried], Codes(program));
    }

    // Without Q204 the cycle returns to CLEARANCE (controller-mapping 5, CYCLE_RETRACT); cycle 207 is TAP with Q239 as
    // PITCH.
    [Fact]
    public void CyclDef207_WithoutQ204_IsTapBackToClearance()
    {
        Assert.Equal(Lines("CYCLE=TAP SURFACE=0 CLEARANCE=2 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PITCH=1.5"),
            Body("1 CYCL DEF 207 GEW.-BOHREN GS NEU ~\n    Q200=2 ~\n    Q201=-20 ~\n    Q239=1,5 ~\n    Q203=0"));
    }

    // Q204 that puts SAFE on CLEARANCE is SAFE with CYCLE_RETRACT=SAFE until the question of phase 3 P3-05 is answered
    // (controller-mapping 5; Expected/BOHREN.ncx keeps the Fanuc reading).
    [Fact]
    public void Q204_EqualToTheClearance_IsSafeWithCycleRetractSafe()
    {
        Assert.Equal(Lines(DrillWords), Body(Drill));
    }

    // M99 in a rapid positioning in the plane is the CYCLE_CALL at its position; the definition stays until the next
    // CYCL DEF, and CYCLE=OFF stands before the next non-cycle motion (heidenhain 5, 7 rule 7).
    [Fact]
    public void M99_InARapidPositioning_IsTheCallAndCycleOffStandsBeforeTheNextMotion()
    {
        Assert.Equal(Lines(
            "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1000",
            DrillWords,
            "CYCLE_CALL X=10 Y=10 COMP=OFF",
            "CYCLE_CALL X=20",
            "CYCLE=OFF",
            "RAPID Z=50"),
            Body("1 TOOL CALL 1 Z S1000\n" + Drill + "\n3 L X+10 Y+10 R0 FMAX M99\n4 L X+20 FMAX M99\n5 L Z+50 FMAX"));
    }

    // M99 in a positioning at the feed or along the tool axis moves first and calls at the point it reached.
    [Fact]
    public void M99_InAFeedOrToolAxisPositioning_MovesThenCalls()
    {
        Assert.Equal(Lines(DrillWords, "LINE X=10 F=200", "CYCLE_CALL", "RAPID X=20 Z=5", "CYCLE_CALL"),
            Body(Drill + "\n3 L X+10 F200 M99\n4 L X+20 Z+5 FMAX M99"));
    }

    // The positioning between the definition and its first call does not switch the cycle off; a call after CYCLE=OFF
    // writes the definition again, which the control keeps until the next CYCL DEF (heidenhain 5).
    [Fact]
    public void Call_AfterTheCycleWasSwitchedOff_WritesTheDefinitionAgain()
    {
        Assert.Equal(Lines(DrillWords, "RAPID X=10 Y=10", "CYCLE_CALL", "CYCLE=OFF", "RAPID Z=50", DrillWords,
            "CYCLE_CALL X=30"),
            Body(Drill + "\n3 L X+10 Y+10 FMAX\n4 CYCL CALL\n5 L Z+50 FMAX\n6 L X+30 FMAX M99"));
    }

    // CYCL CALL PAT calls at every point of the PATTERN DEF, one CYCLE_CALL per point (heidenhain 7 rule 7).
    [Fact]
    public void CyclCallPat_ExpandsToOneCallPerPoint()
    {
        string text = Text("0 BEGIN PGM P MM\n1 PATTERN DEF ~\n    POS1 (X+25 Y+33,5 Z+0) ~\n    POS2 (X+50 Y+75 Z+0)\n"
            + Drill + "\n3 CYCL CALL PAT FMAX\n4 M30\n5 END PGM P MM\n");

        Assert.Equal(Lines(DrillWords, "CYCLE_CALL X=25 Y=33.5", "CYCLE_CALL X=50 Y=75"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // A PATTERN DEF no CYCL CALL PAT calls stays RAW (heidenhain 7 rule 9).
    [Fact]
    public void PatternDef_WithoutACall_IsKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram("1 PATTERN DEF ~\n    POS1 (X+25 Y+33 Z+0)")));
    }

    // A call without a definition is an ERROR of the source and stays RAW (heidenhain 5).
    [Fact]
    public void CyclCall_WithoutADefinition_IsAnErrorAndKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.HeidenhainCycleCallWithoutDefinition, DiagnosticCodes.KeptAsRaw],
            Codes(FramedProgram("1 CYCL CALL")));
    }

    // A catalog cycle whose parameters are no words of the language is written natively, CYCLE:HEIDENHAIN=n with its Q
    // parameters in source order (language 4.7.1, D94).
    [Fact]
    public void CyclDef251_IsWrittenNatively()
    {
        string text = Text("0 BEGIN PGM N MM\n1 CYCL DEF 251 RECHTECKTASCHE ~\n    Q215=0 ~\n    Q218=60 ~\n    Q219=40 ~\n"
            + "    Q201=-10\n2 M30\n3 END PGM N MM\n");

        Assert.Equal(Lines("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60 Q219=40 Q201=-10"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // A cycle the catalog does not know, an OEM cycle, stays RAW, and so do its calls (heidenhain 7 rule 9).
    [Fact]
    public void CyclDef_OfACycleTheCatalogDoesNotKnow_IsKeptAsRawWithItsCalls()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 361 STANDARD TFB ~\n    Q1=1\n2 CYCL CALL");

        Assert.Equal(3, program.Blocks.Count(block => block.Find("RAW") is not null));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // A CYCL DEF the reader keeps as RAW, CYCL DEF 12.0 PGM CALL over two blocks, replaces the active definition all the
    // same, since the definition stays active until the next CYCL DEF: the M99 after it stays RAW and does not call the
    // cycle before it (heidenhain 5, 7 rule 9).
    [Fact]
    public void CyclDef_KeptAsRawOverSeveralBlocks_ReplacesTheDefinitionAndItsCallsStayRaw()
    {
        NcxProgram program = FramedProgram(Drill + "\n3 L X+10 Y+10 Z+5 R0 FMAX M99\n4 CYCL DEF 12.0 PGM CALL\n"
            + "5 CYCL DEF 12.1 PGM TNC:\\SUB.H\n6 L X+20 FMAX M99\n7 L X+30 FMAX M99");

        Assert.Equal(1, program.Blocks.Count(block => block.Verb?.Key == "CYCLE_CALL"));
        Assert.Equal(4, program.Blocks.Count(block => block.Find("RAW") is not null));
        Assert.Equal(Enumerable.Repeat(DiagnosticCodes.KeptAsRaw, 4), Codes(program));
    }

    // An older cycle written over several blocks, CYCL DEF 1.0 TIEFBOHREN to 1.5, is the definition the CYCL CALL after
    // it calls: the call stays RAW with its definition, and the valid program has no ERROR (heidenhain 5, 7 rule 9).
    [Fact]
    public void CyclCall_OfAnOlderCycleOverSeveralBlocks_IsKeptAsRawWithoutAnError()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 1.0 TIEFBOHREN\n2 CYCL DEF 1.1 ABST 2\n3 CYCL DEF 1.2 TIEFE -20\n"
            + "4 CYCL DEF 1.3 ZUSTLG 5\n5 CYCL DEF 1.4 V.ZEIT 0\n6 CYCL DEF 1.5 F100\n7 CYCL CALL");

        Assert.Equal(Enumerable.Repeat(DiagnosticCodes.KeptAsRaw, 7), Codes(program));
    }

    // A CYCL DEF whose cycle number the reader cannot read stays RAW and replaces the active definition all the same:
    // the M99 after it does not call the cycle before it (heidenhain 5).
    [Fact]
    public void CyclDef_WithoutACycleNumber_ReplacesTheDefinitionAndItsCallsStayRaw()
    {
        NcxProgram program = FramedProgram(Drill + "\n3 L X+10 Y+10 R0 FMAX M99\n4 CYCL DEF\n5 L X+20 FMAX M99");

        Assert.Equal(1, program.Blocks.Count(block => block.Verb?.Key == "CYCLE_CALL"));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // The drilling of BOHREN.h checks without an ERROR (virtual machine 3.3).
    [Fact]
    public void Drilling_ChecksWithoutError()
    {
        string text = Text("0 BEGIN PGM D MM\n1 TOOL CALL 1 Z S1000\n" + Drill + "\n3 M3\n4 L X+10 Y+10 R0 FMAX\n"
            + "5 L Z+5 FMAX M99\n6 L X+20 FMAX M99\n7 L Z+50 FMAX\n8 M30\n9 END PGM D MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }
}
