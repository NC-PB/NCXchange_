using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The lines of an output file per [format] (machine-config 2): block_numbers, max_line_length, line_ending.
/// </summary>
public sealed class OutputBufferTests
{
    private static readonly Block s_block = new() { Line = 12, Words = [] };

    // Machine-config 2: block_numbers numbers the lines that take a number from start in steps of step, with the
    // prefix of the controller; line_ending ends every line.
    [Fact]
    public void ToText_BlockNumbersAndCrLf_NumbersTheLinesThatTakeOne()
    {
        var format = new OutputFormat
        {
            BlockNumbers = new BlockNumbering { Enabled = true, Start = 5, Step = 5 },
            LineEnding = LineEnding.CrLf,
        };
        var output = new OutputBuffer(format, "N", line => !line.StartsWith('%'));
        output.Line("%", s_block);
        output.Line("G0 X1.", s_block);
        output.Line("X2.", s_block);
        output.Line("%", s_block);

        Assert.Equal("%\r\nN5 G0 X1.\r\nN10 X2.\r\n%\r\n", output.ToText(new Diagnostics("T.ncx")));
    }

    // Machine-config 2, controllers heidenhain.md 8 rule 1: Heidenhain numbers its blocks from 0 without a letter.
    [Fact]
    public void ToText_NumbersFromZeroWithoutPrefix_WritesTheKlartextNumbers()
    {
        var format = new OutputFormat { BlockNumbers = new BlockNumbering { Enabled = true, Start = 0, Step = 1 } };
        var output = new OutputBuffer(format, "", _ => true);
        output.Line("BEGIN PGM T MM", s_block);
        output.Line("L Z+2 FMAX", s_block);

        Assert.Equal("0 BEGIN PGM T MM\n1 L Z+2 FMAX\n", output.ToText(new Diagnostics("T.ncx")));
    }

    // Machine-config 2 and the TODO(question) of OutputBuffer: without block_numbers no line is numbered, and without
    // line_ending the lines end with LF.
    [Fact]
    public void ToText_WithoutBlockNumbersAndLineEnding_WritesTheLinesAsTheyAre()
    {
        var output = new OutputBuffer(new OutputFormat(), "N", _ => true);
        output.Line("G0 X1.", s_block);

        Assert.Equal("G0 X1.\n", output.ToText(new Diagnostics("T.ncx")));
    }

    // Machine-config 2 and the TODO(question) of OutputBuffer: a line longer than max_line_length, its block number
    // counted, is kept with a WARNING on its block.
    [Fact]
    public void ToText_LineLongerThanMaxLineLength_IsTheWarningCmp021()
    {
        var format = new OutputFormat
        {
            BlockNumbers = new BlockNumbering { Enabled = true, Start = 10, Step = 10 },
            MaxLineLength = 10,
        };
        var output = new OutputBuffer(format, "N", _ => true);
        var diagnostics = new Diagnostics("T.ncx");
        output.Line("G0 X1.", s_block);
        output.Line("G0 X1. Y2.", s_block);

        string text = output.ToText(diagnostics);

        Assert.Equal("N10 G0 X1.\nN20 G0 X1. Y2.\n", text);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.LineLongerThanTheMachineTakes, warning.Code);
        Assert.Equal(12, warning.Line);
    }
}
