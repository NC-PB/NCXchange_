using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The tool and spindle words of the Fanuc compiler (controllers fanuc.md 4, 5, 10 rule 2; controller-mapping 3, 4):
/// T M6 and T per [tool_change], G43 H and G49, CSS, RPM_MAX, ORIENT, the named functions before the S.
/// </summary>
public sealed class FanucToolWordsTests
{
    // Controller-mapping 3: TOOL=4 is T4 M6, PRELOAD=5 T5, TOOL=0 T0 M6 per [tool_change] (machine-config 3).
    [Fact]
    public void Tool_ChangePreloadAndUnload_FollowToolChange()
    {
        string body = FanucCompile.Body("TOOL=4\nPRELOAD=5\nTOOL=5\nTOOL=0");

        Assert.Equal("T4 M6\nT5\nT5 M6\nT0 M6\n", body);
    }

    // Controller-mapping 3, fanuc 4: OFFSET:LEN in a block that moves the tool axis is G43 with H in that block, G43
    // Z2. H1 of the sources; OFFSET:LEN=0 is G49.
    [Fact]
    public void LengthOffset_InTheZBlock_IsG43WithH()
    {
        string body = FanucCompile.Body("TOOL=1\nRAPID X=0 Y=0\nRAPID Z=2 OFFSET:LEN=1\nRAPID Z=50 OFFSET:LEN=0");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG43 Z2. H1\nG49 Z50.\n", body);
    }

    // Length_offset_with_tool_axis (the TODO(question) of OutputFormat): an OFFSET:LEN of the TOOL block, as the
    // Heidenhain reading writes it, stands in the first block that moves the tool axis.
    [Fact]
    public void LengthOffset_OfTheToolBlock_WaitsForTheFirstMoveOfTheToolAxis()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nRAPID X=0 Y=0\nRAPID Z=2");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG43 Z2. H1\n", body);
    }

    // Language 4.4 (OFFSET:LEN is modal), controllers fanuc.md 6: a canned cycle drills along the tool axis, so a
    // waiting length offset stands in the block of its first call, not in the next move after G80.
    [Fact]
    public void LengthOffset_WaitingBeforeACannedCycle_StandsInTheCycleBlock()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nSPINDLE=CW RPM=1000\nRAPID X=10 Y=10"
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=100\nCYCLE_CALL\nCYCLE_CALL X=30"
            + "\nCYCLE=OFF\nRAPID Z=50");

        Assert.Equal(
            "T1 M6\nS1000 M3\nG0 G17 X10. Y10.\nG43 G81 G99 Z-20. R5. H1 F100.\nX30.\nG80\nG0 Z50.\n", body);
    }

    // Language 2 rule 8, language 4.4: an offset that still waits at the end of the program is written before the
    // end, never dropped.
    [Fact]
    public void LengthOffset_StillWaitingAtTheEnd_IsWrittenBeforeTheEnd()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nRAPID X=0 Y=0");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG43 H1\n", body);
    }

    // Language 4.4, virtual machine 3.9 (a subprogram is written from an unknown target state, D99): the moves of the
    // subprogram need the offset, so a waiting offset stands before the call.
    [Fact]
    public void LengthOffset_WaitingBeforeACall_IsWrittenBeforeTheCall()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "TOOL=1 OFFSET:LEN=1",
            "RAPID X=0 Y=0",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "RAPID Z=2",
            "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.Contains("\nT1 M6\nG0 G17 X0. Y0.\nG43 H1\nM98 P0100\nM30\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 1 (N as the target of GOTO), language 4.4: a path that reaches the label by a jump has not
    // run the lines before it, so a waiting offset stands before the label; after the label the modal codes stand
    // again, since the jump arrives with those of its own block (FanucFlow.WriteLabel).
    [Fact]
    public void LengthOffset_WaitingAtALabel_IsWrittenBeforeTheLabel()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nRAPID X=0 Y=0\nVAR:V1=0\nLABEL=5\nRAPID Z=2"
            + "\nVAR:V1={$V1 + 1}\nJUMP=5 IF={$V1 < 3}");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\n#1 = 0\nG43 H1\nN5\nG80\nG0 G17 G90 Z2.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 5\n", body);
    }

    // Controller-mapping 1, SKIP: the lines of a skipped block run only while the switch is off, so a waiting offset
    // stands unskipped before them.
    [Fact]
    public void LengthOffset_WaitingBeforeASkippedMove_IsWrittenUnskippedBeforeIt()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nRAPID X=0 Y=0\nSKIP RAPID Z=2");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG43 H1\n/Z2.\n", body);
    }

    // Controllers fanuc.md 4: G28 and G53 end at machine positions that the offset does not move, so the offset waits
    // for the first move of the tool axis in the workpiece frame (the TODO(question) of OutputFormat).
    [Fact]
    public void LengthOffset_WaitingOverHomeAndG53_StandsWithTheNextMoveOfTheWorkpiece()
    {
        string body = FanucCompile.Body(
            "TOOL=1 OFFSET:LEN=1\nHOME Z\nRAPID Z=0 FRAME=MACHINE\nRAPID X=0 Y=0\nRAPID Z=2");

        Assert.Equal("T1 M6\nG91 G28 Z0\nG0 G17 G90 G53 Z0.\nX0. Y0.\nG43 Z2. H1\n", body);
    }

    // Language 4.2 SETPOS, 4.4 (OFFSET:LEN is modal): the position that SETPOS declares on the tool axis counts with
    // the active length offset, so a waiting offset stands before G92 Z; a SETPOS of the other axes leaves it waiting.
    [Fact]
    public void LengthOffset_WaitingBeforeASetposOfTheToolAxis_IsWrittenBeforeIt()
    {
        string body = FanucCompile.Body("TOOL=1 OFFSET:LEN=1\nRAPID X=0 Y=0\nSETPOS X=5\nSETPOS Z=100\nRAPID Z=50");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG92 X5.\nG43 H1\nG92 Z100.\nZ50.\n", body);
    }

    // Language 5 rule 3 (state words take effect first), 4.2 SETPOS, 4.4: an OFFSET:LEN in the block of a SETPOS of the
    // tool axis is active when the position is declared, so it stands before G92 Z.
    [Fact]
    public void LengthOffset_InTheBlockOfASetposOfTheToolAxis_IsWrittenBeforeIt()
    {
        string body = FanucCompile.Body("TOOL=1\nRAPID X=0 Y=0\nOFFSET:LEN=2 SETPOS Z=80\nRAPID Z=40");

        Assert.Equal("T1 M6\nG0 G17 X0. Y0.\nG43 H2\nG92 Z80.\nZ40.\n", body);
    }

    // Language 2 rule 8, controller-mapping 3: a combined OFFSET has a Fanuc form in the change command of a turret,
    // {offset} of [tool_change]; the change of a mill, T{tool} M6, does not write it, so it is CMP308, never dropped.
    [Fact]
    public void Offset_BareInTheToolBlockOfAMill_IsTheErrorCmp308()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("TOOL=1 OFFSET=5", "RAPID X=0 Y=0 Z=50"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucWordNotWritten, error.Code);
    }

    // Without length_offset_with_tool_axis the offset stands where its word stands (D7).
    [Fact]
    public void LengthOffset_WithoutTheOption_StandsInTheBlockOfItsWord()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("TOOL=1 OFFSET:LEN=1", "RAPID X=0 Y=0", "RAPID Z=2"),
            FanucCompile.Mill(FanucCompile.SourceHeader)));

        Assert.Contains("G17\nT1 M6\nG43 H1\nX0. Y0.\nZ2.\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 4: G96 S with the cutting speed, G97 back to the speed, the limit of RPM_MAX from the
    // table of the spindle (machine-config 5).
    [Fact]
    public void Css_OnAndOff_AreG96AndG97()
    {
        string body = FanucCompile.Body("RPM_MAX=3000\nCSS=ON VC=200\nSPINDLE=CW\nCSS=OFF RPM=1000");

        Assert.Equal("G92 S3000\nG96 S200\nM3\nG97\nS1000\n", body);
    }

    // Fanuc 6, BOHREN.fanuc.nc N3100: the rigid tapping function stands before the S of the running spindle, M29 S500.
    [Fact]
    public void Function_WithTheSpeed_StandsBeforeTheS()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=500\nRPM=500 FUNC:RIGID_TAP=ON");

        Assert.Equal("S500 M3\nM29 S500\n", body);
    }

    // Controller-mapping 4, ORIENT: the orientation of the spindle's table, M19.
    [Fact]
    public void Orient_IsTheOrientCodeOfTheTable()
    {
        string body = FanucCompile.Body("ORIENT=0");

        Assert.Equal("M19\n", body);
    }

    // Controller-mapping 4 and 1: coolant from [coolant], STOP as M0 and M1, MFUNC as the M code with a WARNING
    // (language 4.6).
    [Fact]
    public void Functions_CoolantStopAndMFunc_AreTheirMCodes()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("COOLANT=ON", "STOP=OPTIONAL", "MFUNC=136", "COOLANT=OFF STOP=PROGRAM"),
            FanucCompile.Mill());

        Assert.Contains("\nM8\nM1\nM136\nM9 M0\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucMFuncWritten);
    }
}
