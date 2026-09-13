using Ncx.Core.Model;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The file structure: FILE=BEGIN NCX=1 as the first block, FILE=END as the last, trivia around them, programs and
/// subprograms as sections of the file, and one test per structural ERROR of virtual machine 5 with the smallest input
/// that triggers it (language 4.1, 4.13; D92).
/// </summary>
public sealed class StructurePassTests
{
    // A file with two programs and one subprogram yields three sections with the right block ranges; the ranges count
    // blocks, not lines, so trivia between them does not move them (P0-04, Done when; language 4.13; D92).
    [Fact]
    public void Language413_TwoProgramsAndOneSubprogram_AreThreeSections()
    {
        NcxProgram program = ParseText.File("""
            ; two programs and a subprogram
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="SHAFT" NUMBER=1
            CALL=100
            PROGRAM=END

            SUB=BEGIN NAME=100
            LINE IX=30 F=800
            SUB=END
            PROGRAM=BEGIN NAME="SHAFT_OP2" NUMBER=2 CHANNEL=2
            RAPID X=0
            RAPID Z=2
            PROGRAM=END
            FILE=END
            """);

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        Assert.Equal(3, program.Sections.Count);
        AssertSection(program.Sections[0], SectionKind.Program, "SHAFT", 1, 1, 3);
        AssertSection(program.Sections[1], SectionKind.Sub, "100", 1, 4, 6);
        AssertSection(program.Sections[2], SectionKind.Program, "SHAFT_OP2", 2, 7, 10);
        Assert.Equal(1, program.Sections[0].Number);
        Assert.Null(program.Sections[1].Number);
        Assert.Equal(2, program.Sections[2].Number);
    }

    // Comment-only and blank lines may stand before FILE=BEGIN and after FILE=END (D92).
    [Fact]
    public void D92_TriviaAroundTheFileFrame_IsAllowed()
    {
        NcxProgram program = ParseText.File(
            "; head\n\nFILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n\n; tail\n");

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        Assert.Equal(3, program.FileBegin?.Line);
        Assert.Equal(6, program.FileEnd?.Line);
    }

    // FILE=BEGIN NCX=1 is the first block of every file (language 4.1; virtual machine 5, missing FILE=BEGIN).
    [Fact]
    public void VM5_FirstBlockNotFileBegin_IsError()
    {
        Assert.Equal(1, Error("PROGRAM=BEGIN\nPROGRAM=END", DiagnosticCodes.FileBeginNotFirstBlock).Line);
    }

    // FILE=BEGIN stands on the first block only (language 4.1; virtual machine 5, misplaced FILE=BEGIN).
    [Fact]
    public void VM5_FileBeginNotTheFirstBlock_IsError()
    {
        Assert.Equal(2, Error("FILE=BEGIN NCX=1\nFILE=BEGIN NCX=1", DiagnosticCodes.FileBeginNotFirstBlock).Line);
    }

    // NCX=1 stands in the FILE=BEGIN block (language 3 EBNF, 4.1; virtual machine 5, missing NCX).
    [Fact]
    public void VM5_FileBeginWithoutNcx_IsError()
    {
        Assert.Equal(1, Error("FILE=BEGIN\nFILE=END", DiagnosticCodes.NcxVersionMissing).Line);
    }

    // The format version is 1 (language 4.1).
    [Fact]
    public void Language41_UnknownNcxVersion_IsError()
    {
        Assert.Equal(1, Error("FILE=BEGIN NCX=2\nFILE=END", DiagnosticCodes.NcxVersionUnknown).Line);
    }

    // NCX stands in the FILE=BEGIN block and nowhere else (language 4.1; virtual machine 5, misplaced NCX).
    [Fact]
    public void VM5_NcxOutsideFileBegin_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nNCX=1", DiagnosticCodes.NcxMisplaced).Line);
    }

    // FILE=END is the last block of every file (language 4.1; virtual machine 5, missing FILE=END).
    [Fact]
    public void VM5_LastBlockNotFileEnd_IsError()
    {
        Assert.Equal(2, Error("FILE=BEGIN NCX=1\nPROGRAM=BEGIN", DiagnosticCodes.FileEndNotLastBlock).Line);
    }

    // No block follows FILE=END (language 4.1; virtual machine 5, misplaced FILE=END).
    [Fact]
    public void VM5_BlockAfterFileEnd_IsError()
    {
        Assert.Equal(1, Error("FILE=END\nRAPID X=1", DiagnosticCodes.FileEndNotLastBlock).Line);
    }

    // The blocks of the file frame hold the words of their grammar only: FILE=BEGIN NCX=1, FILE=END (language 3 EBNF).
    [Fact]
    public void Language3_FileBeginWithAnotherWord_IsError()
    {
        Assert.Equal(1, Error("FILE=BEGIN NCX=1 UNITS=MM\nFILE=END", DiagnosticCodes.StructuralBlockOtherWord).Line);
    }

    // PROGRAM=END is a block of its own (language 3 EBNF, 4.1).
    [Fact]
    public void Language3_ProgramEndWithAnotherWord_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nSPINDLE=OFF PROGRAM=END", DiagnosticCodes.StructuralBlockOtherWord).Line);
    }

    // A block stands inside a program or a subprogram (language 4.13; virtual machine 5).
    [Fact]
    public void VM5_BlockOutsideEverySection_IsError()
    {
        Assert.Equal(2, Error("FILE=BEGIN NCX=1\nRAPID X=1", DiagnosticCodes.BlockOutsideSection).Line);
    }

    // Subprograms stand next to the programs, never inside one (language 4.9, 4.13; virtual machine 5).
    [Fact]
    public void VM5_SubInsideProgram_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nSUB=BEGIN NAME=1", DiagnosticCodes.SubInsideProgram).Line);
    }

    // A file holds one or more programs (language 4.1, 4.13; virtual machine 5).
    [Fact]
    public void VM5_FileWithoutProgram_IsError()
    {
        NcxProgram program = ParseText.File("FILE=BEGIN NCX=1\nFILE=END");

        Diagnostic diagnostic = Assert.Single(program.Diagnostics.Items);
        Assert.Equal(DiagnosticCodes.FileWithoutProgram, diagnostic.Code);
    }

    // Section names are unique in the file: NAME=1 and NAME="1" name the same subprogram (virtual machine 3.6, 5).
    [Fact]
    public void VM5_DuplicateSectionName_IsError()
    {
        Assert.Equal(2, Error("SUB=BEGIN NAME=1\nSUB=BEGIN NAME=\"1\"", DiagnosticCodes.DuplicateSectionName).Line);
    }

    // A program ends with its PROGRAM=END (language 4.13; virtual machine 5, missing PROGRAM=END).
    [Fact]
    public void VM5_ProgramWithoutProgramEnd_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nFILE=END", DiagnosticCodes.ProgramEndMissing).Line);
    }

    // PROGRAM=END appears exactly once per program (language 4.13; virtual machine 5, misplaced PROGRAM=END).
    [Fact]
    public void Language413_ProgramEndTwice_IsError()
    {
        const string Text = "PROGRAM=BEGIN\nPROGRAM=END\nPROGRAM=END";

        Assert.Equal(3, Error(Text, DiagnosticCodes.ProgramEndOutsideProgram).Line);
    }

    // PROGRAM=END stands in a program; in a subprogram it is misplaced (language 4.13; virtual machine 5, misplaced
    // PROGRAM=END).
    [Fact]
    public void VM5_ProgramEndInASubprogram_IsError()
    {
        Assert.Equal(2, Error("SUB=BEGIN NAME=1\nPROGRAM=END", DiagnosticCodes.ProgramEndOutsideProgram).Line);
    }

    // Programs do not nest: a PROGRAM=BEGIN inside a program ends that program without its PROGRAM=END (language 4.13;
    // virtual machine 5, misplaced PROGRAM=BEGIN).
    [Fact]
    public void VM5_ProgramBeginInsideAProgram_IsProgramEndMissing()
    {
        List<Diagnostic> errors = Errors("PROGRAM=BEGIN\nPROGRAM=BEGIN", DiagnosticCodes.ProgramEndMissing);

        Assert.Equal(2, errors[0].Line);
        Assert.Contains("program of line 1", errors[0].Message, StringComparison.Ordinal);
    }

    // Subprograms do not nest: a SUB=BEGIN inside a subprogram ends that one without its SUB=END (language 4.13;
    // virtual machine 5, misplaced SUB=BEGIN).
    [Fact]
    public void VM5_SubBeginInsideASubprogram_IsSubEndMissing()
    {
        List<Diagnostic> errors = Errors("SUB=BEGIN NAME=1\nSUB=BEGIN NAME=2", DiagnosticCodes.SubEndMissing);

        Assert.Equal(2, errors[0].Line);
        Assert.Contains("subprogram of line 1", errors[0].Message, StringComparison.Ordinal);
    }

    // A subprogram ends with its SUB=END (language 4.13; virtual machine 5, missing SUB=END).
    [Fact]
    public void VM5_SubWithoutSubEnd_IsError()
    {
        Assert.Equal(2, Error("SUB=BEGIN NAME=1\nFILE=END", DiagnosticCodes.SubEndMissing).Line);
    }

    // SUB=END ends a subprogram and stands nowhere else (language 4.13; virtual machine 5, misplaced SUB=END).
    [Fact]
    public void VM5_SubEndOutsideSub_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nSUB=END", DiagnosticCodes.SubEndOutsideSub).Line);
    }

    // SUB=BEGIN carries the NAME of the subprogram (language 3 EBNF, 4.9).
    [Fact]
    public void Language49_SubBeginWithoutName_IsError()
    {
        Assert.Equal(1, Error("SUB=BEGIN\nSUB=END", DiagnosticCodes.SubNameMissing).Line);
    }

    // END is reserved for JUMP=END (language 4.9; virtual machine 5, LABEL=END).
    [Fact]
    public void VM5_LabelEnd_IsError()
    {
        Assert.Equal(2, Error("PROGRAM=BEGIN\nlabel=end", DiagnosticCodes.LabelEnd).Line);
    }

    // The header words of PROGRAM=BEGIN give the section its name, number and channel (language 4.1, 4.14).
    [Fact]
    public void Language41_HeaderWords_GiveTheSectionItsNameNumberAndChannel()
    {
        NcxProgram program = ParseText.File(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"HEXAGON_FACE\" NUMBER=1 CHANNEL=2\nPROGRAM=END\nFILE=END\n");

        Section section = Assert.Single(program.Programs);
        Assert.Equal("HEXAGON_FACE", section.Name);
        Assert.Equal(1, section.Number);
        Assert.Equal(2, section.Channel);
    }

    // A program without CHANNEL runs on channel 1 and one without NAME has none (language 4.1).
    [Fact]
    public void Language41_ProgramWithoutHeaderWords_HasTheDefaults()
    {
        NcxProgram program = ParseText.File("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n");

        Section section = Assert.Single(program.Programs);
        Assert.Null(section.Name);
        Assert.Null(section.Number);
        Assert.Equal(1, section.Channel);
    }

    // Parses a file and returns the one ERROR of the rule among what the parser reported.
    private static Diagnostic Error(string text, string code)
    {
        NcxProgram program = ParseText.File(text);

        Diagnostic diagnostic = ParseText.Single(program.Diagnostics, code);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        return diagnostic;
    }

    // Parses a file and returns every ERROR of the rule, at least one, in the order reported.
    private static List<Diagnostic> Errors(string text, string code)
    {
        NcxProgram program = ParseText.File(text);

        var errors = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                errors.Add(diagnostic);
            }
        }

        Assert.True(errors.Count > 0, $"Expected {code}, got:\n{program.Diagnostics.ToText()}");
        return errors;
    }

    private static void AssertSection(
        Section section, SectionKind kind, string name, int channel, int firstBlock, int lastBlock)
    {
        Assert.Equal(kind, section.Kind);
        Assert.Equal(name, section.Name);
        Assert.Equal(channel, section.Channel);
        Assert.Equal(firstBlock, section.FirstBlock);
        Assert.Equal(lastBlock, section.LastBlock);
    }
}
