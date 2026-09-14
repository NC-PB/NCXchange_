using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The program and the frame of a Klartext file (controllers heidenhain.md 1, 7 rules 4, 8 and 9; controller-mapping 1):
/// BEGIN PGM, END PGM, M30, the header of D34, SECTION, the comments, the skip, STOP, the comma, what stays RAW.
/// </summary>
public sealed class HeidenhainProgramTests
{
    // BEGIN PGM name MM opens the one program of the file with its name and its units, END PGM closes the file; the
    // header of D34 follows PROGRAM=BEGIN (controller-mapping 1; heidenhain 7 rule 4).
    [Fact]
    public void BeginPgm_WithM30AndEndPgm_IsTheProgramWithItsHeaderAndEnd()
    {
        NcxProgram program = Program("0 BEGIN PGM PART MM\n1 L X+10 Y+0 R0 FMAX\n2 M30\n3 END PGM PART MM\n");

        Assert.Equal(Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"PART\"",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "RAPID X=10 Y=0 COMP=OFF",
            "PROGRAM=END",
            "FILE=END"), Ncx.Core.Writing.NcxWriter.Write(program));
        Assert.Empty(program.Diagnostics.Items);
    }

    // INCH in BEGIN PGM is UNITS=INCH (controller-mapping 1, UNITS).
    [Fact]
    public void BeginPgm_WithInch_IsUnitsInch()
    {
        string text = Text("0 BEGIN PGM PART INCH\n1 M30\n2 END PGM PART INCH\n");

        Assert.Contains("FEED_MODE=PER_MIN COMP=OFF UNITS=INCH WORKPLANE=XY CYCLE=OFF\n", text, StringComparison.Ordinal);
    }

    // A missing M30 before END PGM is a WARNING, and the program ends with PROGRAM=END after its last block
    // (heidenhain 7 rule 4; controller-mapping 1, PROGRAM=END).
    [Fact]
    public void EndPgm_WithoutM30_WarnsAndEndsTheProgramAfterItsLastBlock()
    {
        NcxProgram program = Program("0 BEGIN PGM PART MM\n1 M5\n2 END PGM PART MM\n");
        string text = Ncx.Core.Writing.NcxWriter.Write(program);

        Assert.EndsWith(Lines("SPINDLE=OFF", "PROGRAM=END", "FILE=END"), text, StringComparison.Ordinal);
        Assert.Equal([DiagnosticCodes.ProgramEndMissing], Codes(program));
    }

    // A section after M30 that a jump enters stands in front of the end behind JUMP=END (heidenhain 7 rule 4; language
    // 4.13).
    [Fact]
    public void SectionAfterM30_EnteredByAJump_StandsInFrontOfTheEndBehindJumpEnd()
    {
        string text = Text("0 BEGIN PGM J MM\n1 FN 9: IF +Q1 EQU +0 GOTO LBL 5\n2 L X+10 R0 FMAX\n3 M30\n"
            + "4 LBL 5\n5 L X+20 R0 FMAX\n6 END PGM J MM\n");

        Assert.Equal(Lines(
            "JUMP=5 IF={$Q1 == 0}",
            "RAPID X=10 COMP=OFF",
            "JUMP=END",
            "LABEL=5",
            "RAPID X=20 COMP=OFF"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // The comma is the decimal separator; the reader accepts both (heidenhain 7 rule 8).
    [Fact]
    public void Comma_AndDot_AreBothDecimalSeparators()
    {
        Assert.Equal(Lines("RAPID X=50.4 Y=-7.025 COMP=OFF"), Body("9 L X50,4 Y-7.025 R0 FMAX"));
    }

    // * - title is SECTION, the structuring comment (controller-mapping 1, COMMENT and SECTION).
    [Fact]
    public void StarDash_IsSection()
    {
        Assert.Equal(Lines("SECTION=\"SIDE MILL D10 L35 SD10\""), Body("4 * - SIDE MILL D10 L35 SD10"));
    }

    // A comment at the end of a block is the comment of its NCX block; a comment line is trivia (controller-mapping 1;
    // D92).
    [Fact]
    public void Comment_AtTheEndOfABlock_IsTheCommentOfItsNcxBlock()
    {
        Assert.Equal(Lines(Commented("SPINDLE=CW", "; SPINDEL EIN"), "; NUR KOMMENTAR"),
            Body("7 M3 ;SPINDEL EIN\n8 ;NUR KOMMENTAR"));
    }

    // / at the block start is the optional skip, SKIP (controller-mapping 1, SKIP).
    [Fact]
    public void Slash_IsSkip()
    {
        Assert.Equal(Lines("SKIP RAPID X=10"), Body("5 /L X+10 FMAX"));
    }

    // STOP and M0 stop the program, M1 optionally (heidenhain 1; controller-mapping 1, STOP).
    [Fact]
    public void StopM0AndM1_AreStops()
    {
        Assert.Equal(Lines("STOP=PROGRAM", "STOP=PROGRAM", "STOP=OPTIONAL"), Body("5 STOP\n6 M0\n7 M1"));
    }

    // BLK FORM, APPR and DEP, TCH PROBE, FN 16, FN 18 and FN 19 are RAW with a WARNING (heidenhain 7 rule 9).
    [Theory]
    [InlineData("2 BLK FORM 0.1 Z X0 Y0 Z-20")]
    [InlineData("5 APPR LT X+10 Y+10 LEN15 RL F100")]
    [InlineData("6 DEP CT CCA90 R+5 F1000")]
    [InlineData("7 TCH PROBE 400 GRUNDDREHUNG")]
    [InlineData("8 FN 16: F-PRINT TNC:\\MASKE.A / TNC:\\PROT.TXT")]
    [InlineData("9 FN 18: SYSREAD Q1 = ID270 NR1")]
    [InlineData("10 FN 19: PLC=+10/+1")]
    public void BlkFormApprDepTchProbeFn16Fn18Fn19_AreKeptAsRawWithAWarning(string line)
    {
        NcxProgram program = FramedProgram(line);

        Block raw = Assert.Single(program.Blocks, block => block.Find("RAW") is not null);
        Assert.Equal(line, ((StringValue)raw.Find("RAW")!.Value).Content);
        Assert.Equal("HEIDENHAIN", raw.Find("RAW")!.Addr);
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // An M function no table of the machine names is MFUNC with a WARNING (machine-config 5).
    [Fact]
    public void MFunction_NoTableNames_IsMfuncWithAWarning()
    {
        NcxProgram program = FramedProgram("5 M13");

        Assert.Equal(Lines("MFUNC=13"), BodyOf(Ncx.Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.HeidenhainMCodeNotNamed], Codes(program));
    }
}
