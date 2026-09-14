using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The labels, calls, jumps and Q parameters (controllers heidenhain.md 1, 6, 7 rule 3; controller-mapping 6; language
/// 4.9, 4.13; virtual machine 3.9): LBL as SUB or LABEL by usage, CALL LBL, CALL LBL REP, CALL PGM, FN 9 to FN 12, FN 0
/// to FN 5, formulas.
/// </summary>
public sealed class HeidenhainFlowTests
{
    private const string Drill10 = "CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
        + "    Q204=2";

    // LBL n closed by LBL 0 and called without REP is a SUB section of the file (heidenhain 7 rule 3; language
    // 4.13).
    [Fact]
    public void Lbl_CalledWithoutRepAndClosedByLbl0_IsASubSection()
    {
        string text = Text("0 BEGIN PGM S MM\n1 CALL LBL 1\n2 M30\n3 LBL 1\n4 L IX+10 F100\n5 LBL 0\n6 END PGM S MM\n");

        Assert.Equal(Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"S\"",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "CALL=1",
            "PROGRAM=END",
            "SUB=BEGIN NAME=1",
            "LINE IX=10 F=100",
            "SUB=END",
            "FILE=END"), text);
        AssertFormatsToItself(text);
    }

    // The subprogram runs with the state of its callers at the CALL, and check walks it at every call without an
    // ERROR (virtual machine 3.9).
    [Fact]
    public void Subprogram_WithIncrementalWords_ChecksWithoutError()
    {
        string text = Text("0 BEGIN PGM S MM\n1 TOOL CALL 1 Z S1000\n2 M3\n3 L X+0 Y+0 Z+5 R0 FMAX\n4 CALL LBL 1\n"
            + "5 CALL LBL 1\n6 M30\n7 LBL 1\n8 L IX+10 F100\n9 LBL 0\n10 END PGM S MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }

    // M99 in a subprogram calls the cycle every caller defined; where the callers define different cycles the reader
    // does not know which, and the block stays RAW (virtual machine 3.9).
    [Fact]
    public void M99_InASubprogram_CallsTheCycleItsCallersAgreeOn()
    {
        string agreed = Text("0 BEGIN PGM S MM\n1 " + Drill10 + "\n2 L X+0 Y+0 Z+5 R0 FMAX\n3 CALL LBL 1\n4 M30\n"
            + "5 LBL 1\n6 L IX+10 FMAX M99\n7 LBL 0\n8 END PGM S MM\n");
        NcxProgram disagreed = Program("0 BEGIN PGM S MM\n1 " + Drill10 + "\n2 CALL LBL 1\n3 " + Drill10.Replace(
            "-10", "-20", StringComparison.Ordinal) + "\n4 CALL LBL 1\n5 M30\n6 LBL 1\n7 L IX+10 FMAX M99\n8 LBL 0\n"
            + "9 END PGM S MM\n");

        Assert.Contains("SUB=BEGIN NAME=1\nCYCLE_CALL IX=10\nSUB=END\n", agreed, StringComparison.Ordinal);
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(disagreed));
    }

    // A subprogram with a CYCL DEF, one whose cycle number the reader cannot read too, may replace the definition of
    // its caller, which stays active until the next CYCL DEF: the M99 after the call stays RAW (heidenhain 5; virtual
    // machine 3.9).
    [Fact]
    public void M99_AfterASubprogramWithACyclDef_IsKeptAsRaw()
    {
        NcxProgram program = Program("0 BEGIN PGM S MM\n1 " + Drill10 + "\n2 L X+0 Y+0 Z+5 R0 FMAX\n3 CALL LBL 1\n"
            + "4 L X+20 FMAX M99\n5 M30\n6 LBL 1\n7 CYCL DEF\n8 LBL 0\n9 END PGM S MM\n");

        Assert.DoesNotContain(program.Blocks, block => block.Verb?.Key == "CYCLE_CALL");
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // An LBL used by REP is a LABEL, CALL LBL n REP k is REPEAT=n TIMES=k (heidenhain 7 rule 3).
    [Fact]
    public void Lbl_UsedByRep_IsALabelAndTheCallIsRepeat()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 COMP=OFF", "LABEL=1", "LINE IX=10 F=100", "TIMES=3 REPEAT=1"),
            Body("1 L X+0 Y+0 R0 FMAX\n2 LBL 1\n3 L IX+10 F100\n4 CALL LBL 1 REP 3"));
    }

    // FN 9 to FN 12 jump to an LBL, which is a LABEL: equal, unequal, greater, less (heidenhain 6, 7 rule 3).
    [Fact]
    public void Fn9ToFn12_AreJumpsWithIfToALabel()
    {
        Assert.Equal(Lines(
            "LABEL=2",
            "JUMP=3 IF={$Q1 == $Q3}",
            "JUMP=3 IF={$Q1 != $Q2}",
            "JUMP=2 IF={$Q1 > 10}",
            "JUMP=3 IF={$Q1 < 0}",
            "LABEL=3",
            "RAPID X=0"),
            Body("1 LBL 2\n2 FN 9: IF +Q1 EQU +Q3 GOTO LBL 3\n3 FN 10: IF +Q1 NE +Q2 GOTO LBL 3\n"
                + "4 FN 11: IF +Q1 GT +10 GOTO LBL 2\n5 FN 12: IF +Q1 LT +0 GOTO LBL 3\n6 LBL 3\n7 L X+0 FMAX"));
    }

    // An FN jump to an LBL that the end of the program follows is JUMP=END (controller-mapping 1, JUMP=END).
    [Fact]
    public void FnJump_ToTheLabelBeforeTheEnd_IsJumpEnd()
    {
        string text = Text("0 BEGIN PGM J MM\n1 FN 9: IF +Q1 EQU +1 GOTO LBL 99\n2 L X+10 R0 FMAX\n3 LBL 99\n4 M30\n"
            + "5 END PGM J MM\n");

        Assert.Equal(Lines("JUMP=END IF={$Q1 == 1}", "RAPID X=10 COMP=OFF"), BodyOf(text));
    }

    // CALL PGM name calls another file, CALL="name" (controller-mapping 6; language 4.9).
    [Fact]
    public void CallPgm_IsTheCallOfAnExternalProgram()
    {
        Assert.Equal(Lines("CALL=\"DRILL\""), Body("5 CALL PGM DRILL"));
    }

    // CALL LBL of a label that is no subprogram stays RAW.
    [Fact]
    public void CallLbl_OfNoSubprogram_IsKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram("5 CALL LBL 7")));
    }

    // A formula is VAR with the Q names kept (heidenhain 6; controller-mapping 6, VAR; language 4.9).
    [Fact]
    public void Formula_IsVarWithTheQNamesKept()
    {
        Assert.Equal(Lines("VAR:Q1={$Q2 + 3 * SIN($Q3)}", "VAR:QL3={$QR2 * 2}"),
            Body("1 Q1 = Q2 + 3 * SIN Q3\n2 QL3 = QR2 * 2"));
    }

    // FN 0 assigns, FN 1 adds, FN 2 subtracts, FN 3 multiplies, FN 4 divides, FN 5 takes the square root (heidenhain 6).
    [Fact]
    public void Fn0ToFn5_AreAssignments()
    {
        Assert.Equal(Lines("VAR:Q5=10", "VAR:Q1={$Q2 + 5}", "VAR:Q1={$Q2 - 5}", "VAR:Q1={$Q2 * 5}", "VAR:Q1={$Q2 / 5}",
            "VAR:Q1={SQRT($Q2)}"),
            Body("1 FN 0: Q5 = +10\n2 FN 1: Q1 = +Q2 + +5\n3 FN 2: Q1 = +Q2 - +5\n4 FN 3: Q1 = +Q2 * +5\n"
                + "5 FN 4: Q1 = +Q2 DIV +5\n6 FN 5: Q1 = SQRT +Q2"));
    }

    // The string parameters QS have their own formulas: RAW.
    [Fact]
    public void StringParameter_IsKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram("1 QS1 = \"TEXT\"")));
    }

    // The flow formats to itself (D91).
    [Fact]
    public void Flow_FormatsToItself()
    {
        NcxProgram program = Program("0 BEGIN PGM F MM\n1 Q1 = NEG Q2 + 1\n2 LBL 5\n3 FN 9: IF +Q1 EQU +Q2 GOTO LBL 5\n"
            + "4 CALL LBL 5 REP 2/2\n5 M30\n6 END PGM F MM\n");

        AssertFormatsToItself(NcxWriter.Write(program));
        Assert.Empty(program.Diagnostics.Items);
    }
}
