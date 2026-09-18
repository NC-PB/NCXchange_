using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The frames of the Fanuc compiler (controllers fanuc.md 4; controller-mapping 1; language 4.2; D31): ORIGIN, the
/// chain with G52, G68, G51.1 and G68.2, HOME, SETPOS, the transformations, TOLERANCE, the rotary options, RETRACT.
/// </summary>
public sealed class FanucFramesTests
{
    // A tilt the machine writes through [transform] (machine-config 5).
    private const string Transform = """
        [transform]
        TILT_ON = "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"
        TILT_OFF = "G69"
        TILT_TURN = "G53.1"
        """;

    // Language 4.2 ORIGIN, controller-mapping 1: G54 to G59 are the datums 1 to 6, G54.1 P1 the datum 7.
    [Fact]
    public void Origin_DatumsOneToSixAndBeyond_AreG54ToG59AndG541P()
    {
        string body = FanucCompile.Body("ORIGIN=1\nORIGIN=2\nORIGIN=7");

        Assert.Equal("G54\nG55\nG54.1 P1\n", body);
    }

    // Controllers fanuc.md 4, 9 rule 6: SHIFT is G52 with its axes, SHIFT=RESET cancels it with G52 X0 Y0 (D31).
    [Fact]
    public void Shift_AndItsReset_AreG52()
    {
        string body = FanucCompile.Body("SHIFT X=10 Y=20\nSHIFT=RESET");

        Assert.Equal("G52 X10. Y20.\nG52 X0 Y0\n", body);
    }

    // Controller-mapping 1, ROTATE: G68 about the origin of the plane with R, the angle a real number with its point
    // (fanuc 10 rule 5), ROTATE=RESET G69 (D245).
    [Fact]
    public void Rotate_AndItsReset_AreG68AndG69()
    {
        string body = FanucCompile.Body("ROTATE=30\nROTATE=RESET");

        Assert.Equal("G68 X0 Y0 R30.\nG69\n", body);
    }

    // Controller-mapping 1, MIRROR: G51.1 with the mirrored axes at 0, MIRROR=OFF G50.1.
    [Fact]
    public void Mirror_AndOff_AreG511AndG501()
    {
        string body = FanucCompile.Body("MIRROR=X\nMIRROR=OFF");

        Assert.Equal("G51.1 X0\nG50.1 X0\n", body);
    }

    // D31: a Fanuc control holds one local shift inside the datum, so a shift after a rotation cannot be written in
    // program order.
    [Fact]
    public void Chain_ShiftAfterARotation_IsTheErrorCmp360()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("ROTATE=30", "SHIFT X=5"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucChainNotWritable, error.Code);
    }

    // Controller-mapping 1, TILT and MOVE: the TILT_ON template of [transform] with its values as real numbers
    // (fanuc 10 rule 5), G53.1 after it for MOVE=TURN, G69 for TILT=RESET.
    [Fact]
    public void Tilt_WithMoveTurn_IsTheTiltTemplateAndG531()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("TILT A=0 B=45 C=0 MOVE=TURN", "TILT=RESET"),
            FanucCompile.Mill(extra: Transform)));

        Assert.Contains("\nG68.2 X0. Y0. Z0. I0. J45. K0.\nG53.1\nG69\n", text, StringComparison.Ordinal);
    }

    // Language 4.2 MOVE (D82), controller-mapping 1 MOVE: G68.2 alone is MOVE=STAY and G53.1 after it MOVE=TURN, and
    // MOVE=MOVE, which positions the rotary axes with the tool tip on the workpiece, has no Fanuc form.
    [Fact]
    public void Tilt_WithMoveMove_IsTheErrorCmp308()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("TILT A=0 B=45 C=0 MOVE=MOVE", "TILT=RESET"), FanucCompile.Mill(extra: Transform)));

        Assert.Equal(DiagnosticCodes.FanucWordNotWritten, error.Code);
    }

    // Machine-config 3: a template that writes the point after a placeholder itself, {b}., takes the number without
    // one, so that the point stands once.
    [Fact]
    public void Tilt_TemplateWithALiteralPoint_WritesThePointOnce()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("TILT A=0 B=45 C=0", "TILT=RESET"),
            FanucCompile.Mill(extra: Transform.Replace("J{b}", "J{b}.", StringComparison.Ordinal))));

        Assert.Contains("\nG68.2 X0. Y0. Z0. I0. J45. K0.\nG69\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 4, 9 rule 3; controller-mapping 1, HOME: G91 G28 Z0 and G28 X0 Y0 as the sources write
    // them, G30 P2 for the second reference point; after the return G90 stands again with the next absolute motion.
    [Fact]
    public void Home_ZThenXY_IsG91G28Z0ThenG28X0Y0()
    {
        string body = FanucCompile.Body("RAPID X=0 Y=0\nHOME Z\nHOME X Y\nHOME Z POINT=2\nRAPID X=10");

        Assert.Equal("G17 X0. Y0.\nG91 G28 Z0\nG28 X0 Y0\nG30 P2 Z0\nG90 X10.\n", body);
    }

    // Language 4.2 SETPOS, controller-mapping 1: G92 with the declared coordinates on a mill (D55).
    [Fact]
    public void Setpos_OnAMill_IsG92()
    {
        string body = FanucCompile.Body("RAPID X=10 Y=10\nSETPOS X=0 Y=0");

        Assert.Equal("G17 X10. Y10.\nG92 X0. Y0.\n", body);
    }

    // Language 2 rule 3, D101, D244: HOME leaves G91 G28 active, and G92 takes its words as increments under G91, so
    // the SETPOS after HOME writes G90 in front of G92.
    [Fact]
    public void Setpos_AfterHome_IsG92UnderG90()
    {
        string body = FanucCompile.Body("RAPID X=10 Y=10\nHOME Z\nSETPOS X=0 Y=0");

        Assert.Equal("G17 X10. Y10.\nG91 G28 Z0\nG90 G92 X0. Y0.\n", body);
    }

    // Language 2 rule 3, 4.2 (SHIFT, ROTATE, MIRROR), D244, D245: after an incremental block G52, G68 X0 Y0 and G51.1
    // X0 would take their words as increments under G91, so G90 stands in front of them.
    [Theory]
    [InlineData("SHIFT X=10", "G90 G52 X10.")]
    [InlineData("ROTATE=30", "G90 G68 X0 Y0 R30.")]
    [InlineData("MIRROR=X", "G90 G51.1 X0")]
    public void Chain_AfterAnIncrementalMove_IsWrittenUnderG90(string block, string expected)
    {
        string body = FanucCompile.Body("LINE X=0 F=100\nLINE IX=5\n" + block);

        Assert.Equal("G1 G17 X0. F100.\nG91 X5.\n" + expected + "\n", body);
    }

    // Language 2 rule 3, D244: the G52 X0 that cancels a shift after an incremental block stands under G90 as well,
    // so that the next G52 replaces the shift instead of adding to it.
    [Fact]
    public void Shift_ResetAfterAnIncrementalMove_IsG52X0UnderG90()
    {
        string body = FanucCompile.Body("LINE X=0 F=100\nSHIFT X=5\nLINE IX=1\nSHIFT=RESET\nSHIFT X=10");

        Assert.Equal("G1 G17 X0. F100.\nG52 X5.\nG91 X1.\nG90 G52 X0\nG52 X10.\n", body);
    }

    // Controller-mapping 1, TCPM, POLAR, CYLINDER; fanuc 4: G43.4 H with the length register and G49, G12.1 and
    // G13.1, G7.1 C with the radius and G7.1 C0, where [transform] names nothing else. G49 ends the length offset as
    // well, which the holder keeps (virtual machine 4), so G43 H1 waits for the tool axis and stands before the end.
    [Fact]
    public void Transformations_OnAndOff_AreTheCodesOfFanuc4()
    {
        string body = FanucCompile.Body(
            "TOOL=1 OFFSET:LEN=1\nTCPM=ON\nTCPM=OFF\nPOLAR=ON\nPOLAR=OFF\nCYLINDER=30\nCYLINDER=OFF");

        Assert.Equal("T1 M6\nG43.4 H1\nG49\nG12.1\nG13.1\nG7.1 C30.\nG7.1 C0\nG43 H1\n", body);
    }

    // Controller-mapping 1 TCPM and 2 tool vectors, D81, fanuc 4: a line with a tool vector under TCPM is written
    // under G43.5 with the vector as I J K; the H of G43.5 applies the length register, so no G43 follows until G49
    // has ended it.
    [Fact]
    public void Tcpm_WithToolVectors_IsG435WithIJKOnTheLines()
    {
        string body = FanucCompile.Body(
            "TOOL=1 OFFSET:LEN=1\nTCPM=ON\nLINE X=1 Y=2 Z=3 TX=0 TY=0.5 TZ=0.866 F=100\nTCPM=OFF");

        Assert.Equal("T1 M6\nG43.5 H1\nG1 G17 X1. Y2. Z3. I0. J0.5 K0.866 F100.\nG49\nG43 H1\n", body);
    }

    // Language 4.4 and virtual machine 4 (OFFSET:LEN stays until an explicit word), fanuc 4 (G49 cancels the length
    // offset): after TCPM=OFF the length offset of the holder stands again with the next move of the tool axis.
    [Fact]
    public void Tcpm_Off_RestoresTheLengthOffsetOfTheHolder()
    {
        string body = FanucCompile.Body("TOOL=1\nRAPID X=0 Y=0 Z=50 OFFSET:LEN=1\nTCPM=ON\nLINE X=10 F=100"
            + "\nTCPM=OFF\nRAPID Z=2\nLINE Z=-5 F=200");

        Assert.Equal("T1 M6\nG0 G17 G43 X0. Y0. Z50. H1\nG43.4 H1\nG1 X10. F100.\nG49\nG0 G43 Z2. H1\n"
            + "G1 Z-5. F200.\n", body);
    }

    // Without length_offset_with_tool_axis the offset stands again right after G49, where TCPM=OFF stands (D7).
    [Fact]
    public void Tcpm_OffWithoutTheOption_RestoresTheLengthOffsetAfterG49()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("TOOL=1 OFFSET:LEN=1", "TCPM=ON", "TCPM=OFF", "RAPID Z=2"),
            FanucCompile.Mill(FanucCompile.SourceHeader)));

        Assert.Contains("\nT1 M6\nG43 H1\nG43.4 H1\nG49\nG43 H1\nZ2.\nM30\n", text, StringComparison.Ordinal);
    }

    // Controller-mapping 1, TOLERANCE; D85: [tolerance] ON and OFF, G5.1 Q1 and G5.1 Q0.
    [Fact]
    public void Tolerance_OnAndOff_AreTheToleranceTemplates()
    {
        string body = FanucCompile.Body("TOLERANCE=0.02\nTOLERANCE=OFF");

        Assert.Equal("G5.1 Q1\nG5.1 Q0\n", body);
    }

    // Machine-config 5, D86: ROTARY_PATH without a template of [transform] is a WARNING and not written.
    [Fact]
    public void RotaryPath_WithoutTemplate_IsAWarningCmp361()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("ROTARY_PATH=SHORTEST"), FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucOptionNotWritten);
        Assert.Contains("G80 G90 G94 G98\nM30\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    // Controller-mapping 1, RETRACT; D83: without a template of [retract] and without the kinematics module RETRACT
    // cannot be written.
    [Fact]
    public void Retract_WithoutTemplate_IsTheErrorCmp363()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=0 Y=0 Z=5", "RETRACT"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucRetractNotWritable, error.Code);
    }
}
