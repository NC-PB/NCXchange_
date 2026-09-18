namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// The frame of a Klartext program (controllers heidenhain.md 1; 8 rule 1; controller-mapping 1; language 4.1, 4.13):
/// BEGIN PGM and END PGM, the block numbers, the file of each program, the comments, RAW and the block skip.
/// </summary>
public sealed class HeidenhainProgramFrameTests
{
    // Controllers heidenhain.md 8 rule 1: consecutive block numbers from 0, BEGIN PGM name MM from the program name and
    // the units, END PGM name MM after M30; the file is named after the program (heidenhain 1), and the lines end as
    // line_ending of the machine says, CRLF.
    [Fact]
    public void Rule1_Program_IsFramedByBeginPgmAndEndPgmAfterM30WithConsecutiveNumbersFromZero()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"2.5D FRAESEN\" NUMBER=1",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=10 Y=-5",
            "PROGRAM=END",
            "FILE=END"));

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        CompiledFile file = Assert.Single(result.Files);
        Assert.Equal("2.5D FRAESEN.h", file.Name);
        Assert.Equal(
            "0 BEGIN PGM 2.5D FRAESEN MM\r\n1 L X+10 Y-5 R0 FMAX\r\n2 M30\r\n3 END PGM 2.5D FRAESEN MM\r\n",
            file.Text);
    }

    // Heidenhain 8 rule 1: the units of BEGIN PGM and END PGM are those of the program, INCH for UNITS=INCH.
    [Fact]
    public void Rule1_ProgramInInches_IsBeginPgmAndEndPgmInch()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"", "UNITS=INCH", "RAPID X=1", "PROGRAM=END", "FILE=END"));

        Assert.Equal(["BEGIN PGM T INCH", "L X+1 R0 FMAX", "M30", "END PGM T INCH"],
            HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result)));
    }

    // Heidenhain 8 rule 1: BEGIN PGM needs the units, and a program without UNITS has none; the compile stops and
    // writes no file (virtual machine 2.9).
    [Fact]
    public void Rule1_ProgramWithoutUnits_IsTheErrorCmp112()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"", "PROGRAM=END", "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(2, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainProgramWithoutUnits).Line);
    }

    // Heidenhain 8 rule 1, machine-config 2 (program_layout = "file_per_program"), D48: one program per file, each
    // numbered from 0 and named after its program.
    [Fact]
    public void Rule1_TwoPrograms_AreTwoFilesEachNumberedFromZero()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"OP1\"", "UNITS=MM", "RAPID Z=5", "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"OP2\"", "UNITS=MM", "RAPID Z=10", "PROGRAM=END",
            "FILE=END"));

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(2, result.Files.Count);
        Assert.Equal("OP1.h", result.Files[0].Name);
        Assert.Equal("0 BEGIN PGM OP1 MM\r\n1 L Z+5 R0 FMAX\r\n2 M30\r\n3 END PGM OP1 MM\r\n", result.Files[0].Text);
        Assert.Equal("OP2.h", result.Files[1].Name);
        Assert.Equal("0 BEGIN PGM OP2 MM\r\n1 L Z+10 R0 FMAX\r\n2 M30\r\n3 END PGM OP2 MM\r\n", result.Files[1].Text);
    }

    // The TODO(question) of the program start: the target state of a program starts unknown (P3-03), so the header's
    // FEED_MODE=PER_MIN is M137 (controller-mapping 2, FEED_MODE); UNITS stands in BEGIN PGM, WORKPLANE waits for the
    // TOOL CALL, COMP for the first straight line, and CYCLE=OFF writes nothing (heidenhain 5).
    [Fact]
    public void Header_OfTheExamples_IsM137AndItsCompensationIsR0OnTheFirstLine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "RAPID X=10 Y=-5",
            "PROGRAM=END",
            "FILE=END"));

        Assert.Equal(["BEGIN PGM T MM", "M137", "L X+10 Y-5 R0 FMAX", "M30", "END PGM T MM"],
            HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result)));
    }

    // Controller-mapping 1, COMMENT and SECTION: SECTION is the structuring block * - text, COMMENT the comment block
    // ; text (controllers heidenhain.md 1).
    [Fact]
    public void Section_AndComment_AreAStructuringBlockAndACommentBlock()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "SECTION=\"SIDE MILL D10 L35 SD10\"", "COMMENT=\"PARKING\""));

        Assert.Equal("* - SIDE MILL D10 L35 SD10\n; PARKING", HeidenhainCompile.Body(result));
    }

    // Language 4.1, D5: RAW of the controller is written as the reader kept it; the compiler numbers the blocks
    // itself, so the block number of the source goes (heidenhain 8 rule 1).
    [Fact]
    public void Raw_OfTheController_IsWrittenWithoutTheBlockNumberOfTheSource()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAW:HEIDENHAIN=\"2 BLK FORM 0.1 Z X0 Y0 Z-20\""));

        Assert.Equal("BLK FORM 0.1 Z X0 Y0 Z-20", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 1: BEGIN PGM gives the units of the whole program, and a UNITS that changes them has
    // no Klartext form.
    [Fact]
    public void Units_ThatChangeTheUnitsOfBeginPgm_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RAPID X=0 Y=0", "UNITS=INCH"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // Controllers heidenhain.md 1, 8 rules 1 and 6: the LBL section of a subprogram stands between the BEGIN PGM and
    // the END PGM of the program that calls it, so a UNITS in it that changes the units of BEGIN PGM has no Klartext
    // form either.
    [Fact]
    public void Units_ThatChangeTheUnitsOfBeginPgmInASubprogram_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"", "UNITS=MM WORKPLANE=XY", "RAPID X=0 Y=0 Z=2", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "UNITS=INCH", "RAPID X=1 Y=1", "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // Controllers heidenhain.md 1, 8 rule 1: a UNITS in a subprogram that keeps the units of BEGIN PGM writes nothing,
    // and END PGM carries the units of BEGIN PGM.
    [Fact]
    public void Units_OfASubprogramThatKeepTheUnitsOfBeginPgm_WriteNothing()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"", "UNITS=INCH", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "UNITS=INCH", "RAPID X=1 Y=1", "SUB=END",
            "FILE=END"));

        Assert.Equal(["BEGIN PGM T INCH", "CALL LBL 100", "M30", "LBL 100", "L X+1 Y+1 R0 FMAX", "LBL 0",
                "END PGM T INCH"],
            HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result)));
    }

    // Controller-mapping 1, SKIP; controllers heidenhain.md 1: the / of the optional skip stands at the start of every
    // block the NCX block is written as.
    [Fact]
    public void Skip_IsTheSlashAtTheStartOfEveryBlockOfTheNcxBlock()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "SKIP LINE X=5 F=100 COOLANT=ON"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\n/M8\n/L X+5 F100", HeidenhainCompile.Body(result));
    }
}
