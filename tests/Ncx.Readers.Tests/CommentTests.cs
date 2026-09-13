using static Ncx.Readers.Tests.Fakes.FakeRead;

namespace Ncx.Readers.Tests;

/// <summary>
/// Comments of the source: a comment on a block becomes the block's comment, a comment-only line trivia, ( name )
/// after O the NAME, a structuring comment SECTION (controller-mapping 1; D5, D92).
/// </summary>
public sealed class CommentTests
{
    [Fact]
    public void Comments_OfTheSource_AreKeptWhereTheyStand()
    {
        string text = Text(Lines(
            "%",
            "O0001 (2.5D FRAESEN)",
            "(SIDE MILL D10)",
            "G0 X0 (FIRST)",
            "G90 (ABSOLUTE)",
            "",
            "* - ROUGHING",
            "M30 (END)",
            "%"));

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NAME=\"2.5D FRAESEN\" NUMBER=1",
                "; SIDE MILL D10",
                Commented("RAPID X=0", "; FIRST"),
                "; ABSOLUTE",
                "",
                "SECTION=\"ROUGHING\"",
                Commented("PROGRAM=END", "; END"),
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // ( name ) after O becomes NAME and is not written a second time as a comment (controller-mapping 1).
    [Fact]
    public void ProgramName_CommentAfterO_BecomesNameOnly()
    {
        string text = Text(Lines("O0001 (BAR)", "M30"));

        Assert.Contains("\nPROGRAM=BEGIN NAME=\"BAR\" NUMBER=1\nPROGRAM=END\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("; BAR", text, StringComparison.Ordinal);
    }

    // Blank lines of the source stay blank lines of the program (D92).
    [Fact]
    public void Trivia_BlankLinesBetweenBlocks_StayInPlace()
    {
        string text = Text(Lines("%", "O0001", "G0 X0", "", "", "G0 X1", "M30", "%"));

        Assert.Contains("\nRAPID X=0\n\n\nRAPID X=1\n", text, StringComparison.Ordinal);
    }
}
