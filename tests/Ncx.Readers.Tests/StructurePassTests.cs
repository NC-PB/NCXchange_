using Ncx.Core.Model;
using static Ncx.Readers.Tests.Fakes.FakeRead;

namespace Ncx.Readers.Tests;

/// <summary>
/// The structure pass on the reader side: the file frame, programs and subprograms as sections, the code a source
/// keeps after its end in front of PROGRAM=END behind JUMP=END, and the M99 loop (language 4.13, D48, D89;
/// controller-mapping 1 and 6).
/// </summary>
public sealed class StructurePassTests
{
    // The case of INCREMENTAL_SUB: a GOTO 300 to a parking section below M30 and a GOTO 22 back to the M30. The
    // section lands in front of the end behind JUMP=END with the labels intact, PROGRAM=END follows it, and the
    // subprogram after the end is a SUB section (language 4.13, D89; examples/INCREMENTAL_SUB.ncx).
    [Fact]
    public void JumpEnteredSection_BelowM30_LandsInFrontOfTheEndBehindJumpEnd()
    {
        NcxProgram program = Program(Lines(
            "%",
            "O0003 (SLOT ROW)",
            "G0 X0 Y0",
            "M98 P100 L4",
            "M5",
            "GOTO 300",
            "N22 M30",
            "",
            "N300 (PARKING)",
            "G28 Z0",
            "G28 X0 Y0",
            "GOTO 22",
            "",
            "O0100",
            "G1 Z-3. F200",
            "G91 X30. F800",
            "G90",
            "M99",
            "%"));
        string text = Core.Writing.NcxWriter.Write(program);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"SLOT ROW\" NUMBER=3",
                "RAPID X=0 Y=0",
                "CALL=100 TIMES=4",
                "SPINDLE=OFF",
                "JUMP=300",
                "LABEL=22",
                "JUMP=END",
                "",
                Commented("LABEL=300", "; PARKING"),
                "HOME Z",
                "HOME X Y",
                "JUMP=22",
                "PROGRAM=END",
                "",
                "SUB=BEGIN NAME=100",
                "LINE Z=-3 F=200",
                "LINE IX=30 F=800",
                "SUB=END",
                "FILE=END"),
            text);
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        Assert.Equal(7, Assert.Single(program.Blocks, block => block.Has("PROGRAM", null, "END")).Line);
        AssertFormatsToItself(text);
    }

    // M99 in a program, where no subprogram is active, jumps back to the first block: LABEL=START after the header,
    // /M99 as SKIP JUMP=START, and the M30 after it is the end (controller-mapping 6, language 4.13).
    [Fact]
    public void M99Loop_SkippedM99BeforeM30_IsSkipJumpStartToALabelAfterTheHeader()
    {
        NcxProgram program = Program(Lines("%", "O0001 (BAR)", "G0 X0", "/M99", "M30", "%"));
        string text = Core.Writing.NcxWriter.Write(program);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"BAR\" NUMBER=1",
                "LABEL=START",
                "RAPID X=0",
                "SKIP JUMP=START",
                "PROGRAM=END",
                "FILE=END"),
            text);
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        AssertFormatsToItself(text);
    }

    // A bar-work program that ends with M99 loops for ever: JUMP=START, and PROGRAM=END after it closes the section
    // without a WARNING, the loop being the program's way to end (controller-mapping 6).
    [Fact]
    public void M99Loop_M99AtTheEndWithoutM30_IsJumpStartAndTheProgramEnds()
    {
        NcxProgram program = Program(Lines("%", "O0001", "G0 X0", "M99", "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "LABEL=START",
                "RAPID X=0",
                "JUMP=START",
                "PROGRAM=END",
                "FILE=END"),
            Core.Writing.NcxWriter.Write(program));
        Assert.Empty(program.Diagnostics.Items);
    }

    // A block-skipped /M30 is not the program end: SKIP JUMP=END there and the real PROGRAM=END at the end of the
    // program (controller-mapping 6).
    [Fact]
    public void SkippedM30_BeforeTheEnd_IsSkipJumpEnd()
    {
        string text = Text(Lines("%", "O0001", "G0 X0", "/M30", "G0 X10", "M30", "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "RAPID X=0",
                "SKIP JUMP=END",
                "RAPID X=10",
                "PROGRAM=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // A section below the end that ends the program, the N2 M2 of the Nakamura sample, ends with JUMP=END
    // (language 4.13).
    [Fact]
    public void JumpEnteredSection_EndingTheProgram_EndsWithJumpEnd()
    {
        string text = Text(Lines("%", "O1000", "GOTO 2", "N30 M30", "N2 M2 (STOP)", "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1000",
                "JUMP=2",
                "JUMP=END",
                Commented("LABEL=2", "; STOP"),
                "JUMP=END",
                "PROGRAM=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // Several O programs in one file are several PROGRAM sections; an O program after the end that returns with M99
    // is a SUB section named by its number (controller-mapping 1, 6; language 4.13).
    [Fact]
    public void Sections_TwoProgramsAndASubprogram_AreThreeSections()
    {
        NcxProgram program = Program(Lines(
            "%", "O0001", "M98 P200", "M30", "O0002", "G0 X1", "M30", "O0200", "G0 X2", "M99", "%"));

        Assert.Equal(2, program.Programs.Count);
        Assert.Equal("200", Assert.Single(program.Subs).Name);
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
    }

    // An O section that a block of the file calls is a subprogram, also when it holds an M30: a CALL of a program is
    // an ERROR (language 4.13), and an M30 in a subprogram ends the program from the call, JUMP=END (virtual machine
    // 3.6). The M99 with code after it that a GOTO enters is RETURN, and check finds no CALL of a program.
    [Fact]
    public void Sections_CalledOSectionHoldingM30_IsASubWhoseM30IsJumpEnd()
    {
        NcxProgram program = Program(Lines(
            "%", "O0001", "M98 P100", "M30", "O0100", "GOTO 9", "G0 X5", "M99", "N9 M30", "%"));
        string text = Core.Writing.NcxWriter.Write(program);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "CALL=100",
                "PROGRAM=END",
                "SUB=BEGIN NAME=100",
                "JUMP=9",
                "RAPID X=5",
                "RETURN",
                "LABEL=9",
                "JUMP=END",
                "SUB=END",
                "FILE=END"),
            text);
        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        AssertFormatsToItself(text);
        // The fake reader knows no G21: check runs the text with the UNITS a real reader takes from the source, as
        // the subprogram's motion needs it (VM200).
        Diagnostics check = Check(text.Replace("PROGRAM=BEGIN NUMBER=1\n", "PROGRAM=BEGIN NUMBER=1\nUNITS=MM\n",
            StringComparison.Ordinal));
        Assert.DoesNotContain(Core.Model.DiagnosticCodes.CallOfProgram, Codes(check));
        Assert.False(check.HasErrors, check.ToText());
    }

    // The reading the structure pass keeps until the open question of StructurePass.DecideKinds is answered: an O
    // section that no block of the file calls is a program when it holds an M30, so a subprogram in its own file that
    // holds one reads as a program, its M99 as the loop of controller-mapping 6.
    [Fact]
    public void Sections_UncalledOSectionHoldingM30_IsAProgramWhoseM99Loops()
    {
        string text = Text(Lines("%", "O0100", "GOTO 9", "G0 X5", "M99", "N9 M30", "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=100",
                "LABEL=START",
                "JUMP=9",
                "RAPID X=5",
                "JUMP=START",
                "LABEL=9",
                "PROGRAM=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // A file holds at least one program (language 4.13): when no O section is one, the first that no block of the
    // file calls is the program, its M99 the loop, and the O section it calls stays a subprogram, wherever it stands.
    [Fact]
    public void Sections_NoOSectionEndsTheProgram_TheFirstUncalledOneIsTheProgram()
    {
        string text = Text(Lines("%", "O0200", "G0 X2", "M99", "O0001", "M98 P200", "M99", "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "SUB=BEGIN NAME=200",
                "RAPID X=2",
                "SUB=END",
                "PROGRAM=BEGIN NUMBER=1",
                "LABEL=START",
                "CALL=200",
                "JUMP=START",
                "PROGRAM=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
        // The fake reader knows no G21: check runs the text with the UNITS a real reader takes from the source, as
        // the subprogram's motion needs it (VM200).
        Diagnostics check = Check(text.Replace("PROGRAM=BEGIN NUMBER=1\n", "PROGRAM=BEGIN NUMBER=1\nUNITS=MM\n",
            StringComparison.Ordinal));
        Assert.DoesNotContain(Core.Model.DiagnosticCodes.CallOfProgram, Codes(check));
        Assert.False(check.HasErrors, check.ToText());
    }

    // A subprogram whose M99 is skipped or has code after it returns with RETURN there, and SUB=END is its last
    // block (language 4.9, 4.13).
    [Fact]
    public void Subprogram_SkippedM99BeforeMoreCode_IsSkipReturnAndSubEndStaysLast()
    {
        string text = Text(Lines("%", "O0001", "M98 P100", "M30", "O0100", "/M99", "G0 X5", "M99", "%"));

        Assert.EndsWith(Lines("SUB=BEGIN NAME=100", "SKIP RETURN", "RAPID X=5", "SUB=END", "FILE=END"), text,
            StringComparison.Ordinal);
        AssertFormatsToItself(text);
    }

    // Every program ends with PROGRAM=END (language 4.13): a source program without an end gets it after its last
    // block and a WARNING.
    [Fact]
    public void Program_WithoutAnEnd_EndsAfterItsLastBlockWithAWarning()
    {
        NcxProgram program = Program(Lines("%", "O0001", "G0 X0", "%"));

        Assert.EndsWith(Lines("RAPID X=0", "PROGRAM=END", "FILE=END"), Core.Writing.NcxWriter.Write(program),
            StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(program.Diagnostics.Items);
        Assert.Equal((DiagnosticCodes.ProgramEndMissing, Severity.Warning, 2),
            (warning.Code, warning.Severity, warning.Line));
    }

    // Every subprogram ends with SUB=END (language 4.13): a source subprogram without a return gets it after its
    // last block and a WARNING.
    [Fact]
    public void Subprogram_WithoutAReturn_EndsAfterItsLastBlockWithAWarning()
    {
        NcxProgram program = Program(Lines("O0001", "M98 P100", "M30", "O0100 (SUB)", "G0 X0"));

        Assert.EndsWith(Lines(Commented("SUB=BEGIN NAME=100", "; SUB"), "RAPID X=0", "SUB=END", "FILE=END"),
            Core.Writing.NcxWriter.Write(program), StringComparison.Ordinal);
        Assert.Equal([DiagnosticCodes.SubReturnMissing], Codes(program));
    }

    // A file without % and without an O line, the start and the end of the file frame it and its blocks are one
    // program without a name (controller-mapping 1, language 4.1 and 4.13).
    [Fact]
    public void FileFrame_SourceWithoutPercentAndO_IsFramedAndOneProgram()
    {
        string text = Text(Lines("G0 X0", "M30"));

        Assert.Equal(
            Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN", "RAPID X=0", "PROGRAM=END", "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // The optional block skip, / or /n in front of a block, is SKIP or SKIP=n on the blocks read from it (language
    // 4.1, controller-mapping 1).
    [Fact]
    public void BlockSkip_SlashAndSlashN_AreSkipAndSkipN()
    {
        string text = Text(Lines("%", "O0001", "/G0 X1", "/2 G0 X2", "M30", "%"));

        Assert.Contains("\nSKIP RAPID X=1\nSKIP=2 RAPID X=2\n", text, StringComparison.Ordinal);
        AssertFormatsToItself(text);
    }
}
