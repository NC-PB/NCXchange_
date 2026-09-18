namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// The M functions of Klartext and the words without a Klartext form (controllers heidenhain.md 1, 2, 4;
/// controller-mapping 1, 2, 4; machine-config 5; language 2 rule 8, 4.6, 5 rule 3).
/// </summary>
public sealed class HeidenhainFunctionTests
{
    // Machine-config 5, [spindle.TOOL] and [coolant]: M3, M4, M5, M8, M9 of the tables, each written where it changes.
    [Fact]
    public void SpindleAndCoolant_AreTheMFunctionsOfTheTablesOnChange()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "SPINDLE=CW", "COOLANT=ON", "SPINDLE=CW", "COOLANT=OFF", "SPINDLE=CCW", "SPINDLE=OFF"));

        Assert.Equal("M3\nM8\nM9\nM4\nM5", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 2, FEED_MODE: M136 per revolution, M137 per minute, on change.
    [Fact]
    public void FeedMode_IsM136OrM137OnChange()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "FEED_MODE=PER_REV", "FEED_MODE=PER_REV", "FEED_MODE=PER_MIN"));

        Assert.Equal("M136\nM137", HeidenhainCompile.Body(result));
    }

    // Language 5 rule 3 and the TODO(question) D252 of HeidenhainFunctions: the functions of a motion block act before
    // its motion, each in a Klartext block of its own before the line, in the canonical order of the words.
    [Fact]
    public void FunctionsOfAMotionBlock_StandInBlocksOfTheirOwnBeforeTheMotion()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "RAPID Z=2 COOLANT=ON SPINDLE=CW"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nM3\nM8\nL Z+2 FMAX", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, STOP: M1 and M0, after the motion of their block.
    [Fact]
    public void Stop_IsM0OrM1AfterTheMotion()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0 STOP=OPTIONAL", "STOP=PROGRAM"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nM1\nM0", HeidenhainCompile.Body(result));
    }

    // Language 4.6: MFUNC is the M function the machine configuration does not name, written with the WARNING CMP109.
    [Fact]
    public void Mfunc_IsItsMFunctionWithAWarning()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("MFUNC=77"));

        Assert.Equal("M77", HeidenhainCompile.Body(result));
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainMFunctionNotNamed).Line);
    }

    // Language 4.6, machine-config 5: FUNC:name=STATE is the template of that state of [func].
    [Fact]
    public void Func_IsTheTemplateOfItsState()
    {
        var mill = HeidenhainCompile.MillAnd("[func]\nCHIP_CONVEYOR = { ON = \"M60\", OFF = \"M61\" }");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "FUNC:CHIP_CONVEYOR=ON", "FUNC:CHIP_CONVEYOR=OFF"), mill);

        Assert.Equal("M60\nM61", HeidenhainCompile.Body(result));
    }

    // Language 4.5, machine-config 5: ORIENT is the ORIENT template of the spindle.
    [Fact]
    public void Orient_IsTheOrientTemplateOfTheSpindle()
    {
        var mill = HeidenhainCompile.MillWith("OFF = \"M5\"", "OFF = \"M5\"\nORIENT = \"M19\"");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("ORIENT=90"), mill);

        Assert.Equal("M19", HeidenhainCompile.Body(result));
    }

    // Language 2 rule 8: nothing of the program is dropped silently; a word the compiler writes no Klartext for is an
    // ERROR, POLAR=ON here, whose Heidenhain form controller-mapping 1 names only as cycle 27 or a FUNCTION per TNC.
    [Fact]
    public void WordWithoutKlartext_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RAPID X=0 Y=0", "POLAR=ON"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }
}
