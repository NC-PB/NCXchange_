namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The G-code system of the target (controllers fanuc.md 3; machine-config 1, gcode_system; controller-mapping 2): a
/// lathe of system A has no G90 and G91, writes incremental words with U W H, the feed mode with G98 and G99, SETPOS
/// with G50, HOME with U0 W0, and the tool change in the turret form.
/// </summary>
public sealed class FanucSystemATests
{
    private static readonly string s_program = FanucCompile.Lines(
        "FILE=BEGIN NCX=1",
        "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
        "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
        "TOOL=1 OFFSET=1",
        "RAPID X=50 Z=2",
        "LINE IX=-10 IZ=-5 F=0.2",
        "HOME X Z",
        "SETPOS Z=0",
        "PROGRAM=END",
        "FILE=END");

    // Fanuc 3, system A: no G90 in the header, G99 for the feed per revolution, U and W for the incremental words,
    // G28 U0 W0, G50 for SETPOS, T0101 for TOOL=1 OFFSET=1.
    [Fact]
    public void SystemA_Program_WritesTheCodesOfSystemA()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(s_program, FanucCompile.Lathe()));

        Assert.Equal(
            "%\nO0001 (T)\nG18 G99 G40 G80\nT0101\nG0 X50. Z2.\nG1 U-10. W-5. F0.2\nG28 U0 W0\nG50 Z0.\nM30\n%\n",
            text);
    }

    // Fanuc 3, system A: G90 is the simple turning cycle, not a distance code, so a sweep beyond 360 degrees (D84;
    // controller-mapping 2, ANGLE) writes its turns with the absolute letters X Z alone.
    [Fact]
    public void ArcAngle_OnALatheOfSystemA_WritesTheTurnsWithoutG90()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "TOOL=1 OFFSET=1",
            "RAPID X=40 Z=0",
            "LINE IX=-10 IZ=-5 F=0.2",
            "ARC=CW CENTER:X=20 CENTER:Z=-5 ANGLE=540 F=0.1",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Equal(
            "%\nO0001 (T)\nG18 G99 G40 G80\nT0101\nG0 X40. Z0.\nG1 U-10. W-5. F0.2\n"
            + "G2 Z-5. X30. K0. I-5. F0.1\nG2 Z-5. X10. K0. I-5.\nM30\n%\n", text);
    }

    // D222: a [workpiece] template that carries the datum, G54 M428 as on the Nakamura, beside ORIGIN=1 of the same
    // block writes G54 once, G54 M428 on one line.
    [Fact]
    public void Workpiece_TemplateWithTheDatumOfTheBlock_WritesTheDatumOnce()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "ORIGIN=1 WORKPIECE=MAIN",
            "PROGRAM=END",
            "FILE=END");
        string lathe = FanucCompile.Lathe(extra: "[workpiece]\nMAIN = \"G54 M428\"");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, lathe));

        Assert.Equal("%\nO0001 (T)\nG18 G99 G40 G80\nG54 M428\nM30\n%\n", text);
    }

    // Fanuc 3, system B: G91 blocks and G95 instead of U W and G99.
    [Fact]
    public void SystemB_Program_WritesG91AndG95()
    {
        string lathe = FanucCompile.Lathe().Replace("gcode_system = \"A\"", "gcode_system = \"B\"",
            StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(s_program, lathe));

        Assert.Contains("\nG1 G91 X-10. Z-5. F0.2\nG28 X0 Z0\n", text, StringComparison.Ordinal);
        Assert.StartsWith("%\nO0001 (T)\nG90\nG18 G95 G40 G80\n", text, StringComparison.Ordinal);
    }
}
