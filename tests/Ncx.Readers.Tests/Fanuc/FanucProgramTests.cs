using Ncx.Core.Model;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 1, program and frame words without the frames of FanucFrameTests: the file
/// frame, the program, its end, the header of D34, the units, the plane, the datums, the block skip, the comments, the
/// stops and the dwell.
/// </summary>
public sealed class FanucProgramTests
{
    // The leading % is FILE=BEGIN, Oxxxx (name) PROGRAM=BEGIN with NAME and NUMBER, M30 PROGRAM=END, the closing %
    // FILE=END (controller-mapping 1).
    [Fact]
    public void Percent_OAndM30_AreTheFileAndTheProgram()
    {
        string text = Text("%\nO0001 (2.5D FRAESEN)\nG0 X0\nM30\n%\n");

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"2.5D FRAESEN\" NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "RAPID X=0",
                "PROGRAM=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // A source M2 is not preserved: it is PROGRAM=END like M30 (D49, controller-mapping 1).
    [Fact]
    public void M2_IsProgramEnd()
    {
        string text = Text("%\nO0001\nG0 X0\nM2\n%\n");

        Assert.Contains("\nRAPID X=0\nPROGRAM=END\nFILE=END\n", text, StringComparison.Ordinal);
    }

    // Writers emit a complete header (D34); the header blocks of the source give its values and write no word of
    // their own (examples/2.5D_FRAESEN.ncx: N10 G0 G40, N20 G80 G90 G94 G98 and N70 G17 are the header state).
    [Fact]
    public void Header_SourceHeaderBlocks_AreFoldedIntoTheCompleteHeader()
    {
        string text = Text("%\nO0001\nN10 G0 G40\nN20 G80 G90 G95 G98\nN30 G54\nN40 G0 G17 X1.\nM30\n%\n");

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "ORIGIN=1",
                "RAPID X=1",
                "PROGRAM=END",
                "FILE=END"),
            text);
    }

    // UNITS comes from G20 and G21, else from the configuration (controller-mapping 1); a header G20 is the header's.
    [Fact]
    public void Units_G20InTheHeaderAndLater_IsUnitsInch()
    {
        string header = Text("%\nO0001\nG20\nG0 X1.\nM30\n%\n");

        Assert.Contains("\nFEED_MODE=PER_MIN COMP=OFF UNITS=INCH WORKPLANE=XY CYCLE=OFF\n", header,
            StringComparison.Ordinal);
        Assert.Equal(Lines("RAPID X=1", "UNITS=INCH"), Body("G0 X1.\nG20"));
    }

    // G17, G18 and G19 are WORKPLANE, written where the source changes the plane (controller-mapping 1).
    [Fact]
    public void Workplane_G18_IsWorkplaneZx()
    {
        Assert.Equal(Lines("RAPID X=1", "WORKPLANE=ZX"), Body("G0 X1.\nG18\nG18"));
    }

    // G54 to G59 are ORIGIN=1 to 6, G54.1 Pn ORIGIN=6+n (language 4.2, controller-mapping 1).
    [Fact]
    public void Origin_G55G59AndG541P2_AreOrigin2And6And8()
    {
        Assert.Equal(Lines("ORIGIN=2", "ORIGIN=6", "ORIGIN=8"), Body("G55\nG59\nG54.1 P2"));
    }

    // / or /n at the block start is SKIP or SKIP=n (controller-mapping 1, language 4.1).
    [Fact]
    public void Skip_SlashTwo_IsSkip2()
    {
        Assert.Equal(Lines("SKIP=2 RAPID X=1"), Body("/2 G0 X1."));
    }

    // A comment on a block is its trailing comment, a comment-only line trivia (controller-mapping 1, D92).
    [Fact]
    public void Comments_OnABlockAndAlone_AreTheBlockCommentAndTrivia()
    {
        Assert.Equal(Lines(Commented("RAPID X=1", "; ONE"), "; TWO"), Body("G0 X1. (ONE)\n(TWO)"));
    }

    // M0 and M1 are STOP=PROGRAM and STOP=OPTIONAL (controller-mapping 1), compared by number (D105).
    [Fact]
    public void Stop_M0AndM01_AreStopProgramAndOptional()
    {
        Assert.Equal(Lines("STOP=PROGRAM", "STOP=OPTIONAL"), Body("M0\nM01"));
    }

    // G4 P dwells P milliseconds, G4 X and G4 U seconds on lathes (controller-mapping 1, DWELL).
    [Fact]
    public void Dwell_G4PAndG4XAndG4U_AreDwellInSeconds()
    {
        Assert.Equal(Lines("DWELL=0.5"), Body("G4 P500"));
        Assert.Equal(Lines("DWELL=2", "DWELL=0.2"), Body("G4 X2\nG4 U0.2", Lathe()));
    }

    // A lathe whose X is programmed in diameters reads with DIAMETER=ON (controller-mapping 1, DIAMETER; D60).
    [Fact]
    public void Header_XInDiameters_HasDiameterOn()
    {
        string text = Text("%\nO0001\nG0 X20.\nM30\n%\n", Lathe());

        Assert.Contains("\nFEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY DIAMETER=ON CYCLE=OFF\nRAPID X=20\n",
            text, StringComparison.Ordinal);
    }

    // Nakamura names the file of the second path O1000.P-2: its program runs on channel 2, CHANNEL=2 on PROGRAM=BEGIN
    // (controller-mapping 7, CHANNEL; language 4.14; controllers fanuc.md 1).
    [Fact]
    public void Channel_NakamuraFileOfPath2_IsChannel2()
    {
        string text = TextOfFile("O1000.P-2", "%\nO1000\nG0 X1.\nM30\n%\n", Lathe());

        Assert.Contains("\nPROGRAM=BEGIN NUMBER=1000 CHANNEL=2\n", text, StringComparison.Ordinal);
        AssertFormatsToItself(text);
    }

    // A file name without the path suffix names no channel; the job says where the program runs (language 4.14).
    [Fact]
    public void Channel_FileWithoutThePathSuffix_WritesNoChannel()
    {
        Assert.Contains("\nPROGRAM=BEGIN NUMBER=1000\n", TextOfFile("O1000", "%\nO1000\nG0 X1.\nM30\n%\n", Lathe()),
            StringComparison.Ordinal);
        Assert.Contains("\nPROGRAM=BEGIN NUMBER=1000\n", TextOfFile("O1000.P-2", "%\nO1000\nG0 X1.\nM30\n%\n"),
            StringComparison.Ordinal);
    }

    // The reader keeps what it cannot express as RAW and its output formats to itself (D5, D91).
    [Fact]
    public void Output_WithRawAndComments_FormatsToItself()
    {
        NcxProgram program = FramedProgram("G0 X1. (A)\nG64\n(B)\nG1 X2. F100");

        AssertFormatsToItself(Core.Writing.NcxWriter.Write(program));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }
}
