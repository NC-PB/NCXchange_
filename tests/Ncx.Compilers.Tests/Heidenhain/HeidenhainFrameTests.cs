namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// HOME as M91 moves, ORIGIN and the frame chain, the modes of the control, RETRACT and TOLERANCE (controllers
/// heidenhain.md 2, 3; 8 rule 5; controller-mapping 1; language 4.2; machine-config 4 and 5; D31, D82, D83, D85, D86,
/// D100).
/// </summary>
public sealed class HeidenhainFrameTests
{
    // Heidenhain 8 rule 5: HOME is L ... FMAX M91 to the reference coordinates of [[axis]], home = 0 on the iTNC 530 of
    // the repository; an L block, which takes R0 as the first line of the program does, whose compensation the control
    // does not know yet (heidenhain 2).
    [Fact]
    public void Rule5_Home_IsAnM91MoveToTheReferenceCoordinates()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z", "HOME X Y"));

        Assert.Equal("L Z+0 R0 FMAX M91\nL X+0 Y+0 FMAX M91", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5, language 4.4: COMP applies from the motion of its block on, and HOME is written as an L
    // block, which carries R0, RL and RR (heidenhain 2), so the COMP=OFF of a block without a motion is the R0 of the
    // M91 line that follows it.
    [Fact]
    public void Rule5_HomeAfterCompOff_WritesR0OnTheM91Line()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LINE X=10 COMP=LEFT", "LINE X=20", "COMP=OFF", "HOME X Y", "HOME Z"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z-1 F100\nL X+10 RL\nL X+20\nL X+0 Y+0 R0 FMAX M91\nL Z+0 FMAX M91",
            HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5: home2 for POINT=2.
    [Fact]
    public void Rule5_HomeOfPoint2_IsTheM91MoveToHome2()
    {
        var mill = HeidenhainCompile.MillWith(
            "acceleration = 3500\nhome = 0", "acceleration = 3500\nhome = 0\nhome2 = -100");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z POINT=2"), mill);

        Assert.Equal("L Z-100 R0 FMAX M91", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5, D100: an axis without a reference point is an ERROR of this compiler, not of ncx check.
    [Fact]
    public void Rule5_HomeOfAnAxisWithoutReferencePoint_IsTheErrorCmp100()
    {
        var mill = HeidenhainCompile.MillWith("acceleration = 3500\nhome = 0", "acceleration = 3500");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z"), mill);

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainHomeWithoutReferencePoint).Line);
    }

    // Controllers heidenhain.md 3; controller-mapping 1, ORIGIN: ORIGIN=1 is cycle 247 with the preset Q339.
    [Fact]
    public void Origin_IsCycle247WithThePreset()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("ORIGIN=1"));

        Assert.Equal("CYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 3; controller-mapping 1, SHIFT: cycle 7 axis by axis, and its reset the cycle with
    // every axis at 0.
    [Fact]
    public void Shift_IsCycle7AxisByAxisAndItsResetTheCycleWithZeros()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=60 Y=40 Z=-5", "SHIFT=RESET"));

        Assert.Equal(
            "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+60\nCYCL DEF 7.2 Y+40\nCYCL DEF 7.3 Z-5\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\nCYCL DEF 7.2 Y+0\nCYCL DEF 7.3 Z+0",
            HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 3; controller-mapping 1, ROTATE and MIRROR: cycle 10 with ROT, cycle 8 with the axes,
    // their cancels cycle 10 with ROT+0 and cycle 8 without axes, in the order of the chain (D31).
    [Fact]
    public void RotateAndMirror_AreCycles10And8InTheOrderOfTheChain()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "MIRROR=X", "MIRROR=OFF", "ROTATE=RESET"));

        Assert.Equal(
            "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+30\nCYCL DEF 8.0 SPIEGELN\nCYCL DEF 8.1 X\n"
            + "CYCL DEF 8.0 SPIEGELN\nCYCL DEF 8.1\nCYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2, D31, and the TODO(question) D253 of HeidenhainChain: ORIGIN empties the chain, so the shift is
    // cancelled before cycle 247.
    [Fact]
    public void Origin_OverAShift_CancelsTheShiftBeforeCycle247()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=10", "ORIGIN=1"));

        Assert.Equal(
            "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\n"
            + "CYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1",
            HeidenhainCompile.Body(result));
    }

    // The TODO(question) D253 of HeidenhainChain.Append: a second cycle 7 would replace the first on the control, so a
    // SHIFT appended while a shift stands in the chain is CMP106.
    [Fact]
    public void Shift_WhileAShiftStandsInTheChain_IsTheErrorCmp106()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=10", "SHIFT Y=5"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainTransformReplacesAnother).Line);
    }

    // Controller-mapping 1, TILT and MOVE; machine-config 5, [transform]: PLANE SPATIAL through TILT_ON with the angles
    // and the {move} of MOVE, PLANE RESET through TILT_OFF.
    [Fact]
    public void Tilt_IsThePlaneTemplateWithTheMoveOfTheMachine()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TILT_ON = "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move}"
            TILT_OFF = "PLANE RESET STAY"
            move = { TURN = "TURN FMAX", MOVE = "MOVE ABST50 FMAX", STAY = "STAY" }
            """);

        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=TURN", "TILT=RESET"), mill);

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 TURN FMAX\nPLANE RESET STAY", HeidenhainCompile.Body(result));
    }

    // D82; machine-config 5: an empty TILT_AXIS_ON means the target writes TILT_AXIS only with the kinematics module.
    [Fact]
    public void TiltAxis_WithAnEmptyTemplate_IsTheErrorCmp107()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TILT_AXIS_ON = ""

            [[axis]]
            id = "A1"
            ncx = "A"
            letter = "A"
            kind = "rotary"
            owner = "TABLE1"

            [[axis]]
            id = "C1"
            ncx = "C"
            letter = "C"
            kind = "rotary"
            owner = "TABLE1"
            """);

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TILT_AXIS A=0 C=90"), mill);

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordNeedsKinematics).Line);
    }

    // Controllers heidenhain.md 2; controller-mapping 1, TCPM, ROTARY_PATH, ROTARY_FEED; D86: M128 and M129, M126 and
    // M127, M116 and M117, the controller's own M functions where the machine names no template, each on change.
    [Fact]
    public void Modes_AreTheControllersOwnMFunctionsOnChange()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TCPM=ON", "ROTARY_PATH=SHORTEST", "ROTARY_FEED=MM_MIN", "TCPM=ON", "ROTARY_PATH=FULL", "TCPM=OFF"));

        Assert.Equal("M128\nM126\nM116\nM127\nM129", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, TCPM; machine-config 5: FUNCTION TCPM where the machine's [transform] names it.
    [Fact]
    public void Tcpm_OfAMachineWithItsTemplates_IsFunctionTcpm()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TCPM_ON = "FUNCTION TCPM F TCP AXIS POS"
            TCPM_OFF = "FUNCTION RESET TCPM"
            """);

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TCPM=ON", "TCPM=OFF"), mill);

        Assert.Equal("FUNCTION TCPM F TCP AXIS POS\nFUNCTION RESET TCPM", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, RETRACT; machine-config 5, [retract]; D83: bare RETRACT is M140 MB MAX, RETRACT=50 is
    // M140 MB50.
    [Fact]
    public void Retract_IsM140MbOfTheMachine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RETRACT", "RETRACT=50"));

        Assert.Equal("M140 MB MAX\nM140 MB50", HeidenhainCompile.Body(result));
    }

    // Language 4.4; controllers heidenhain.md 2, differences.md (radius compensation): M140 carries no R0, RL or RR,
    // so a RETRACT after a COMP=OFF that no L block has written would run with the RL of the control, CMP115.
    [Fact]
    public void Retract_AfterACompensationChangeNoLineWrote_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LINE X=10 COMP=LEFT", "COMP=OFF", "RETRACT"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Controller-mapping 1, TOLERANCE; machine-config 5, [tolerance]; D85: cycle 32 with the tolerance, the mode and
    // the rotary tolerance, and TOLERANCE=OFF the cycle with T0.
    [Fact]
    public void Tolerance_IsCycle32OfTheMachine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF"));

        Assert.Equal(
            "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0,02\nCYCL DEF 32.2 HSC-MODE:0 TA0,05\n"
            + "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0",
            HeidenhainCompile.Body(result));
    }

    // The TODO(question) D151 of HeidenhainFrames.WriteTolerance: without TOLERANCE:ROTARY the {rotary} of the template
    // has no value, which leaves the template unusable (machine-config introduction).
    [Fact]
    public void ToleranceWithoutRotaryTolerance_LeavesTheTemplateWithoutAValue()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOLERANCE=0.02"));

        Assert.Empty(result.Files);
        Assert.Contains(result.Diagnostics.Items, diagnostic => diagnostic.Message.Contains(
            "{rotary}", StringComparison.Ordinal));
    }
}
