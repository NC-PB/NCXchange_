using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 6, variables and control flow (controllers fanuc.md 1, 7; language 4.9,
/// 4.12): #n to Vn, [ ] to { }, the operators and functions, IF GOTO, IF THEN, WHILE DO END, GOTO, G65, M98, M99, the
/// system variables, and what stays RAW.
/// </summary>
public sealed class FanucMacroTests
{
    // #100 is V100 (D33); an assignment is VAR (controller-mapping 6).
    [Fact]
    public void Hash100_IsV100()
    {
        Assert.Equal(Lines("VAR:V100=5", "VAR:V101={$V100 + 20}"), Body("#100=5\n#101=#100+20"));
    }

    // [ ] are the parentheses of an NCX expression, SIN[ ] SIN( ) (controller-mapping 6, functions).
    [Fact]
    public void BracketsAndFunctions_AreTheNcxExpression()
    {
        Assert.Equal(Lines("VAR:V1={($V2 + 3.5) * SIN($V3)}"), Body("#1=[#2+3.5]*SIN[#3]"));
    }

    // FIX is INT, FUP the rounding away from zero (phase 3, P3-02: FIX and FUP to INT forms).
    [Fact]
    public void FixAndFup_AreIntForms()
    {
        Assert.Equal(
            Lines("VAR:V1={INT($V2)}", "VAR:V3={(INT($V4) + SGN(FRAC($V4)))}"),
            Body("#1=FIX[#2]\n#3=FUP[#4]"));
    }

    // ATAN[a]/[b] is the arc tangent of a over b.
    [Fact]
    public void AtanOfTwoBrackets_IsAtan2()
    {
        Assert.Equal(Lines("VAR:V1={ATAN2($V2, $V3)}"), Body("#1=ATAN[#2]/[#3]"));
    }

    // IF [ ] GOTO n is JUMP=n with IF, EQ is == (controller-mapping 6; language 4.9).
    [Fact]
    public void IfGoto_IsJumpWithIf()
    {
        Assert.Equal(
            Lines("JUMP=30 IF={$V503 == 0}", "LABEL=30", "RAPID X=1"),
            Body("IF [#503 EQ 0] GOTO 30\nN30 G0 X1."));
    }

    // GOTO n is JUMP=n to the block Nn, which gets its LABEL (controller-mapping 6).
    [Fact]
    public void Goto_IsJumpToTheLabelOfItsBlock()
    {
        Assert.Equal(Lines("JUMP=20", "LABEL=20", "RAPID X=1"), Body("GOTO 20\nN20 G0 X1."));
    }

    // IF [ ] THEN #n = e is lowered to a jump over the assignment (phase 3, P3-02).
    [Fact]
    public void IfThen_IsLoweredToAJumpOverTheAssignment()
    {
        Assert.Equal(
            Lines("JUMP=IF_3 IF={NOT ($V1 > 0)}", "VAR:V2=1", "LABEL=IF_3"),
            Body("IF [#1 GT 0] THEN #2=1"));
    }

    // WHILE [ ] DOm ... ENDm is lowered to LABEL, JUMP and IF (controller-mapping 6, structured loops).
    [Fact]
    public void WhileDoEnd_IsLoweredToLabelsAndJumps()
    {
        Assert.Equal(
            Lines(
                "VAR:V1=0",
                "LABEL=WHILE_4",
                "JUMP=WHILE_4_END IF={NOT ($V1 < 3)}",
                "VAR:V1={$V1 + 1}",
                "JUMP=WHILE_4",
                "LABEL=WHILE_4_END"),
            Body("#1=0\nWHILE [#1 LT 3] DO1\n#1=#1+1\nEND1"));
    }

    // G65 P9010 A1. B2. Z-5. calls with arguments; the letters are the locals #1, #2, #26 of the callee, V1, V2, V26
    // (controller-mapping 6, CALL + ARG; language 4.9; controllers fanuc.md 7).
    [Fact]
    public void G65_IsCallWithTheLocalVariablesOfItsLetters()
    {
        string text = Text("%\nO0001\nG65 P9010 A1. B2. Z-5.\nM30\nO9010\n#3=#1+#2\nM99\n%\n");

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "CALL=9010 ARG:V1=1 ARG:V2=2 ARG:V26=-5",
                "PROGRAM=END",
                "SUB=BEGIN NAME=9010",
                "VAR:V3={$V1 + $V2}",
                "SUB=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // M98 P L calls L times; the older form M98 P40100 calls program 0100 four times (controller-mapping 6).
    [Fact]
    public void M98_IsCallWithTimes_AlsoInTheOlderForm()
    {
        string text = Text("%\nO0001\nM98 P100 L4\nM98 P40100\nM30\nO0100\nG91 G1 X10. F100\nM99\n%\n");

        Assert.Contains("\nCALL=100 TIMES=4\nCALL=100 TIMES=4\nPROGRAM=END\nSUB=BEGIN NAME=100\n", text,
            StringComparison.Ordinal);
    }

    // Nakamura M200 P{times}{nnnn} calls a program from the memory card, the same as M98 for NCX: CALL with TIMES from
    // the leading digits, and the called O program of the file is a SUB (controller-mapping 6, CALL and TIMES; 8).
    [Fact]
    public void M200_OfTheBuilderNakamura_IsACallWithTimes()
    {
        NcxProgram program = Program("%\nO0001\nM200 P20100\nM200 P8889\nM30\nO0100\nG0 X1.\nM99\n%\n", Lathe());
        string text = NcxWriter.Write(program);

        Assert.Contains(
            "\nCALL=100 TIMES=2\nCALL=\"O8889\"\nPROGRAM=END\nSUB=BEGIN NAME=100\nRAPID X=1\nSUB=END\n", text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticCodes.FanucMCodeNotNamed, Codes(program));
        AssertFormatsToItself(text);
    }

    // On a machine of another builder M200 is no call: the block stays RAW, and no WARNING says it were written as
    // MFUNC (controller-mapping 8; machine-config 5).
    [Fact]
    public void M200_OnAMachineOfAnotherBuilder_IsKeptAsRawWithoutAnMfuncWarning()
    {
        NcxProgram program = FramedProgram("M200 P20100");

        Assert.Equal(Lines("RAW:FANUC=\"M200 P20100\""), BodyOf(NcxWriter.Write(program)));
        Assert.DoesNotContain(DiagnosticCodes.FanucMCodeNotNamed, Codes(program));
    }

    // A program the file does not hold is an external program, called by its file name (language 4.9, CALL).
    [Fact]
    public void M98_OfAProgramNotInTheFile_IsAnExternalCall()
    {
        Assert.Equal(Lines("CALL=\"O8889\""), Body("M98 P8889"));
    }

    // M99 in the main program loops to a label after the header (controller-mapping 6).
    [Fact]
    public void M99_InTheMainProgram_LoopsToTheStart()
    {
        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "LABEL=START",
                "RAPID X=1",
                "JUMP=START",
                "PROGRAM=END",
                "FILE=END"),
            Text("%\nO0001\nG0 X1.\nM99\n%\n"));
    }

    // The system variables are the SYS_ names of [system_variables], with their index (language 4.12, D51).
    [Fact]
    public void SystemVariables_MappedByTheMachine_AreTheirSysNames()
    {
        Assert.Equal(Lines("VAR:V1={$SYS_POS_X}", "VAR:V2={$SYS_WEAR_Z[99]}"), Body("#1=#5041\n#2=#11099"));
    }

    // GOTO n to the block of the M30 is JUMP=END, the early end of language 4.9 (controller-mapping 1, JUMP=END).
    [Fact]
    public void GotoToTheM30Block_IsJumpEnd()
    {
        string text = Text("%\nO0001\nIF [#1 EQ 0] GOTO 30\nG0 X1.\nN30 M30\n%\n");

        Assert.Equal(Lines("JUMP=END IF={$V1 == 0}", "RAPID X=1"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // A block-skipped /M30 is not the end of the program: SKIP JUMP=END, and PROGRAM=END stands at the real end
    // (controller-mapping 6; language 4.13).
    [Fact]
    public void SlashM30_IsSkipJumpEnd()
    {
        string text = Text("%\nO0001\nG0 X1.\n/M30\nG0 X2.\nM30\n%\n");

        Assert.Equal(Lines("RAPID X=1", "SKIP JUMP=END", "RAPID X=2"), BodyOf(text));
        Assert.Contains("\nRAPID X=2\nPROGRAM=END\nFILE=END\n", text, StringComparison.Ordinal);
    }

    // The NT NURSE branch command G480 I{n} of the builder nakamura jumps to Nn unconditionally: JUMP=n, and Nn gets
    // its LABEL (controller-mapping 6, JUMP + IF; controller-mapping 9: the branch commands are not RAW).
    [Fact]
    public void G480_OfTheBuilderNakamura_IsAJumpToTheLabelOfI()
    {
        Assert.Equal(Lines("JUMP=20", "LABEL=20", "RAPID X=1"), Body("G480 I20.\nN20 G0 X1.", Lathe()));
    }

    // G471 to G476 D Q I{n} jump when D compares with Q: equal, not equal, >=, <=, >, < (controller-mapping 6).
    [Fact]
    public void G471ToG476_CompareDWithQ_AreJumpsWithIf()
    {
        Assert.Equal(
            Lines(
                "JUMP=20 IF={$V1 == 2}",
                "JUMP=20 IF={$V1 != 2}",
                "JUMP=20 IF={$V1 >= 2}",
                "JUMP=20 IF={$V1 <= 2}",
                "JUMP=20 IF={$V1 > 2}",
                "JUMP=20 IF={$V1 < 2}",
                "LABEL=20",
                "RAPID X=1"),
            Body(
                "G471 D#1 Q2. I20.\nG472 D#1 Q2. I20.\nG473 D#1 Q2. I20.\nG474 D#1 Q2. I20.\nG475 D#1 Q2. I20.\n"
                + "G476 D#1 Q2. I20.\nN20 G0 X1.",
                Lathe()));
    }

    // G481 to G486 Q R I{n} jump when the variable #q compares with the variable #r (controller-mapping 6).
    [Fact]
    public void G481ToG486_CompareTheVariablesOfQAndR_AreJumpsWithIf()
    {
        Assert.Equal(
            Lines(
                "JUMP=20 IF={$V1 == $V2}",
                "JUMP=20 IF={$V1 != $V2}",
                "JUMP=20 IF={$V1 >= $V2}",
                "JUMP=20 IF={$V1 <= $V2}",
                "JUMP=20 IF={$V1 > $V2}",
                "JUMP=20 IF={$V1 < $V2}",
                "LABEL=20",
                "RAPID X=1"),
            Body(
                "G481 Q1. R2. I20.\nG482 Q1. R2. I20.\nG483 Q1. R2. I20.\nG484 Q1. R2. I20.\nG485 Q1. R2. I20.\n"
                + "G486 Q1. R2. I20.\nN20 G0 X1.",
                Lathe()));
    }

    // The branch commands are the NT NURSE macros of the builder nakamura (controller-mapping 6, 8); on a machine of
    // another builder G480 has no NCX word and stays RAW (controller-mapping 9).
    [Fact]
    public void G480_OnAMachineOfAnotherBuilder_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G480 I20.\"", "RAPID X=1"), Body("G480 I20.\nN20 G0 X1."));
    }

    // An unmapped system variable, an indirect variable, M99 P and G66 keep their blocks as RAW (language 4.12;
    // controller-mapping 6, 9).
    [Fact]
    public void UnmappedAndUnexpressibleMacroForms_AreKeptAsRaw()
    {
        Assert.Equal(
            Lines(
                "RAW:FANUC=\"#1=#5025\"",
                "RAW:FANUC=\"#[#1]=5\"",
                "RAW:FANUC=\"M99 P30\"",
                "RAW:FANUC=\"G66 P9010 A1.\""),
            Body("#1=#5025\n#[#1]=5\nM99 P30\nG66 P9010 A1."));
    }
}
