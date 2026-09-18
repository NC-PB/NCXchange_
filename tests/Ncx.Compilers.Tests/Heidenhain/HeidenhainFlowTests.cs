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

    // Language 4.9; heidenhain 8 rule 2; virtual machine 1: the STATIC walk records the REPEAT and does not follow it,
    // and CALL LBL REP reaches the label with the M5 and the F800 the control has there, so a modal word a block after
    // the label states stands again: the M3 and the F500. The compensation, which no block after the label states, is
    // modal (language 2 rule 2): R0 on both ways, and none is written.
    [Fact]
    public void Label_ModalWordsAfterIt_AreWrittenAgainWhereTheyAreUsed()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=500",
            "SPINDLE=CW",
            "RAPID X=0 Y=0 Z=0",
            "LINE X=10 F=500",
            "LABEL=1",
            "SPINDLE=CW",
            "LINE X=20 F=500",
            "LINE X=0 F=800",
            "SPINDLE=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "TOOL CALL 1 Z S500\nM3\nL X+0 Y+0 Z+0 R0 FMAX\nL X+10 F500\nLBL 1\nM3\nL X+20 F500\nL X+0 F800\nM5\n"
            + "CALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 4.4 and 4.9; controllers heidenhain.md 2: the CALL LBL REP reaches the label with the R0 of the L block
    // before it, and the arc after the label, which the program runs with the COMP=LEFT it states there, carries no
    // compensation in Klartext, CMP115 on the REPEAT.
    [Fact]
    public void Repeat_ReachingAnArcWithAnotherCompensation_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE Z=-1 F=100",
            "LINE X=5 COMP=LEFT",
            "LABEL=1",
            "COMP=LEFT",
            "ARC=CW X=15 Y=0 R=5",
            "LINE X=20 COMP=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(11, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Language 4.4 and 4.9: a jump forward reaches the label with the R0 the control has at the jump, and the arc after
    // the label runs with the COMP=LEFT the program states after the label, CMP115 on the JUMP.
    [Fact]
    public void Jump_ReachingAnArcWithAnotherCompensation_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE Z=-1 F=100",
            "JUMP=1 IF={$Q1 > 0}",
            "LINE X=5 COMP=LEFT",
            "LABEL=1",
            "COMP=LEFT",
            "ARC=CW X=15 Y=0 R=5"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Language 2 rule 2, 4.4 and 4.9: a jump that reaches an arc after its label with the compensation and the feed
    // the arc runs with needs no L block, and neither the arc nor the L block after it, which state no F and no COMP,
    // write them again: each way brings the R0 and the F100 of the program.
    [Fact]
    public void Repeat_ReachingAnArcWithItsCompensation_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LABEL=1", "ARC=CW X=10 Y=0 R=5", "LINE X=0", "REPEAT=1 TIMES=2"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z-1 F100\nLBL 1\nCR X+10 Y+0 R+5 DR-\nL X+0\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 1, 2 rule 2, 4.4 and 4.9: compensation is modal, and REPEAT runs the blocks from the label again, so
    // the L block after the label, which states no COMP, runs with the RL of the text on the first pass and with the
    // R0 of the REPEAT on the second; Klartext writes no compensation there, and each pass keeps what the control has.
    [Fact]
    public void Repeat_LineAfterTheLabelThatStatesNoComp_RunsWithTheCompensationOfEachWay()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=1000",
            "SPINDLE=CW",
            "RAPID X=0 Y=0 Z=0",
            "LINE X=10 F=500 COMP=LEFT",
            "LABEL=1",
            "LINE X=20",
            "LINE X=0 COMP=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "TOOL CALL 1 Z S1000\nM3\nL X+0 Y+0 Z+0 R0 FMAX\nL X+10 RL F500\nLBL 1\nL X+20\nL X+0 R0\n"
            + "CALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 2 rule 2, 4.4 and 4.9: the REPEAT reaches the arc after the label with the R0 the program has at the
    // REPEAT, which the control has as well, so the arc runs as the program on both passes.
    [Fact]
    public void Repeat_ReachingAnArcWithTheCompensationOfTheRepeat_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=1000",
            "SPINDLE=CW",
            "RAPID X=0 Y=0 Z=0",
            "LINE X=5 F=500 COMP=LEFT",
            "LABEL=1",
            "ARC=CW X=15 Y=0 R=5",
            "LINE X=0 COMP=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "TOOL CALL 1 Z S1000\nM3\nL X+0 Y+0 Z+0 R0 FMAX\nL X+5 RL F500\nLBL 1\nCR X+15 Y+0 R+5 DR-\nL X+0 R0\n"
            + "CALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 2 rule 2 and 4.9; heidenhain 8 rule 2: the feed is modal, so the line after the label, which states no
    // F, runs with the F500 of the text on the first pass and with the F800 of the REPEAT on the second; Klartext
    // writes no F there.
    [Fact]
    public void Repeat_LineAfterTheLabelThatStatesNoFeed_RunsWithTheFeedOfEachWay()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE X=10 F=500", "LABEL=1", "LINE X=20", "LINE X=0 F=800", "REPEAT=1 TIMES=2"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL X+10 F500\nLBL 1\nL X+20\nL X+0 F800\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 4.4 and 4.9: a COMP a block after the label states is written, although the text before the label has
    // it already: the REPEAT reaches the label with R0.
    [Fact]
    public void Label_CompStatedAfterIt_IsWrittenOnItsLine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE X=5 F=500 COMP=LEFT",
            "LABEL=1",
            "COMP=LEFT",
            "LINE X=20",
            "LINE X=0 COMP=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "L X+0 Y+0 R0 FMAX\nL X+5 RL F500\nLBL 1\nL X+20 RL\nL X+0 R0\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 4.4 and 4.9: the COMP=LEFT before the label waits for the next L block, the first after the label, which
    // writes it; the REPEAT brings the RL of the program as well.
    [Fact]
    public void Label_AfterACompNoLineWrote_WritesItWithTheNextLine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE X=5 F=500", "COMP=LEFT", "LABEL=1", "LINE X=20", "LINE X=10", "REPEAT=1 TIMES=2"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL X+5 F500\nLBL 1\nL X+20 RL\nL X+10\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 2 rule 2, 4.4 and 4.9: the L block after the label writes the RL of the COMP=LEFT before it for the way
    // the text runs, and the REPEAT reaches it with the R0 of the program, which states no COMP after the label;
    // Klartext has one text for both ways, CMP118 on the REPEAT.
    [Fact]
    public void Repeat_WithAnotherCompensationThanTheLineAfterTheLabelWrites_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE X=5 F=500",
            "COMP=LEFT",
            "LABEL=1",
            "LINE X=20",
            "LINE X=10 COMP=OFF",
            "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(10, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 4.4 and 4.9: the COMP=OFF of the REPEAT block has no L block, so the control reaches the label with RL,
    // and the L block after it, which states no COMP, writes none, CMP118 on the REPEAT.
    [Fact]
    public void Repeat_WithACompNoLineWroteAtTheJump_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE X=10 F=500 COMP=LEFT", "LABEL=1", "LINE X=20", "COMP=OFF REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 4.4 and 4.9: a jump forward with a COMP=OFF that no L block has written reaches the L block after the
    // label with RL on the control, and the L block writes nothing, since the text runs into the label with R0; the
    // label reports it on the JUMP, CMP118.
    [Fact]
    public void Jump_ForwardWithACompNoLineWrote_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE X=5 F=500 COMP=LEFT",
            "COMP=OFF JUMP=1 IF={$Q1 > 0}",
            "LINE X=6",
            "LABEL=1",
            "LINE X=20"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 2 rule 2 and 4.9; heidenhain 8 rule 2: the line after the label writes the F800 of the F before it for
    // the way the text runs, and the REPEAT reaches it with the F500 of the program, CMP118.
    [Fact]
    public void Repeat_WithAnotherFeedThanTheLineAfterTheLabelWrites_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE X=5 F=500",
            "F=800",
            "LABEL=1",
            "LINE X=20",
            "LINE X=10 F=500",
            "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(10, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 4.3 and 4.9; heidenhain 8 rule 2: the F=800 of the REPEAT block waits for a motion, so the control
    // reaches the label with F500, and the line after it, which states no F, writes none, CMP118.
    [Fact]
    public void Repeat_WithAFeedNoMotionWroteAtTheJump_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE X=10 F=500", "LABEL=1", "LINE X=20", "F=800 REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 4.9; virtual machine 3.9, D99: the subprogram the way after the label enters is written from an unknown
    // target state, so its first L block writes the RL of the text, and the REPEAT runs it with the R0 of the program,
    // CMP118.
    [Fact]
    public void Repeat_IntoASubprogramThatWritesAnotherCompensation_IsTheErrorCmp118()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0",
            "LINE X=5 F=100 COMP=LEFT",
            "LABEL=1",
            "CALL=5",
            "LINE X=0 COMP=OFF",
            "REPEAT=1 TIMES=2",
            "PROGRAM=END",
            "SUB=BEGIN NAME=5",
            "LINE IX=10",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelWaysDiffer).Line);
    }

    // Language 4.9: the REPEAT to LBL 1 runs on through LBL 2, which the text reaches in step with the program, so
    // neither L block after the labels writes the compensation, and both repeats run as the program does.
    [Fact]
    public void Repeat_ThroughAnotherLabel_WritesTheLoops()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0",
            "LINE X=5 F=500 COMP=LEFT",
            "LABEL=1",
            "LINE X=20",
            "LABEL=2",
            "LINE X=30",
            "LINE X=0 COMP=OFF",
            "REPEAT=1 TIMES=2",
            "REPEAT=2 TIMES=1"));

        Assert.Equal(
            "L X+0 Y+0 R0 FMAX\nL X+5 RL F500\nLBL 1\nL X+20\nLBL 2\nL X+30\nL X+0 R0\nCALL LBL 1 REP 2\n"
            + "CALL LBL 2 REP 1",
            HeidenhainCompile.Body(result));
    }

    // Language 4.9; heidenhain 8 rule 3; controller-mapping 1, WORKPLANE: the CALL LBL REP reaches the label with the
    // tool axis Y of the last TOOL CALL, and the WORKPLANE=XY the program states after the label writes nothing in
    // Klartext, which gives the working plane with the tool axis of TOOL CALL only, CMP110 on the REPEAT.
    [Fact]
    public void Repeat_ReachingAWorkplaneOfAnotherToolAxis_IsTheErrorCmp110()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=500",
            "LABEL=1",
            "WORKPLANE=XY",
            "RAPID X=0 Y=0 Z=5",
            "TOOL=2 RPM=500 WORKPLANE=ZX",
            "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWorkplaneWithoutToolCall).Line);
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
    // (controller-mapping 1, JUMP=END), whose number follows every other label of the file; the L block after LBL 2,
    // which states no COMP, writes none (language 2 rule 2).
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
