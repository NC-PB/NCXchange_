using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// The flow of controllers siemens.md 8 and the Siemens column of controller-mapping 6, as the compiler writes it: the
/// labels NAME:, IF ... GOTOF chains, the calls, REPEAT with P, the variables and the expressions.
/// </summary>
public sealed class SiemensFlowTests
{
    // Controller-mapping 6, JUMP + IF: IF cond GOTOF label forward, GOTOB backward, the labels NAME: at the block
    // start; the structured loops are never reconstructed.
    [Fact]
    public void JumpWithIf_BackAndForward_IsIfGotobAndGotof()
    {
        Assert.Equal(Lines("R1=0", "BACK:", "R1=R1+1", "IF R1<3 GOTOB BACK", "GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=0", "LABEL=BACK", "VAR:R1={$R1 + 1}", "JUMP=BACK IF={$R1 < 3}", "JUMP=DONE",
                "LABEL=DONE"));
    }

    // Siemens 8: GOTOB jumps backward, and a jump to the label of its own block runs the block again, whose label line
    // stands before the IF line (controller-mapping 6, JUMP + IF; language 4.9).
    [Fact]
    public void JumpWithIf_ToTheLabelOfItsOwnBlock_IsGotob()
    {
        Assert.Equal(Lines("R1=0", "AGAIN:", "R1=R1+1", "IF R1<3 GOTOB AGAIN"),
            MillBody("VAR:R1=0", "LABEL=AGAIN VAR:R1={$R1 + 1} IF={$R1 < 3} JUMP=AGAIN"));
    }

    // Virtual machine 1: the STATIC walk does not follow the jump back, which arrives at the label with the modal codes
    // of its own block, G95 here, so the modal words after the label stand again (language 2 rule 2).
    [Fact]
    public void Label_ReachedByAJump_WritesTheModalWordsAgain()
    {
        Assert.Equal(Lines("R1=0", "G1 X0 F100", "BACK:", "G1 G90 G94 X1 F100", "G1 G95 X2 F0.1", "R1=R1+1",
                "IF R1<3 GOTOB BACK"),
            MillBody("VAR:R1=0", "LINE X=0 F=100", "LABEL=BACK", "LINE X=1 F=100", "FEED_MODE=PER_REV LINE X=2 F=0.1",
                "VAR:R1={$R1 + 1}", "JUMP=BACK IF={$R1 < 3}"));
    }

    // Controllers siemens.md 8: a label begins with two letters and is no statement of the language; an integer label
    // and a label named like a statement are written with LABEL_ in front.
    [Fact]
    public void Label_OfAnIntegerAndOfAStatement_IsWrittenWithAPrefix()
    {
        Assert.Equal(Lines("GOTOF LABEL_10", "LABEL_10:", "GOTOF LABEL_LOOP", "LABEL_LOOP:"),
            MillBody("JUMP=10", "LABEL=10", "JUMP=LOOP", "LABEL=LOOP"));
    }

    // Controller-mapping 1, JUMP=END: GOTOF to a label followed by M30, which the program end carries.
    [Fact]
    public void JumpToEnd_GoesToTheLabelBeforeM30()
    {
        CompileResult result = Run(Program(MillHeader, "VAR:R1=1", "JUMP=END IF={$R1 == 1}", "RAPID X=10"), Mill());

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "R1=1", "IF R1==1 GOTOF PROGRAM_END", "G0 X10",
            "PROGRAM_END:", "M30"), TextOf(result));
    }

    // Language 4.9 and virtual machine 3.6: JUMP=END in a subprogram ends the program from the call, and the target of
    // a GOTOF is a label of its own unit (siemens 8), which has no form that ends the program; an ERROR (the
    // TODO(question) of SiemensFlow).
    [Fact]
    public void JumpToEnd_InASubprogram_IsAnError()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:R1=1
            CALL=CHECK
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            JUMP=END IF={$R1 == 1}
            SUB=END
            FILE=END
            """;

        Assert.Equal(["CMP594"], CompilerCodes(Run(program, Mill())));
    }

    // Siemens 8: the comparisons bind weaker than AND and OR on the control, so they stand in parentheses; NOT binds
    // stronger than a comparison (language 4.12).
    [Fact]
    public void Condition_WithAndAndNot_PutsTheComparisonsInParentheses()
    {
        Assert.Equal(Lines("R1=1 R2=2", "IF (R1>0) AND NOT (R2==5) GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=1 VAR:R2=2", "JUMP=DONE IF={$R1 > 0 AND NOT $R2 == 5}", "LABEL=DONE"));
    }

    // Controller-mapping 6, functions: TRUNC for INT, MINVAL and MAXVAL for MIN and MAX, POT for the square, <> for !=.
    [Fact]
    public void Expression_Functions_AreTheSinumerikFunctions()
    {
        Assert.Equal(Lines("R1=2", "R2=TRUNC(R1/3)+MINVAL(R1,4)+POT(R1)", "IF R2<>1 GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=2", "VAR:R2={INT($R1 / 3) + MIN($R1, 4) + $R1 ^ 2}", "JUMP=DONE IF={$R2 != 1}",
                "LABEL=DONE"));
    }

    // Siemens 8: the SINUMERIK language has no power other than the square and no sign function, an ERROR.
    [Fact]
    public void Expression_PowerOfThree_IsAnError()
    {
        Assert.Equal(["CMP590"], CompilerCodes(Run(Program(MillHeader, "VAR:R1=2", "VAR:R2={$R1 ^ 3}"), Mill())));
    }

    // D173: a variable of another controller, Q1, has no SINUMERIK name without [variables] map, an ERROR; with the map
    // Q = "R" it is R1.
    [Fact]
    public void Variable_OfAnotherController_IsMappedOrAnError()
    {
        Assert.Equal(["CMP591"], CompilerCodes(Run(Program(MillHeader, "VAR:Q1=5"), Mill())));
        CompileResult mapped = Run(Program(MillHeader, "VAR:Q1=5", "RAPID X={$Q1}"), Replaced(MillFile,
            "call_depth = 8", "call_depth = 8\nmap = { Q = \"R\" }"));
        Assert.Equal(Lines("R1=5", "G0 X=R1"), Body(mapped));
    }

    // Controller-mapping 6, CALL and TIMES: NAME P3 repeats the call three times.
    [Fact]
    public void Call_WithTimes_IsNameP()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            RAPID X=0
            CALL=DRILL_ROW TIMES=3
            PROGRAM=END
            SUB=BEGIN NAME=DRILL_ROW
            RAPID IX=10
            SUB=END
            FILE=END
            """;

        Assert.Contains("\nDRILL_ROW P3\n", TextOf(Run(program, Mill())), StringComparison.Ordinal);
    }

    // Controller-mapping 6, structured loops: the compiler writes IF ... GOTOF chains, a CALL under IF a jump over it.
    [Fact]
    public void CallWithIf_IsAJumpOverTheCall()
    {
        Assert.Equal(Lines("R1=1", "IF NOT (R1==1) GOTOF SKIP_CALL_5", "EXTCALL(\"O9010\")", "SKIP_CALL_5:"),
            MillBody("VAR:R1=1", "CALL=\"O9010\" IF={$R1 == 1}"));
    }

    // Controller-mapping 6: a call under IF is a jump over it, so the label after the call is reached with the modal
    // call the subprogram leaves armed and without it; the call at a position after it arms it again (siemens 7; D99).
    [Fact]
    public void CallWithIf_OfASubprogramThatLeavesAModalCall_ArmsItAgainAfterTheCall()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:R1=1
            CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10
            CALL=HOLE IF={$R1 == 1}
            CYCLE_CALL X=20
            CYCLE=OFF
            PROGRAM=END
            SUB=BEGIN NAME=HOLE
            CYCLE_CALL X=10
            SUB=END
            FILE=END
            """;

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "R1=1", "IF NOT (R1==1) GOTOF SKIP_CALL_6", "HOLE",
                "SKIP_CALL_6:", "MCALL CYCLE81(2,0,2,-10)", "G0 X20", "MCALL", "M30", "%_N_HOLE_SPF", "PROC HOLE",
                "MCALL CYCLE81(2,0,2,-10)", "G0 G90 X10", "RET"),
            TextOf(Run(program, Mill())));
    }

    // Controller-mapping 6, CALL: a program outside the file with EXTCALL (CALL="name").
    [Fact]
    public void CallOfAProgramOutsideTheFile_IsExtcall()
    {
        Assert.Equal(Lines("EXTCALL(\"O9010\")"), MillBody("CALL=\"O9010\""));
    }

    // Controller-mapping 6, REPEAT + TIMES: REPEAT LABEL P=n, the blocks from the label to the REPEAT n more times.
    [Fact]
    public void Repeat_WithTimes_IsRepeatWithP()
    {
        Assert.Equal(Lines("G0 X0", "ROW:", "G0 X=IC(10)", "REPEAT ROW P=2"),
            MillBody("RAPID X=0", "LABEL=ROW", "RAPID IX=10", "REPEAT=ROW TIMES=2"));
    }

    // Language 4.9, RETURN: in a subprogram the return, sub_end (machine-config 2).
    [Fact]
    public void Return_InASubprogram_IsRet()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=SIDE
            PROGRAM=END
            SUB=BEGIN NAME=SIDE
            VAR:R1=1
            RETURN
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC SIDE", "R1=1", "RET", "RET"), TextOf(Run(program, Mill())),
            StringComparison.Ordinal);
    }
}
