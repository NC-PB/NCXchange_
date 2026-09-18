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
