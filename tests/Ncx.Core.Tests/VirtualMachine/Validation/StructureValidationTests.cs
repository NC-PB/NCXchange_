using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The structure rules of virtual machine 5, one test per rule with the smallest file that breaks it: the file frame,
/// the sections, the words of a block (the parser's PAR codes, language 3, 4.1, 4.13, 5) and RAW.
/// </summary>
public sealed class StructureValidationTests
{
    // VM 5: missing FILE=BEGIN; it is the first block of every file (language 4.1).
    [Fact]
    public void FileBegin_Missing_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile("PROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.FileBeginNotFirstBlock);
    }

    // VM 5: misplaced FILE=BEGIN, on another block than the first (language 4.1).
    [Fact]
    public void FileBegin_Misplaced_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=BEGIN NCX=1\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.FileBeginNotFirstBlock);
    }

    // VM 5: missing NCX in the FILE=BEGIN block (language 4.1).
    [Fact]
    public void Ncx_Missing_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile("FILE=BEGIN\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.NcxVersionMissing);
    }

    // VM 5: misplaced NCX, in a block other than FILE=BEGIN (language 4.1).
    [Fact]
    public void Ncx_Misplaced_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("NCX=1").Diagnostics, DiagnosticCodes.NcxMisplaced);
    }

    // VM 5: missing FILE=END; it is the last block of every file (language 4.1).
    [Fact]
    public void FileEnd_Missing_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.FileEndNotLastBlock);
    }

    // VM 5: misplaced FILE=END, with a block after it (language 4.1).
    [Fact]
    public void FileEnd_Misplaced_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.FileEndNotLastBlock);
    }

    // VM 5: missing PROGRAM=END, the last block of every program (language 4.13).
    [Fact]
    public void ProgramEnd_Missing_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.ProgramEndMissing);
    }

    // VM 5: misplaced PROGRAM=END, outside a program (language 4.13).
    [Fact]
    public void ProgramEnd_Misplaced_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nPROGRAM=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.ProgramEndOutsideProgram);
    }

    // VM 5: missing PROGRAM=BEGIN, which leaves the blocks of the program outside every section (language 4.13).
    [Fact]
    public void ProgramBegin_Missing_LeavesABlockOutsideEverySection()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nUNITS=MM\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.BlockOutsideSection);
    }

    // VM 5: missing SUB=END, the last block of every subprogram (language 4.13).
    [Fact]
    public void SubEnd_Missing_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nSUB=BEGIN NAME=1\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.SubEndMissing);
    }

    // VM 5: misplaced SUB=END, outside a subprogram (language 4.13).
    [Fact]
    public void SubEnd_Misplaced_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nSUB=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.SubEndOutsideSub);
    }

    // Language 4.9, 4.13: SUB=BEGIN carries the NAME of its subprogram.
    [Fact]
    public void SubBegin_WithoutName_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nSUB=BEGIN\nSUB=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.SubNameMissing);
    }

    // VM 5: a block outside every section (language 4.13).
    [Fact]
    public void Block_OutsideEverySection_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nUNITS=MM\nPROGRAM=BEGIN\nPROGRAM=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.BlockOutsideSection);
    }

    // VM 5: a SUB inside a PROGRAM; subprograms stand next to the programs (language 4.13).
    [Fact]
    public void Sub_InsideAProgram_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nSUB=BEGIN NAME=1\nSUB=END\nPROGRAM=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.SubInsideProgram);
    }

    // VM 5: a file without a program (language 4.1, 4.13).
    [Fact]
    public void File_WithoutAProgram_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile("FILE=BEGIN NCX=1\nSUB=BEGIN NAME=1\nSUB=END\nFILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.FileWithoutProgram);
    }

    // VM 5: duplicate SUB, two sections of one name (VM 3.6).
    [Fact]
    public void Sub_DuplicateName_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseFile(
            "FILE=BEGIN NCX=1\nPROGRAM=BEGIN\nPROGRAM=END\nSUB=BEGIN NAME=1\nSUB=END\nSUB=BEGIN NAME=1\nSUB=END\n"
            + "FILE=END\n");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.DuplicateSectionName);
    }

    // VM 3 step 1, 5: an unknown key.
    [Fact]
    public void Key_Unknown_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("SPEED=1").Diagnostics, DiagnosticCodes.UnknownKey);
    }

    // VM 5: an unknown value (language 4.5, SPINDLE takes CW, CCW or OFF).
    [Fact]
    public void Value_Unknown_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("SPINDLE=UP").Diagnostics, DiagnosticCodes.WordValueNotAccepted);
    }

    // VM 5, D95: a pseudo-word in a user file.
    [Fact]
    public void PseudoWord_InAUserFile_IsAnError()
    {
        NcxProgram program = RuleAssert.ParseProgram("@SAVE=SPINDLE:MAIN");

        RuleAssert.Only(program.Diagnostics, DiagnosticCodes.PseudoWordInUserFile);
    }

    // VM 5: a duplicate key (language 5 rule 4).
    [Fact]
    public void Key_Duplicate_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("UNITS=MM UNITS=MM").Diagnostics, DiagnosticCodes.DuplicateKey);
    }

    // VM 5: two verbs in one block (language 5 rule 1).
    [Fact]
    public void Verb_TwoInOneBlock_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("RAPID X=1 LINE").Diagnostics, DiagnosticCodes.TwoVerbs);
    }

    // VM 5: an axis word without verb (language 5 rule 2).
    [Fact]
    public void AxisWord_WithoutVerb_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("X=1").Diagnostics, DiagnosticCodes.AxisWordWithoutVerb);
    }

    // VM 3.1a, 5: RETRACT with other axis words; RETRACT carries none (language 5 rule 2).
    [Fact]
    public void Retract_WithAnAxisWord_IsAnError()
    {
        RuleAssert.Only(RuleAssert.ParseProgram("RETRACT X=1").Diagnostics, DiagnosticCodes.AxisWordWithoutVerb);
    }

    // VM 5: MOVE or ROT without TILT or TILT_AXIS (language 5 rule 5).
    [Theory]
    [InlineData("MOVE=TURN")]
    [InlineData("ROT=TABLE")]
    public void MoveOrRot_WithoutTilt_IsAnError(string word)
    {
        RuleAssert.Only(RuleAssert.ParseProgram(word).Diagnostics, DiagnosticCodes.PartnerWordMissing);
    }

    // VM 5: PHASE or POINT without their verb or partner (language 5 rule 5).
    [Theory]
    [InlineData("PHASE=90")]
    [InlineData("POINT=2")]
    public void PhaseOrPoint_WithoutPartner_IsAnError(string word)
    {
        RuleAssert.Only(RuleAssert.ParseProgram(word).Diagnostics, DiagnosticCodes.PartnerWordMissing);
    }

    // VM 5: IF without partner, and ARG, TIMES, WITH without partner (language 5 rule 5).
    [Theory]
    [InlineData("IF={1}")]
    [InlineData("ARG:A=1")]
    [InlineData("TIMES=2")]
    [InlineData("WITH=1,2")]
    public void FlowWord_WithoutPartner_IsAnError(string word)
    {
        RuleAssert.Only(RuleAssert.ParseProgram(word).Diagnostics, DiagnosticCodes.PartnerWordMissing);
    }

    // VM 5, language 4.1: RAW present is a WARNING; it compiles only to its own controller or builder.
    [Fact]
    public void Raw_Present_Warns()
    {
        string text = VmHarness.File("RAW:NAKAMURA=\"G300\"", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.RawPresent);

        Assert.Equal(3, warning.Line);
        Assert.StartsWith("RAW:NAKAMURA is source text", warning.Message, StringComparison.Ordinal);
    }

    // VM 5: RAW present warns once per block, however often the walks of D99 pass it.
    [Fact]
    public void Raw_InASubprogramCalledTwice_WarnsOnce()
    {
        string text = VmHarness.File(
            "CALL=1 TIMES=2", "PROGRAM=END", "SUB=BEGIN NAME=1", "RAW:FANUC=\"G411\"", "SUB=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.RawPresent);
    }
}
