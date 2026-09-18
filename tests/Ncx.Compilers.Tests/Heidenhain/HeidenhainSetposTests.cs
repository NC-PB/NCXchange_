namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// SETPOS folded into SHIFT, the cycle 7 datum shift that makes the position read as the declared value
/// (machine-config 3; D55; virtual machine 3.4, D101; controllers heidenhain.md 3; language 4.2, D31, D253).
/// </summary>
public sealed class HeidenhainSetposTests
{
    // Machine-config 3: "Heidenhain: none, the compiler folds it into SHIFT"; virtual machine 3.4: the setpos shift is
    // the position minus the declared value, here X 10 - 0 and Y 20 - 0.
    [Fact]
    public void Setpos_OfAPositionInTheWorkpieceFrame_IsFoldedIntoCycle7()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0 Y=0"));

        Assert.Equal(
            "L X+10 Y+20 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nCYCL DEF 7.2 Y+20",
            HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 3: a new cycle 7 replaces the previous one, so the cycle 7 of a second SETPOS carries
    // the setpos shift of the first one as well.
    [Fact]
    public void Setpos_OfASecondAxis_WritesCycle7WithEverySetposShiftThatStands()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "RAPID Y=30", "SETPOS Y=0"));

        Assert.Equal(
            "L X+10 Y+20 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nL Y+30 FMAX\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nCYCL DEF 7.2 Y+30",
            HeidenhainCompile.Body(result));
    }

    // Virtual machine 3.4: the next ORIGIN clears the setpos shift; the TODO(question) D253 of HeidenhainChain: its
    // cycle 7 is cancelled before cycle 247, as the entries of the chain are.
    [Fact]
    public void Setpos_ThenOrigin_CancelsTheCycle7BeforeCycle247()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "ORIGIN=1"));

        Assert.Equal(
            "L X+10 Y+20 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\nCYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2, D31: a ROTATE after the SETPOS is applied to the frame the SETPOS shifted, cycle 10 after cycle 7,
    // and its RESET removes the rotation alone; the setpos shift stands until ORIGIN (virtual machine 3.4).
    [Fact]
    public void Setpos_ThenARotationAndItsReset_KeepsTheCycle7()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "ROTATE=30", "ROTATE=RESET"));

        Assert.Equal(
            "L X+10 Y+20 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\n"
            + "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+30\nCYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2, D31: a SETPOS under a ROTATE shifts the rotated frame, cycle 7 after cycle 10; the rotation turns X
    // and Y only, so a setpos shift of Z acts the same once the rotation is removed.
    [Fact]
    public void Setpos_OfTheToolAxisUnderARotation_IsCycle7AfterCycle10AndStaysThroughItsReset()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "RAPID X=10 Y=20 Z=5", "SETPOS Z=0", "ROTATE=RESET"));

        Assert.Equal(
            "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+30\nL X+10 Y+20 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\n"
            + "CYCL DEF 7.1 Z+5\nCYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0",
            HeidenhainCompile.Body(result));
    }

    // The TODO(question) of HeidenhainSetpos.CheckRemoved: where the setpos shift of X acts once the rotation it was
    // declared under is removed is not given, so the RESET is CMP114.
    [Fact]
    public void Setpos_UnderARotationThenItsReset_IsTheErrorCmp114()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "ROTATE=RESET"));

        Assert.Empty(result.Files);
        Assert.Equal(7, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainSetposFrameRemoved).Line);
    }

    // The TODO(question) D253 of HeidenhainSetpos: the cycle 7 of the SETPOS would replace the cycle 7 of the SHIFT.
    [Fact]
    public void Setpos_WhileAShiftStandsInTheChain_IsTheErrorCmp106()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "SHIFT X=5", "RAPID X=10 Y=20 Z=5", "SETPOS X=0"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainTransformReplacesAnother).Line);
    }

    // The TODO(question) D253 of HeidenhainChain.Append: the cycle 7 of the SHIFT would replace the cycle 7 of the
    // SETPOS.
    [Fact]
    public void Shift_WhileASetposShiftStands_IsTheErrorCmp106()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "SHIFT Y=5"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainTransformReplacesAnother).Line);
    }

    // The TODO(question) D253 of HeidenhainSetpos: the second cycle 7 would replace the first, which the cycle 10 of
    // the ROTATE follows.
    [Fact]
    public void Setpos_ReplacingACycle7ThatARotationFollows_IsTheErrorCmp106()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0", "ROTATE=30", "RAPID X=1 Y=1", "SETPOS Y=0"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainTransformReplacesAnother).Line);
    }

    // Virtual machine 3.4; language 4.9: the setpos shift is the position before the SETPOS minus the declared value,
    // X 10 - 0 on the way the text runs, and the REPEAT reaches the label at X 15, where the program declares a shift
    // of 15 and the cycle 7 of the text writes 10, CMP119 on the REPEAT.
    [Fact]
    public void Setpos_AfterALabelThatARepeatReachesAtAnotherPosition_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=0 Z=5", "SETPOS X=0", "LABEL=1", "SETPOS X=0", "RAPID IX=5", "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Virtual machine 3.4; language 4.9: the REPEAT brings the setpos shift of the loop's own SETPOS, and the program
    // declares the next one from the position that shift gives, X 20 - 0, where the cycle 7 of the text writes 10,
    // CMP119 on the REPEAT.
    [Fact]
    public void Setpos_AfterALabelThatARepeatReachesWithAnotherSetposShift_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=0 Z=5", "LABEL=1", "RAPID X=10", "SETPOS X=0", "RAPID X=5", "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Virtual machine 3.4; language 4.9: the REPEAT brings the frame the text brings to the label, and the RAPID X=0
    // after it puts X at the same place on both ways, so the SETPOS declares the same shift on both and the loop runs
    // as the program does.
    [Fact]
    public void Setpos_AfterALabelAndAnAbsoluteMoveOfItsAxis_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=0 Z=5", "SETPOS X=0", "LABEL=1", "RAPID X=0", "SETPOS X=0", "RAPID IX=5",
            "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "L X+10 Y+0 Z+5 R0 FMAX\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nLBL 1\nL X+0 FMAX\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nL IX+5 FMAX\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // The TODO(question) of HeidenhainSetpos.NamedAxes: after HOME the axis is known in the MACHINE frame only (D35,
    // D101), and the shift of cycle 7 against the active preset is not known.
    [Fact]
    public void Setpos_AfterHome_IsTheErrorCmp113()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z", "SETPOS Z=100"));

        Assert.Empty(result.Files);
        Assert.Equal(5,
            HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainSetposWithoutWorkpiecePosition).Line);
    }

    // The TODO(question) D117 of HeidenhainSetpos.NamedAxes: the virtual machine changes nothing for an incremental
    // word under SETPOS, which is left unwritten, the ERROR CMP101 (language 2 rule 8).
    [Fact]
    public void Setpos_WithAnIncrementalWord_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=10 Y=20 Z=5", "SETPOS X=0 IY=2"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // Controllers heidenhain.md 6 and the TODO(question) of HeidenhainNumbers: the shift of a declared Q parameter is
    // the formula position minus Q1, which Klartext does not take as a coordinate (CMP102).
    [Fact]
    public void Setpos_OfAnExpression_IsTheErrorCmp102()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "VAR:Q1=3", "RAPID X=10 Y=20 Z=5", "SETPOS X={$Q1}"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainValueWithoutKlartext).Line);
    }
}
