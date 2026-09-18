namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// Q parameters, labels, jumps and repeats in Klartext (controllers heidenhain.md 1, 6; controller-mapping 1 and 6;
/// language 4.9, 4.12).
/// </summary>
public sealed class HeidenhainFlowTests
{
    // Controllers heidenhain.md 6; controller-mapping 6, VAR: the formula of the Q parameter, Q2 + 3 * SIN Q3 style,
    // numbers with the comma, MOD as %.
    [Fact]
    public void Variable_IsTheFormulaOfTheQParameter()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "VAR:Q1=10",
            "VAR:Q1={$Q1 + 20}",
            "VAR:Q2={SIN($Q1) * 2}",
            "VAR:QL5=2.5",
            "VAR:Q4={SQRT($Q1 + 1) MOD 3}",
            "VAR:Q5={-($Q1 ^ 2)}"));

        Assert.Equal("Q1 = 10\nQ1 = Q1 + 20\nQ2 = SIN Q1 * 2\nQL5 = 2,5\nQ4 = SQRT (Q1 + 1) % 3\nQ5 = -(Q1 ^ 2)",
            HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 6: ATAN2 is no function of the formula syntax, CMP102.
    [Fact]
    public void Formula_WithAFunctionKlartextHasNot_IsTheErrorCmp102()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("VAR:Q1={ATAN2($Q2, 1)}"));

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainValueWithoutKlartext).Line);
    }

    // Language 4.9; controllers heidenhain.md 6: Klartext names its variables Q, QL and QR, CMP101 for another name.
    [Fact]
    public void Variable_WithoutAQName_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("VAR:V1=5"));

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // Controller-mapping 6, LABEL and JUMP + IF: LBL n, and FN 9 to FN 12 IF a comparison b GOTO LBL n; the TODO
    // (question) of HeidenhainFlow.Condition gives FN 10 and FN 11 the words NE and GT.
    [Fact]
    public void LabelAndConditionalJumps_AreLblAndFn9To12()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1",
            "JUMP=1 IF={$Q3 < $Q2}",
            "JUMP=1 IF={$Q3 == 4}",
            "JUMP=1 IF={$Q3 != -2.5}",
            "JUMP=1 IF={($Q3 > $Q2)}"));

        Assert.Equal(
            "LBL 1\nFN 12: IF +Q3 LT +Q2 GOTO LBL 1\nFN 9: IF +Q3 EQU +4 GOTO LBL 1\nFN 10: IF +Q3 NE -2,5 GOTO LBL 1\n"
            + "FN 11: IF +Q3 GT +Q2 GOTO LBL 1",
            HeidenhainCompile.Body(result));
    }

    // Controller-mapping 6, JUMP: a jump without IF is the jump whose condition always holds, FN 9 with two equal
    // values.
    [Fact]
    public void Jump_WithoutCondition_IsFn9WithTwoEqualValues()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=5", "RAPID X=0 Y=0", "JUMP=5 IF={$Q1 > 0}", "JUMP=5"));

        Assert.Equal("LBL 5\nL X+0 Y+0 R0 FMAX\nFN 11: IF +Q1 GT +0 GOTO LBL 5\nFN 9: IF +0 EQU +0 GOTO LBL 5",
            HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, JUMP=END: FN 9 to FN 12 to an LBL that ends with M30, the label after every other label of
    // the file.
    [Fact]
    public void JumpToTheEnd_GoesToAnLblBeforeTheM30()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=3", "JUMP=END IF={$Q1 > 0}", "RAPID X=0 Y=0"));

        Assert.Equal("LBL 3\nFN 11: IF +Q1 GT +0 GOTO LBL 4\nL X+0 Y+0 R0 FMAX\nLBL 4", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 1; controller-mapping 6, REPEAT: CALL LBL n REP k; the TODO(question) D212 of
    // HeidenhainFlow.WriteRepeat writes the count 1 for a REPEAT without TIMES.
    [Fact]
    public void Repeat_IsCallLblRep()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1", "RAPID X=0 Y=0", "REPEAT=1 TIMES=3", "REPEAT=1"));

        Assert.Equal("LBL 1\nL X+0 Y+0 R0 FMAX\nCALL LBL 1 REP 3\nCALL LBL 1 REP 1", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 6: FN 9 to FN 12 compare for equal, unequal, greater or less; <= has no FN function,
    // CMP102.
    [Fact]
    public void Jump_OnAComparisonWithoutFnFunction_IsTheErrorCmp102()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("LABEL=1", "JUMP=1 IF={$Q1 <= 2}"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainValueWithoutKlartext).Line);
    }

    // Machine-config 2: sub_end is written for SUB=END and RETURN, LBL 0 on Heidenhain; language 4.9, 4.13: RETURN
    // returns to the caller before SUB=END, here behind a conditional jump over it.
    [Fact]
    public void Return_InASubprogram_IsTheSubEndLbl0WhereItStands()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"", "UNITS=MM", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "JUMP=5 IF={$Q1 > 0}", "RETURN", "LABEL=5", "RAPID X=2", "SUB=END",
            "FILE=END"));

        Assert.Equal(
            ["BEGIN PGM T MM", "CALL LBL 100", "M30", "LBL 100", "FN 11: IF +Q1 GT +0 GOTO LBL 5", "LBL 0", "LBL 5",
                "L X+2 R0 FMAX", "LBL 0", "END PGM T MM"],
            HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result)));
    }

    // Language 4.9, virtual machine 3.6: RETURN in a program is treated as JUMP=END, the jump to the LBL before the M30
    // (controller-mapping 1, JUMP=END), whose number follows every other label of the file.
    [Fact]
    public void Return_InAProgram_IsTheJumpToTheLblBeforeTheM30()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LABEL=2", "RAPID X=5", "RETURN"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nLBL 2\nL X+5 FMAX\nFN 9: IF +0 EQU +0 GOTO LBL 3\nLBL 3",
            HeidenhainCompile.Body(result));
    }

    // The TODO(question) D221 of HeidenhainFlow.ReturnLabel: the IF of a RETURN JUMP block makes the return of the
    // subprogram conditional (language 4.9; virtual machine 3.6), and the sub_end LBL 0 takes no condition, CMP101.
    [Fact]
    public void Return_UnderIfInASubprogram_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"", "UNITS=MM", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "RAPID X=1", "RETURN JUMP=5 IF={$Q1 > 0}", "LABEL=5", "RAPID X=2", "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // Language 4.9 and 4.13; controllers heidenhain.md 1: the LBL sections of the subprograms stand in the program, and
    // Klartext labels are program-local, so a LABEL and a SUB of one number are the same LBL twice in one file; the
    // TODO(question) of HeidenhainLabels leaves them unrenumbered, CMP116 on the second.
    [Fact]
    public void Labels_LabelAndSubprogramOfOneNumberInOneFile_AreTheErrorCmp116()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0 Z=5",
            "LABEL=1",
            "CALL=1",
            "VAR:Q1={$Q1 + 1}",
            "JUMP=1 IF={$Q1 < 3}",
            "PROGRAM=END",
            "SUB=BEGIN NAME=1",
            "LABEL=2",
            "LINE IX=5 F=200",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(10, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelNotUnique).Line);
    }

    // Language 4.9: a LABEL is unique per program or subprogram, and Klartext has one LBL namespace for the program and
    // the LBL sections in its file (controllers heidenhain.md 1; language 4.13), so a LABEL of the program and one of
    // a subprogram it calls collide, CMP116.
    [Fact]
    public void Labels_OneLabelInTheProgramAndInItsSubprogram_AreTheErrorCmp116()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0 Z=5",
            "LABEL=2",
            "CALL=1",
            "CALL=1",
            "PROGRAM=END",
            "SUB=BEGIN NAME=1",
            "LABEL=2",
            "LINE IX=5 F=200",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(10, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelNotUnique).Line);
    }

    // Controllers heidenhain.md 1: LBL 0 ends a subprogram, so LABEL=0 has no LBL of its own, CMP116.
    [Fact]
    public void Labels_LabelZero_IsTheErrorCmp116()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RAPID X=0 Y=0", "LABEL=0"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelNotUnique).Line);
    }
}
