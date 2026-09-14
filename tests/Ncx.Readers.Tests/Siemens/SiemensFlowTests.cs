using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The flow (controllers siemens.md 8, 11 rule 6; controller-mapping 6; language 4.9, 4.13; D149): the structures
/// lowered to LABEL, JUMP and IF, the jumps, REPEAT, the subprograms with their parameters and their calls, the R
/// parameters and the DEF names as VAR.
/// </summary>
public sealed class SiemensFlowTests
{
    // Rule 6: IF, ELSE and ENDIF lower to a conditional JUMP past the IF part, a JUMP past the ELSE part and their
    // labels.
    [Fact]
    public void IfElseEndif_LowersToJumpsAndLabels()
    {
        Assert.Equal(Lines("VAR:R1=0", "JUMP=IF1_ELSE IF={NOT ($R1 == 6)}", "RAPID X=1", "JUMP=IF1_END",
                "LABEL=IF1_ELSE", "RAPID X=2", "LABEL=IF1_END"),
            CheckedBody("R1=0\nIF R1==6\nG0 X1\nELSE\nG0 X2\nENDIF"));
    }

    // Rule 6: WHILE tests before every pass, FOR counts its variable up to the end value (siemens 8).
    [Fact]
    public void WhileAndFor_LowerToLoopsWithLabels()
    {
        Assert.Equal(Lines("VAR:R1=0", "LABEL=WHILE1", "JUMP=WHILE1_END IF={NOT ($R1 < 3)}", "VAR:R1={$R1 + 1}",
                "JUMP=WHILE1", "LABEL=WHILE1_END"),
            CheckedBody("R1=0\nWHILE R1<3\nR1=R1+1\nENDWHILE"));
        Assert.Equal(Lines("VAR:R1=0", "VAR:R2=1", "LABEL=FOR1", "JUMP=FOR1_END IF={$R2 > (3)}",
                "VAR:R1={$R1 + $R2}", "VAR:R2={$R2 + 1}", "JUMP=FOR1", "LABEL=FOR1_END"),
            CheckedBody("R1=0\nFOR R2=1 TO 3\nR1=R1+R2\nENDFOR"));
    }

    // Rule 6: LOOP runs until a jump leaves it, REPEAT until its UNTIL holds (siemens 8).
    [Fact]
    public void LoopAndRepeatUntil_LowerToJumpsBack()
    {
        Assert.Equal(Lines("LABEL=LOOP1", "JUMP=OUT IF={$R1 > 0}", "JUMP=LOOP1", "LABEL=LOOP1_END", "LABEL=OUT",
                "RAPID X=1"),
            CheckedBody("LOOP\nIF R1>0 GOTOF OUT\nENDLOOP\nOUT:\nG0 X1"));
        Assert.Equal(Lines("LABEL=REPEAT1", "VAR:R1={$R1 - 1}", "JUMP=REPEAT1 IF={NOT ($R1 <= 0)}"),
            CheckedBody("REPEAT\nR1=R1-1\nUNTIL R1<=0"));
    }

    // Rule 6: CASE jumps to the label of its value, DEFAULT to its own otherwise (siemens 8).
    [Fact]
    public void Case_LowersToAJumpPerValue()
    {
        Assert.Equal(Lines("JUMP=A1 IF={($R1) == 1}", "JUMP=A2 IF={($R1) == 2}", "JUMP=A3", "LABEL=A1", "RAPID Y=1",
                "LABEL=A2", "RAPID Y=2", "LABEL=A3", "RAPID Y=3"),
            CheckedBody("CASE(R1) OF 1 GOTOF A1 2 GOTOF A2 DEFAULT GOTOF A3\nA1: G0 Y1\nA2: G0 Y2\nA3: G0 Y3"));
    }

    // Rule 6: GOTOF and GOTOB jump to a label, IF with a jump is a conditional JUMP, REPEAT label P= repeats from the
    // label, REPEAT start end P= the range before it (siemens 8; controller-mapping 6, JUMP and REPEAT).
    [Fact]
    public void GotofGotobAndRepeat_BecomeJumpsAndRepeat()
    {
        Assert.Equal(Lines("VAR:R1=3", "JUMP=SKIP1", "RAPID X=1", "LABEL=SKIP1", "JUMP=SKIP1 IF={$R1 == 3}",
                "TIMES=2 REPEAT=SKIP1", "LABEL=START2", "RAPID X=5", "LABEL=END2", "TIMES=3 REPEAT=START2"),
            CheckedBody("R1=3\nGOTOF SKIP1\nG0 X1\nSKIP1:\nIF R1==3 GOTOB SKIP1\nREPEAT SKIP1 P=2\nSTART2:\nG0 X5\n"
                + "END2:\nREPEAT START2 END2 P=3"));
    }

    // Rule 6: REPEAT start end P= whose range does not end before it, REPEATB label P= and REPEAT label P= with an
    // ENDLABEL: between them repeat a block range: the range becomes a SUB section of its own, which the REPEAT calls P
    // times, and its blocks stay where they stand, since the control runs them there as well; ENDLABEL: only ends the
    // range (controller-mapping 6, REPEAT + TIMES; language 4.9, 4.13).
    [Fact]
    public void RepeatOfARange_BecomesASubSectionAndItsCall()
    {
        string text = Text("%_N_TEST_MPF\nG0 X0 Y0 Z10\nANF: G0 X1\nENDE: G0 X2\nG0 X9\nREPEAT ANF ENDE P=2\n"
            + "N40 G0 Y1\nG0 Y2\nREPEATB N40 P=3\nLAB1: G0 Z5\nENDLABEL:\nG0 Z6\nREPEAT LAB1 P=2\nM30\n");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", "LABEL=ANF", "RAPID X=1", "LABEL=ENDE", "RAPID X=2", "RAPID X=9",
                "CALL=REPEAT_ANF_ENDE TIMES=2", "RAPID Y=1", "RAPID Y=2", "CALL=REPEAT_N40 TIMES=3", "LABEL=LAB1",
                "RAPID Z=5", "RAPID Z=6", "CALL=REPEAT_LAB1_ENDLABEL TIMES=2"),
            BodyOf(text));
        Assert.EndsWith(Lines("PROGRAM=END", "SUB=BEGIN NAME=REPEAT_ANF_ENDE", "LABEL=ANF", "RAPID X=1", "LABEL=ENDE",
                "RAPID X=2", "SUB=END", "SUB=BEGIN NAME=REPEAT_N40", "RAPID Y=1", "SUB=END",
                "SUB=BEGIN NAME=REPEAT_LAB1_ENDLABEL", "LABEL=LAB1", "RAPID Z=5", "SUB=END", "FILE=END"),
            text, StringComparison.Ordinal);
        AssertChecks(text);
        AssertFormatsToItself(text);
    }

    // A repeated range that holds a jump cannot become a SUB section, which is entered and left as a whole: the REPEAT
    // stays RAW with the reason (controller-mapping 6, REPEAT + TIMES; D5).
    [Fact]
    public void RepeatOfARange_WithAJump_StaysRaw()
    {
        NcxProgram program = FramedProgram("ANF: G0 X1\nGOTOF WEITER\nWEITER: G0 X2\nG0 X3\nREPEAT ANF WEITER P=2");

        Assert.Equal(Lines("LABEL=ANF", "RAPID X=1", "JUMP=WEITER", "LABEL=WEITER", "RAPID X=2", "RAPID X=3",
                Raw("REPEAT ANF WEITER P=2")),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // GOTOS jumps to the start of the program: a LABEL after the header; with IF under the condition (siemens 8).
    [Fact]
    public void Gotos_JumpsToALabelAfterTheHeader()
    {
        Assert.Equal(Lines("LABEL=START", "RAPID X=1", "JUMP=START"), CheckedBody("G0 X1\nGOTOS"));
        Assert.Equal(Lines("LABEL=START", "VAR:R1=0", "RAPID X=1", "JUMP=START IF={$R1 == 1}"),
            CheckedBody("R1=0\nG0 X1\nIF R1==1 GOTOS"));
    }

    // An IF whose condition has no NCX form, $P_SEARCH that the machine maps to no SYS_ name, cannot branch in NCX: the
    // structure is kept as RAW as a whole, its branches, its ELSE and its ENDIF included, each block with a WARNING and
    // none with an ERROR (D5; siemens 8; controller-mapping 6, JUMP + IF).
    [Fact]
    public void If_OnAnUnmappedSystemVariable_KeepsTheWholeStructureRaw()
    {
        NcxProgram program = FramedProgram("G0 X0\nIF $P_SEARCH\nG0 X1\nELSE\nG0 X2\nENDIF\nG0 X3");

        Assert.Equal(Lines("RAPID X=0", Raw("IF $P_SEARCH"), Raw("G0 X1"), Raw("ELSE"), Raw("G0 X2"), Raw("ENDIF"),
                "RAPID X=3"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([.. Enumerable.Repeat(DiagnosticCodes.KeptAsRaw, 5)], Codes(program));
    }

    // WHILE and REPEAT ... UNTIL with a condition NCX cannot express are kept as RAW as a whole too, with a structure
    // nested in them; the structures after them are lowered again (D5; siemens 8).
    [Fact]
    public void WhileAndRepeatUntil_OnAnUnmappedSystemVariable_AreKeptRawAsAWhole()
    {
        Assert.Equal(Lines(Raw("WHILE $P_SIM"), Raw("IF R1==1"), Raw("G0 X1"), Raw("ENDIF"), Raw("ENDWHILE"),
                "RAPID X=2", Raw("REPEAT"), Raw("G0 X3"), Raw("UNTIL $P_SIM"), "RAPID X=4", "LABEL=REPEAT1",
                "VAR:R1=1", "JUMP=REPEAT1 IF={NOT ($R1 > 0)}"),
            CheckedBody("WHILE $P_SIM\nIF R1==1\nG0 X1\nENDIF\nENDWHILE\nG0 X2\nREPEAT\nG0 X3\nUNTIL $P_SIM\nG0 X4\n"
                + "REPEAT\nR1=1\nUNTIL R1>0"));
    }

    // Rule 6: the parameters of PROC are ARG names, a position left empty or out takes the default; L100, NAME P3 and
    // CALL "NAME" call the subprogram of the file, P is TIMES; RET and M17 are SUB=END (controller-mapping 6, CALL,
    // TIMES, ARG, SUB=END; D149).
    [Fact]
    public void ProcAndCalls_BecomeSubsWithArgsAndCalls()
    {
        string text = Text("%_N_MAIN_MPF\nPOCK(5)\nL100\nPOCK P3\nCALL \"POCK\"\nM30\n%_N_POCK_SPF\n"
            + "PROC POCK(REAL DEPTH=2)\nG1 Z=-DEPTH F100\nRET\n%_N_L100_SPF\nG0 Z20\nM17\n");

        Assert.Equal(Lines("CALL=POCK ARG:DEPTH=5", "CALL=L100", "CALL=POCK ARG:DEPTH=2 TIMES=3",
                "CALL=POCK ARG:DEPTH=2"),
            BodyOf(text));
        Assert.Contains(Lines("SUB=BEGIN NAME=POCK", "LINE Z={-$DEPTH} F=100", "SUB=END", "SUB=BEGIN NAME=L100",
            "RAPID Z=20", "SUB=END"), text, StringComparison.Ordinal);
    }

    // EXTCALL calls a program outside the file by its name, CALL="NAME" (controller-mapping 6, CALL).
    [Fact]
    public void Extcall_IsACallOfAProgramOutsideTheFile()
    {
        Assert.Equal(Lines("CALL=\"EXT1\"", "CALL=\"EXT2\""),
            CheckedBody("EXTCALL(\"/_N_EXT_DIR/_N_EXT1_SPF\")\nEXTCALL(\"EXT2\")"));
    }

    // R parameters and DEF of a numeric type with a value are VAR, the functions of their expressions the NCX ones;
    // DEF without a value stays RAW (siemens 8; controller-mapping 6, VAR).
    [Fact]
    public void RParametersAndDef_BecomeVar()
    {
        Assert.Equal(Lines("VAR:COUNT=3", Raw("DEF REAL A, B=2"), "VAR:R5=1.5", "VAR:COUNT={$COUNT - 1}",
                "VAR:R1={INT($R2 / 3)}", "VAR:R3={SIN(30) + ((2) ^ 2)}"),
            CheckedBody("DEF INT COUNT=3\nDEF REAL A, B=2\nR[5]=1.5\nCOUNT=COUNT-1\nR1=TRUNC(R2/3)\n"
                + "R3=SIN(30)+POT(2)"));
    }

    // An end without its structure is an ERROR and stays RAW, a structure without its end a WARNING (siemens 8).
    [Fact]
    public void Structures_WithoutTheirBeginOrEnd_AreReported()
    {
        NcxProgram program = FramedProgram("G0 X0\nIF R1==1\nENDWHILE");

        Assert.Equal(Lines("RAPID X=0", "JUMP=IF1_END IF={NOT ($R1 == 1)}", Raw("ENDWHILE")),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensStructureEndWithoutBegin, DiagnosticCodes.KeptAsRaw,
            DiagnosticCodes.SiemensStructureNotClosed], Codes(program));
    }
}
