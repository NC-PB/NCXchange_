using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The flow of custom macro B in the Fanuc compiler (controllers fanuc.md 7; controller-mapping 6; language 4.9, 4.12):
/// # variables, GOTO and IF [ ] GOTO, the program end, the M99 loop, M98 and G65.
/// </summary>
public sealed class FanucFlowTests
{
    // PATTERN_LOOP.ncx in custom macro B: Q1 is #101 through [variables] map (machine-config 7, D173), the label the
    // block number N1, the conditional jump IF [#103 LT #102] GOTO 1.
    [Fact]
    public void Loop_WithQVariables_IsIfGotoOverHashVariables()
    {
        string body = FanucCompile.Body(
            "VAR:Q1=10\nVAR:Q3=0\nLABEL=1\nVAR:Q1={$Q1 + 20}\nVAR:Q3={$Q3 + 1}\nJUMP=1 IF={$Q3 < 5}");

        Assert.Equal("#101 = 10\n#103 = 0\nN1\n#101 = #101 + 20\n#103 = #103 + 1\nIF [#103 LT 5] GOTO 1\n", body);
    }

    // Language 4.12 NOT; fanuc 7: custom macro B has no NOT, so the comparison is inverted, as a reader lowers WHILE.
    [Fact]
    public void Condition_WithNot_IsTheInvertedComparison()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLABEL=1\nJUMP=1 IF={NOT ($V1 < 3 AND $V1 >= 0)}");

        Assert.Equal("#1 = 0\nN1\nIF [[#1 GE 3] OR [#1 LT 0]] GOTO 1\n", body);
    }

    // D33, language 4.9: Vn is #n; the functions take their argument in brackets, INT as FIX (fanuc 7).
    [Fact]
    public void Variables_VNamesAndFunctions_AreHashVariablesWithBrackets()
    {
        string body = FanucCompile.Body("VAR:V2=4\nVAR:V1={$V2 * SIN(30) + INT(2.5)}");

        Assert.Equal("#2 = 4\n#1 = [#2 * SIN[30]] + FIX[2.5]\n", body);
    }

    // D51, machine-config 7: a SYS_ name is the native variable of [system_variables], with its index.
    [Fact]
    public void SystemVariables_AreTheNativeVariablesOfTheMachine()
    {
        string body = FanucCompile.Body("VAR:V100={$SYS_POS_X}\nVAR:V101={$SYS_WEAR_Z[99]}");

        Assert.Equal("#100 = #5041\n#101 = #11099\n", body);
    }

    // Controller-mapping 6, CALL and ARG; fanuc 7: G65 with the letters of the standard table.
    [Fact]
    public void Call_WithArguments_IsG65WithLetters()
    {
        string body = FanucCompile.Body("CALL=\"O9010\" ARG:V1=1 ARG:V2=2.5 ARG:V26=-5");

        Assert.Equal("G65 P9010 A1. B2.5 Z-5.\n", body);
    }

    // Language 4.9 JUMP=END, controller-mapping 1: a conditional end jumps to the block number of the program end,
    // which the end carries.
    [Fact]
    public void JumpToEnd_Conditional_JumpsToTheNumberOfTheEnd()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1=1", "LABEL=5", "JUMP=END IF={$V1 > 0}", "JUMP=5"), FanucCompile.Mill()));

        Assert.EndsWith("#1 = 1\nN5\nIF [#1 GT 0] GOTO 6\nGOTO 5\nN6 M30\n%\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 1 (N numbers matter as jump targets), machine-config 2 (block_numbers): the number of a
    // label is given to no other line, so GOTO 20 names one block (the TODO(question) of FanucCompiler).
    [Fact]
    public void Label_UnderBlockNumbers_KeepsItsNumberForItself()
    {
        string machine = FanucCompile.Mill().Replace("block_numbers = { enabled = false }",
            "block_numbers = { enabled = true, start = 10, step = 10 }", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program(
            "VAR:V1=0", "LABEL=20", "RAPID X=1 Y=1", "VAR:V1={$V1 + 1}", "JUMP=20 IF={$V1 < 3}"), machine));

        Assert.Equal(
            "%\nO0001 (T)\nN10 G0 G40\nN30 G80 G90 G94 G98\nN40 #1 = 0\nN20\nN50 G80\nN60 G0 G17 G90 X1. Y1.\n"
            + "N70 #1 = #1 + 1\nN80 IF [#1 LT 3] GOTO 20\nN90 M30\n%\n", text);
    }

    // Virtual machine 1 (the STATIC walk does not follow JUMP), controllers fanuc.md 10 rule 1: a jump reaches the
    // label with the modal codes of the block it jumps from, here the G0 of the RAPID, so the LINE after the label
    // writes G1, the plane, G90, G94 and F again, with G80 before it (the TODO(question) of D247).
    [Fact]
    public void Label_ThatAJumpReaches_WritesTheModalCodesOfTheNextMotionAgain()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE Z=-1 F=100\nLABEL=10\nLINE X=1\nRAPID Z=5"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 Z-1. F100.\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nG0 Z5.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1 (the STATIC walk does not follow JUMP), language 4.3 (F is modal): the control reaches the
    // label with F100 from the block before it and with F200 from the jump, and keeps either, so the line after the
    // label writes no F of the walk.
    [Fact]
    public void Label_ThatAJumpReachesWithAnotherFeed_LeavesTheFeedToTheControl()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE Z=-1 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=200"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 Z-1. F100.\nN10\nG80\nG1 G17 G90 G94 X1.\nX2. F200.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, language 4.2 (WORKPLANE is modal): the jump reaches the label in the plane ZX and the block
    // before it in XY, so the line after the label writes no plane of the walk, while the feed, the same on both
    // paths, stands again.
    [Fact]
    public void Label_ThatAJumpReachesInAnotherPlane_LeavesThePlaneToTheControl()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 Y=0 F=100\nLABEL=10\nLINE X=1\nWORKPLANE=ZX\nLINE Z=1"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. Y0. F100.\nN10\nG80\nG1 G90 G94 X1. F100.\nG18 Z1.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, D218 (the F of a cycle block is the control's feed): the control has the feed of the cycle
    // where NCX has F100, so F100 stands before the label, and the control keeps the feed of each path after it.
    [Fact]
    public void Label_WhereTheControlHasTheFeedOfACycle_WritesTheFeedOfNcxBeforeIt()
    {
        string body = FanucCompile.Body("LINE X=0 Z=5 F=100\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=565\nCYCLE_CALL"
            + "\nCYCLE=OFF\nVAR:V1=0\nLABEL=10\nLINE X=1\nLINE X=2 F=200\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("G1 G17 X0. Z5. F100.\nG81 G99 Z-10. R2. F565.\nG80\n#1 = 0\nF100.\nN10\nG80\n"
            + "G1 G17 G90 G94 X1.\nX2. F200.\n#1 = #1 + 1\nIF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, D218: at the jump the control has the feed of the cycle where NCX has F200, so F200 stands
    // before the GOTO, and the line after the label, which takes the feed of the path, runs at it.
    [Fact]
    public void Jump_WhereTheControlHasTheFeedOfACycle_WritesTheFeedOfNcxBeforeTheGoto()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 Z=5 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=200"
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=565\nCYCLE_CALL\nCYCLE=OFF\nVAR:V1={$V1 + 1}"
            + "\nJUMP=10 IF={$V1 < 3}");

        Assert.EndsWith("G81 G99 Z-10. R2. F565.\nG80\n#1 = #1 + 1\nF200.\nIF [#1 LT 3] GOTO 10\n", body,
            StringComparison.Ordinal);
    }

    // Virtual machine 1, controllers fanuc.md 5 and 10 rule 2: the main spindle starts after the label with the speed
    // of the path, 2000 from the jump, which the control does not hold there, and an S alone after the M code of the
    // driven tool would belong to the driven tool: CMP309, never the 1000 of the walk.
    [Fact]
    public void Jump_WithASpeedTheControlCannotTake_IsTheErrorCmp309()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=1000",
            "VAR:V1=0",
            "LABEL=10",
            "SPINDLE:MAIN=CW",
            "RAPID X=50 Z=2",
            "SPINDLE:MAIN=OFF",
            "RPM:MAIN=2000",
            "SPINDLE:TOOL=CW RPM:TOOL=500",
            "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}",
            "PROGRAM=END",
            "FILE=END");

        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
    }

    // Virtual machine 1, language 4.5 (RPM is modal): the jump reaches the label with RPM 2000 of the main spindle,
    // which the control cannot take after the M code of the driven tool, but the start after the label states its RPM
    // on every path, so no block takes the speed of a path and the program compiles.
    [Fact]
    public void Jump_WithASpeedStatedAgainAfterTheLabel_Compiles()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "VAR:V1=0",
            "LABEL=10",
            "SPINDLE:MAIN=CW RPM:MAIN=1000",
            "RAPID X=50 Z=2",
            "SPINDLE:MAIN=OFF",
            "RPM:MAIN=2000",
            "SPINDLE:TOOL=CW RPM:TOOL=500",
            "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Contains("\nN10\nS1000 M3\n", text, StringComparison.Ordinal);
        Assert.Contains("\nS500 M88\n#1 = #1 + 1\nIF [#1 LT 3] GOTO 10\n", text, StringComparison.Ordinal);
    }

    // Virtual machine 1, language 4.4 (OFFSET:RAD is modal), controllers fanuc.md 4 (D stands with G41 and G42): the
    // jump reaches the label with the radius register 1 and the block before it with none, and the tool call after the
    // label states the register on every path, so no block takes the register of a path: the program compiles, as the
    // loop of a Heidenhain program with TOOL CALL in it does.
    [Fact]
    public void Jump_WithARadiusRegisterStatedAgainAfterTheLabel_Compiles()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLABEL=10\nTOOL=1 OFFSET:LEN=1 OFFSET:RAD=1\nRAPID X=0 Y=0 Z=5"
            + "\nLINE Z=-1 F=100\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nN10\nG80\nT1 M6\nG0 G17 G90 G43 X0. Y0. Z5. H1\nG1 G94 Z-1. F100.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, controllers fanuc.md 4: a forward jump reaches the label with the radius register 1, the block
    // before it with 2, and the control holds neither, which no line of its own can hold; no block after the label
    // takes the register, so the program compiles.
    [Fact]
    public void Jump_ForwardWithARadiusRegisterNoBlockTakes_Compiles()
    {
        string body = FanucCompile.Body("VAR:V1=0\nTOOL=1 OFFSET:LEN=1 OFFSET:RAD=1\nRAPID X=0 Y=0 Z=5"
            + "\nJUMP=10 IF={$V1 < 3}\nOFFSET:RAD=2\nLABEL=10\nLINE X=1 F=100");

        Assert.EndsWith("IF [#1 LT 3] GOTO 10\nN10\nG80\nG1 G17 G90 G94 X1. F100.\n", body, StringComparison.Ordinal);
    }

    // Virtual machine 1, language 4.4, D53: the same forward jump, and the block after the label starts the
    // compensation without stating the register, whose value differs by path while the control holds neither: CMP309
    // at that block, never the register of the walk.
    [Fact]
    public void Jump_ForwardWithARadiusRegisterABlockTakes_IsTheErrorCmp309AtThatBlock()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program("VAR:V1=0",
            "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1", "RAPID X=0 Y=0 Z=5", "JUMP=10 IF={$V1 < 3}", "OFFSET:RAD=2",
            "LABEL=10", "LINE X=1 F=100 COMP=LEFT"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
        Assert.Equal(10, error.Line);
    }

    // Language 4.11 (RPM is ignored while CSS is on), controllers fanuc.md 4 (G96 S is the cutting speed): the label
    // and the jump are reached under CSS with RPM 1000 and 2000, and an S alone would be a cutting speed, so none
    // stands; no block after the label takes the RPM.
    [Fact]
    public void Label_ReachedUnderCss_WritesNoSpeedAlone()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000\nCSS=ON VC=140\nVAR:V1=0\nLABEL=10\nLINE X=1 F=100"
            + "\nRPM=2000\nLINE X=2\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("S1000 M3\nG96 S140\n#1 = 0\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nX2.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Language 4.11, controllers fanuc.md 4: the jump is reached under CSS with RPM 2000, where S2000 alone would be
    // the cutting speed 2000; the block after the label states CSS=ON before any block writes S.
    [Fact]
    public void Jump_UnderCss_WritesNoSpeedAlone()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000\nVAR:V1=0\nLABEL=10\nLINE X=1 F=100\nCSS=ON VC=140"
            + "\nLINE X=2\nRPM=2000\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("S1000 M3\n#1 = 0\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nG96 S140\nX2.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Language 4.11, controllers fanuc.md 4, virtual machine 1: the control reaches the label under G97 from the block
    // before it and under G96 from the jump, so the S2000 of RPM=2000 after the label would be the cutting speed 2000
    // on the path of the jump: CMP309 at that block, and no S alone before the GOTO.
    [Fact]
    public void Label_ReachedUnderCssOnOnePath_RpmAfterItIsTheErrorCmp309()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program("SPINDLE=CW RPM=1000",
            "VAR:V1=0", "LABEL=10", "LINE X=1 F=100", "RPM=2000", "CSS=ON VC=140", "LINE X=2", "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
        Assert.Equal(8, error.Line);
    }

    // Controller-mapping 1, SKIP; D53; virtual machine 1: the skipped block before the label leaves F50 on one path and
    // F100 on the other, which the control holds, and the jump arrives with F100: the line after the label writes no
    // F, never the F100 of the walk on the path that skips the block.
    [Fact]
    public void Label_AfterASkippedBlockThatLeftTheFeedToTheControl_KeepsItThere()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 F=50\nSKIP F=100\nLABEL=10\nLINE X=1\nVAR:V1={$V1 + 1}"
            + "\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. F50.\n/F100.\nN10\nG80\nG1 G17 G90 G94 X1.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Controller-mapping 1, SKIP; D53; virtual machine 1: the skipped block before the jump leaves F50 on one path and
    // F100 on the other, while the block before the label has F100 as well: the line after the label writes no F.
    [Fact]
    public void Jump_AfterASkippedBlockThatLeftTheFeedToTheControl_KeepsItThere()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=50\nSKIP F=100"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. F100.\nN10\nG80\nG1 G17 G90 G94 X1.\nX2. F50.\n/F100.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Controller-mapping 6, D210: the jump back to the label after the header is the M99 of the main program, with the
    // block skip where the block has SKIP.
    [Fact]
    public void Loop_ToTheLabelAfterTheHeader_IsM99()
    {
        string body = FanucCompile.Body("LABEL=START\nRAPID X=1 Y=1\nSKIP JUMP=START");

        Assert.Equal("G17 X1. Y1.\n/M99\n", body);
    }

    // Controller-mapping 6, REPEAT + TIMES: the counter variable of the compiler is named nowhere (the TODO(question)
    // of FanucFlow).
    [Fact]
    public void Repeat_IsTheErrorCmp304()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "RAPID X=1 Y=1", "REPEAT=1 TIMES=3"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucRepeatNotWritten, error.Code);
    }

    // Fanuc 7: FRAC has no form in custom macro B.
    [Fact]
    public void Expression_WithFrac_IsTheErrorCmp401()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1={FRAC(2.5)}"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucExpressionNotWritable, error.Code);
    }

    // Language 4.9, machine-config 7: a name that neither the V names nor [variables] map cover has no Fanuc variable.
    [Fact]
    public void Variable_NotMapped_IsTheErrorCmp400()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("VAR:DEPTH=5"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucVariableNotMapped, error.Code);
    }
}
