using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The five example programs of the specification are the acceptance test of the parser: they parse without a
/// diagnostic, and every line of them is a block or trivia (P0-04, Done when).
/// </summary>
public sealed class ExampleParsingTests
{
    public static TheoryData<string> Examples()
    {
        return new TheoryData<string>
        {
            "2.5D_FRAESEN.ncx",
            "INCREMENTAL_SUB.ncx",
            "MILLTURN_TRANSFER.ncx",
            "PATTERN_LOOP.ncx",
            "POLAR_FACE.ncx",
        };
    }

    // The five examples parse with zero diagnostics (P0-04, Done when).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_Parsed_HasNoDiagnostics(string example)
    {
        NcxProgram program = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
    }

    // One line is one block; blank and comment-only lines are trivia; so every line of the file is exactly one of the
    // two, which is what lets the writer put the file back together by line number (language 3, Block; D92).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_EveryLine_IsOneBlockOrOneTrivia(string example)
    {
        string text = Fixture.ReadText(example);
        NcxProgram program = Parser.Parse(text, example, new ParserOptions());

        var lines = new List<int>();
        foreach (Block block in program.Blocks)
        {
            lines.Add(block.Line);
        }

        foreach (Trivia trivia in program.Trivia)
        {
            lines.Add(trivia.Line);
        }

        lines.Sort();
        int lineCount = text.Split('\n').Length - (text.EndsWith('\n') ? 1 : 0);
        Assert.Equal(Enumerable.Range(1, lineCount), lines);
    }

    // The programs and subprograms of the examples, with the file frame around them (language 4.1, 4.13).
    [Theory]
    [InlineData("2.5D_FRAESEN.ncx", 1, 0)]
    [InlineData("INCREMENTAL_SUB.ncx", 1, 1)]
    [InlineData("MILLTURN_TRANSFER.ncx", 1, 0)]
    [InlineData("PATTERN_LOOP.ncx", 1, 0)]
    [InlineData("POLAR_FACE.ncx", 1, 0)]
    public void Example_Structure_HasItsProgramsAndSubprograms(string example, int programs, int subs)
    {
        NcxProgram program = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());

        Assert.Equal(programs, program.Programs.Count);
        Assert.Equal(subs, program.Subs.Count);
        Assert.Same(program.Blocks[0], program.FileBegin);
        Assert.Same(program.Blocks[^1], program.FileEnd);
        Assert.Equal(LineEnding.Lf, program.LineEnding);
    }

    // INCREMENTAL_SUB: the program from PROGRAM=BEGIN on line 4 to PROGRAM=END on line 27, the subprogram 100 from
    // line 29 to SUB=END on line 34 (language 4.13).
    [Fact]
    public void IncrementalSub_Sections_SpanTheirBlocks()
    {
        NcxProgram program = Parser.Parse(
            Fixture.ReadText("INCREMENTAL_SUB.ncx"), "INCREMENTAL_SUB.ncx", new ParserOptions());

        Section main = Assert.Single(program.Programs);
        Assert.Equal("SLOT_ROW", main.Name);
        Assert.Equal(3, main.Number);
        Assert.Equal(4, program.Blocks[main.FirstBlock].Line);
        Assert.Equal(27, program.Blocks[main.LastBlock].Line);

        Section sub = Assert.Single(program.Subs);
        Assert.Equal("100", sub.Name);
        Assert.Equal(29, program.Blocks[sub.FirstBlock].Line);
        Assert.Equal(34, program.Blocks[sub.LastBlock].Line);
    }
}
