using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// Comment-only lines and blank lines are trivia: not blocks, kept with their line numbers and their text as read,
/// also before FILE=BEGIN and after FILE=END; a comment behind words is the comment of its block (language 3, Block
/// and Comment; D92).
/// </summary>
public sealed class TriviaTests
{
    // Written with explicit line breaks, so that the blank line of a tab and the blank last line are what they are.
    private const string FramedFile =
        "; before the frame\n"
        + "FILE=BEGIN NCX=1\n"
        + "\n"
        + "PROGRAM=BEGIN NAME=\"P\"                              ; the header\n"
        + "  ; indented comment line\n"
        + "RAPID X=1\n"
        + "\t\n"
        + "PROGRAM=END\n"
        + "FILE=END\n"
        + "; after the frame\n"
        + "\n";

    // Comment lines before and after the file frame and blank lines are collected with their line numbers and their
    // text as read, whitespace included (D92).
    [Fact]
    public void D92_CommentAndBlankLines_AreTriviaWithTheirLineNumbers()
    {
        NcxProgram program = ParseText.File(FramedFile);

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        var expected = new List<Trivia>
        {
            new(1, "; before the frame"),
            new(3, ""),
            new(5, "  ; indented comment line"),
            new(7, "\t"),
            new(10, "; after the frame"),
            new(11, ""),
        };
        Assert.Equal(expected, program.Trivia);
    }

    // Trivia is not a block: the blocks are the lines that hold words (language 3, Block).
    [Fact]
    public void D92_Trivia_IsNoBlock()
    {
        NcxProgram program = ParseText.File(FramedFile);

        var lines = new List<int>();
        foreach (Block block in program.Blocks)
        {
            lines.Add(block.Line);
        }

        Assert.Equal([2, 4, 6, 8, 9], lines);
    }

    // A comment behind the words is the block's comment, from the semicolon to the end of the line, as read (D92).
    [Fact]
    public void D92_TrailingComment_IsOnTheBlock()
    {
        NcxProgram program = ParseText.File(FramedFile);

        Assert.Equal("; the header", program.Blocks[1].Comment);
        Assert.Null(program.Blocks[2].Comment);
    }

    // A block keeps the line it was read from, as read (architecture 4.1).
    [Fact]
    public void KeepSourceText_Default_KeepsTheLineAsRead()
    {
        NcxProgram program = ParseText.File(FramedFile);

        Assert.Equal(
            "PROGRAM=BEGIN NAME=\"P\"                              ; the header", program.Blocks[1].SourceText);
    }

    // Without KeepSourceText a block without an ERROR keeps no source text; a block with an ERROR keeps it all the
    // same (architecture 4.1).
    [Fact]
    public void KeepSourceText_Off_KeepsOnlyTheTextOfABlockWithAnError()
    {
        var options = new ParserOptions { KeepSourceText = false };
        var diagnostics = new Diagnostics("test.ncx");

        Block? clean = Parser.ParseBlock("RAPID X=1", 1, options, diagnostics);
        Block? wrong = Parser.ParseBlock("RAPID FOO=1", 2, options, diagnostics);

        Assert.Null(clean?.SourceText);
        Assert.Equal("RAPID FOO=1", wrong?.SourceText);
    }

    // A line that holds no word is no block (language 3, Block).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("; only a comment")]
    [InlineData("   ; indented")]
    public void ParseBlock_TriviaLine_IsNoBlock(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");

        Assert.Null(ParseText.Block(text, diagnostics));
        Assert.Empty(diagnostics.Items);
    }
}
