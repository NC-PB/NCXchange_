using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Fakes.FakeRead;

namespace Ncx.Readers.Tests;

/// <summary>
/// Source NCX cannot express is kept as RAW:controller or RAW:builder with its text verbatim and a WARNING per block;
/// nothing is dropped (D5; language 4.1; controller-mapping 9; machine-config 5, [raw]).
/// </summary>
public sealed class RawTests
{
    // Done when of P3-01: RAW blocks survive a format round trip. The text is kept verbatim, comment and quotes
    // included; the builder code of the [raw] table carries the builder's name (D5, language 4.1, machine-config 5).
    [Fact]
    public void RawBlocks_ReadAndFormatted_SurviveTheRoundTrip()
    {
        NcxProgram program = Program(Lines(
            "%", "O0001", "G10 L2 P1 Z-175.0 (G54 \"O/L\")", "G0411 F12.", "M30", "%"));
        string text = NcxWriter.Write(program);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                """RAW:FANUC="G10 L2 P1 Z-175.0 (G54 \"O/L\")" """.TrimEnd(),
                """RAW:NAKAMURA="G0411 F12." """.TrimEnd(),
                "PROGRAM=END",
                "FILE=END"),
            text);
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
        Assert.All(program.Diagnostics.Items, warning => Assert.Equal(Severity.Warning, warning.Severity));
        AssertFormatsToItself(text);
    }

    // Each RAW is a WARNING at read time that names the dialect and the rule (controller-mapping 9, D98).
    [Fact]
    public void RawWarning_OnTheLineOfTheBlock_NamesTheDialectAndD5()
    {
        NcxProgram program = Program(Lines("%", "O0001", "G10 L2 P1", "M30", "%"));

        Diagnostic warning = Assert.Single(program.Diagnostics.Items);
        Assert.Equal(3, warning.Line);
        Assert.Contains("RAW:FANUC", warning.Message, StringComparison.Ordinal);
        Assert.Contains("D5", warning.Message, StringComparison.Ordinal);
    }

    // A block the controller continues over several lines is one RAW block per line, since an NCX string holds no
    // line break, and one WARNING (language 3, string; D5).
    [Fact]
    public void Emit_BlockWithContinuationLines_WritesOneRawBlockPerLine()
    {
        var diagnostics = new Diagnostics("fake.h");
        var builder = new NcxBuilder(diagnostics);
        var block = new SourceBlock
        {
            Line = 5,
            Text = "1 CYCL DEF 247 INIT. REF.PKT ~",
            Words = [new SourceWord { Address = "CYCL" }],
            Continuation = ["    Q339=1 ;REF.PUNKTNUMMER"],
        };

        RawEmitter.Emit(builder, block, "HEIDENHAIN", "cycle 247 is read elsewhere", diagnostics);
        NcxProgram program = builder.Build();

        Assert.Equal("""RAW:HEIDENHAIN="1 CYCL DEF 247 INIT. REF.PKT ~" """.TrimEnd(),
            NcxWriter.WriteBlock(program.Blocks[0]));
        Assert.Equal("""RAW:HEIDENHAIN="    Q339=1 ;REF.PUNKTNUMMER" """.TrimEnd(),
            NcxWriter.WriteBlock(program.Blocks[1]));
        Assert.Equal([5, 6], [program.Blocks[0].Line, program.Blocks[1].Line]);
    }

    // The builder's name is the dialect only for a code of the [raw] table, compared by number, on a machine that
    // names a builder; any other block is RAW of the controller family (machine-config 1 and 5, D105).
    [Fact]
    public void DialectOf_BuilderCodeAndOtherCode_AreBuilderAndController()
    {
        var builderCode = new SourceBlock
        {
            Line = 1,
            Text = "G0411",
            Words = [new SourceWord { Address = "G", Text = "0411", Number = 411 }],
        };
        var otherCode = builderCode with { Words = [new SourceWord { Address = "G", Text = "10", Number = 10 }] };

        Assert.Equal("NAKAMURA", RawEmitter.DialectOf(builderCode, Machine(), Core.Machine.Controller.Fanuc));
        Assert.Equal("FANUC", RawEmitter.DialectOf(otherCode, Machine(), Core.Machine.Controller.Fanuc));
    }
}
