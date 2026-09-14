using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// What stays RAW (controllers siemens.md 11 rule 8; controller-mapping 9; D5): MSG, STOPRE, WORKPIECE, DEF of other
/// types, the synchronized actions, the interrupts, the orientation and path-control words, the builder cycles the
/// configuration does not name and the recipe block of the STAMA post, each with a WARNING and kept in place; the
/// words of a block that NCX has no word for as RAW words of that block.
/// </summary>
public sealed class SiemensRawTests
{
    // Rule 8: the statements without an NCX word are RAW blocks in place, each with a WARNING.
    [Fact]
    public void Statements_WithoutAnNcxWord_AreRawBlocksWithAWarning()
    {
        string[] statements =
        [
            "MSG(\"HELLO\")", "STOPRE", "WORKPIECE(,\"\",,\"BOX\",112,0,-50,-80,0,0,100,100)",
            "DEF STRING[20] TXT=\"A\"", "WHEN $AA_IM[X]>10 DO M120", "SETINT(3) PRIO=1 NOTFALL", "ORIWKS", "COMPCAD",
        ];
        NcxProgram program = FramedProgram(string.Join('\n', statements));

        Assert.Equal(Lines([.. statements.Select(Raw)]), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([.. statements.Select(_ => DiagnosticCodes.KeptAsRaw)], Codes(program));
    }

    // Rule 8: the path-control and feed words of a motion block are RAW words of that block (siemens 2).
    [Fact]
    public void PathControlWords_OfAMotionBlock_AreRawWordsOfTheBlock()
    {
        NcxProgram program = FramedProgram("G1 X10 F100\nG64 ADIS=0.1 G1 X30\nFZ=0.1 G1 X40");

        Assert.Equal(Lines("LINE X=10 F=100", "LINE X=30 RAW:SIEMENS=\"G64 ADIS=0.1\"",
                "LINE X=40 RAW:SIEMENS=\"FZ=0.1\""),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensWordsKeptAsRaw, DiagnosticCodes.SiemensWordsKeptAsRaw], Codes(program));
    }

    // Rule 8: a builder cycle the configuration does not name stays RAW with a WARNING (controller-mapping 9).
    [Fact]
    public void BuilderCycle_TheConfigurationDoesNotName_IsRaw()
    {
        NcxProgram program = FramedProgram("CYCLE_HERMLE(1,2)");

        Assert.Equal(Lines(Raw("CYCLE_HERMLE(1,2)")), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // The recipe block of the STAMA post between its markers is no NC syntax and stays RAW (machine-builders 2, STAMA;
    // controller-mapping 9).
    [Fact]
    public void RecipeBlock_OfTheStamaPost_IsRaw()
    {
        Assert.Equal(Lines(Raw("<PROG_BEGIN_C1>"), Raw("GREZ_1=5"), Raw("<PROG_END_PAR>"), "RAPID X=0"),
            Body("<PROG_BEGIN_C1>\nGREZ_1=5\n<PROG_END_PAR>\nG0 X0"));
    }

    // Under ORIMKS a tool vector means the machine axes, while TX TY TZ are meant in the workpiece frame: the vector is
    // written with a WARNING (controller-mapping 2).
    [Fact]
    public void ToolVector_UnderOrimks_IsWrittenWithAWarning()
    {
        NcxProgram program = FramedProgram("ORIMKS\nTRAORI\nG1 X1 Y1 Z1 A3=0 B3=0 C3=1 F100");

        Assert.Equal(Lines(Raw("ORIMKS"), "TCPM=ON", "LINE X=1 Y=1 Z=1 TX=0 TY=0 TZ=1 F=100"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.SiemensVectorInMachineSystem], Codes(program));
    }

    // A program with RAW blocks, RAW words and comments formats to itself, what ncx format does to the output of ncx
    // convert (D91).
    [Fact]
    public void Output_WithRawAndComments_FormatsToItself()
    {
        AssertFormatsToItself(Text("%_N_TEST_MPF\nMSG(\"A;B\") ; NOTE\nG1 X1 F100 FZ=0.1\n"
            + "WORKPIECE(,\"\",,\"BOX\",112)\nM30\n"));
    }
}
