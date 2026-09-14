using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The program units and archives, the header, the ends, the labels, the skip levels, the comments and the M functions
/// (controllers siemens.md 1, 8, 11 rules 1 and 6; controller-mapping 1, 6; language 4.13; D34).
/// </summary>
public sealed class SiemensProgramTests
{
    private const string Header = "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF";

    // Rule 1: every %_N_NAME_MPF is a PROGRAM with the complete header (D34), every %_N_NAME_SPF and PROC unit a SUB,
    // ;$PATH lines are header comments; an _INI unit is RAW blocks of a section of its own, NAME_INI, without the
    // WARNING of a program without M30.
    [Fact]
    public void Units_OfAnArchive_BecomeProgramSubAndRawSection()
    {
        NcxProgram program = Program("%_N_MAIN_MPF\n;$PATH=/_N_WKS_DIR/_N_TEST_WPD\nG0 X0 Y0 Z10\nPOCK\nM30\n"
            + "%_N_POCK_SPF\n;$PATH=/_N_WKS_DIR/_N_TEST_WPD\nPROC POCK\nG1 Z-2 F100\nRET\n%_N_MAIN_INI\nR1=5\n");

        Assert.Equal(Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"MAIN\"", Header, "; $PATH=/_N_WKS_DIR/_N_TEST_WPD",
                "RAPID X=0 Y=0 Z=10", "CALL=POCK", "PROGRAM=END", "SUB=BEGIN NAME=POCK",
                "; $PATH=/_N_WKS_DIR/_N_TEST_WPD", "LINE Z=-2 F=100", "SUB=END", "PROGRAM=BEGIN NAME=\"MAIN_INI\"",
                Raw("%_N_MAIN_INI"), Raw("R1=5"), "PROGRAM=END", "FILE=END"),
            NcxWriter.Write(program));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // The INDEX archive of the corpus holds programs and a subprogram in one file, %_N_1_0_MPF, %_N_TH1_HS_01_SPF and
    // %_N_1_3_MPF (siemens 1; the acceptance of P5-01): one NCX file with a section per unit.
    [Fact]
    public void IndexArchive_ReadsAsOneFileWithASectionPerUnit()
    {
        string text = Text("%_N_1_0_MPF\nG0 X0\nTH1_HS_01\nM30\n%_N_TH1_HS_01_SPF\nG0 Z10\nM17\n%_N_1_3_MPF\nG0 X5\n"
            + "M30\n");

        Assert.Equal(Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"1_0\"", Header, "RAPID X=0", "CALL=TH1_HS_01",
                "PROGRAM=END", "SUB=BEGIN NAME=TH1_HS_01", "RAPID Z=10", "SUB=END", "PROGRAM=BEGIN NAME=\"1_3\"",
                Header, "RAPID X=5", "PROGRAM=END", "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // Rule 6: M2 ends the program like M30 (controller-mapping 1, PROGRAM=END); a file without a unit header is one
    // program without a name.
    [Fact]
    public void M2_AndAFileWithoutAHeader_AreProgramsWithTheirEnd()
    {
        Assert.Equal(Lines("RAPID X=0"), BodyOf(Text("%_N_TEST_MPF\nG0 X0\nM2\n")));
        Assert.Equal(Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN", Header, "RAPID X=0", "PROGRAM=END", "FILE=END"),
            Text("N10 G0 X0\nN20 M30\n"));
    }

    // A label no jump enters is a LABEL of its own, as the restart labels of the posts are (controller-mapping 6,
    // LABEL; machine-builders 2).
    [Fact]
    public void Labels_NoJumpEnters_AreLabelsOfTheirOwn()
    {
        Assert.Equal(Lines("LABEL=WERKZEUG_1", "RAPID X=0", "LABEL=WZ2", "RAPID X=1"),
            CheckedBody("WERKZEUG_1:\nG0 X0\nN20 WZ2: G0 X1"));
    }

    // / is SKIP and /n SKIP=n (siemens 1; controller-mapping 1, SKIP).
    [Fact]
    public void SkipLevels_AreSkip()
    {
        Assert.Equal(Lines("SKIP RAPID X=1", "SKIP=3 RAPID X=2"), CheckedBody("/G0 X1\n/3 N10 G0 X2"));
    }

    // The ; comment of a block is its comment, a comment line is trivia (siemens 1; language 5 rule 7).
    [Fact]
    public void Comments_OnABlockAndAlone_AreTheBlockCommentAndTrivia()
    {
        Assert.Equal(Lines(Commented("RAPID X=3", "; APPROACH"), "; OPERATION 2"),
            CheckedBody("G0 X3 ; APPROACH\n; OPERATION 2"));
    }

    // An M function no table names is MFUNC with a WARNING (machine-config 5; language 4.6; D40, D66).
    [Fact]
    public void MFunction_NoTableNames_IsMfuncWithAWarning()
    {
        NcxProgram program = FramedProgram("M123\nG0 X1 M124");

        Assert.Equal(Lines("MFUNC=123", "RAPID X=1 MFUNC=124"), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensMCodeNotNamed, DiagnosticCodes.SiemensMCodeNotNamed], Codes(program));
    }
}
