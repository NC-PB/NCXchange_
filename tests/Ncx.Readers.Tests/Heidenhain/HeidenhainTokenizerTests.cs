using Ncx.Readers.Heidenhain;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The Klartext syntax (controllers heidenhain.md 1, 7 rule 8): block numbers, ~ continuations, ; comments, * -
/// sections, the / skip, the comma and the dot, the formulas and the FN functions.
/// </summary>
public sealed class HeidenhainTokenizerTests
{
    // The editor numbers every block; the number is part of the text and no word (heidenhain 1).
    [Fact]
    public void BlockNumber_InFrontOfTheBlock_IsNoWord()
    {
        SourceBlock block = Only("5 L X+10 Y-5 R0 FMAX");

        Assert.Equal(["L", "X", "Y", "R", "FMAX"], Addresses(block));
        Assert.Equal(10m, block.Words[1].Number);
        Assert.Equal(-5m, block.Words[2].Number);
        Assert.Equal("5 L X+10 Y-5 R0 FMAX", block.Text);
    }

    // ~ at the end of a line continues the block on the next line (heidenhain 1).
    [Fact]
    public void Tilde_AtTheEndOfALine_ContinuesTheBlockOnTheNextLine()
    {
        List<SourceBlock> blocks = Tokenize("1 CYCL DEF 247 INIT. REF.PKT ~\r\n    Q339=1 ;REF.PUNKTNUMMER\r\n2 M3\r\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal(["    Q339=1 ;REF.PUNKTNUMMER"], blocks[0].Continuation);
        Assert.Equal(1m, blocks[0].Find("Q339")!.Number);
        Assert.Equal("REF.PUNKTNUMMER", blocks[0].Comment);
        Assert.Equal(3, blocks[1].Line);
    }

    // The comma is the decimal separator, the reader accepts the dot too (heidenhain 7 rule 8).
    [Fact]
    public void Comma_AndDot_AreBothDecimalSeparators()
    {
        SourceBlock block = Only("9 L X50,4 Y-7.025 FMAX");

        Assert.Equal(50.4m, block.Words[1].Number);
        Assert.Equal(-7.025m, block.Words[2].Number);
    }

    // ; starts a comment at the end of a block; a line with only a comment is trivia (heidenhain 1; D92).
    [Fact]
    public void Semicolon_StartsTheComment_AndACommentLineIsTrivia()
    {
        List<SourceBlock> blocks = Tokenize("7 M3 ;SPINDEL EIN\n8 ;NUR KOMMENTAR\n");

        Assert.Equal("SPINDEL EIN", blocks[0].Comment);
        Assert.True(blocks[1].IsTrivia);
        Assert.Equal("NUR KOMMENTAR", blocks[1].Comment);
    }

    // * - title is a structuring block; its whole text is the title (heidenhain 1).
    [Fact]
    public void StarDash_IsOneSectionWord()
    {
        SourceBlock block = Only("8 * -   UMFAHREN ; NICHT KOMMENTAR");

        SourceWord title = Assert.Single(block.Words);
        Assert.Equal("*", title.Address);
        Assert.Equal("UMFAHREN ; NICHT KOMMENTAR", title.Text);
    }

    // / at the block start is the optional skip (heidenhain 1).
    [Fact]
    public void Slash_AfterTheBlockNumber_IsTheBlockSkip()
    {
        SourceBlock block = Only("12 /L X+10 FMAX");

        Assert.True(block.BlockSkip);
        Assert.Equal("L", block.Words[0].Address);
    }

    // A formula assigns a Q parameter its right side, one word with the expression (heidenhain 6).
    [Fact]
    public void Formula_IsOneAssignmentWord()
    {
        SourceBlock block = Only("3 Q1 = Q2 + 3 * SIN Q3");

        SourceWord assignment = Assert.Single(block.Words);
        Assert.Equal("Q1", assignment.Address);
        Assert.Equal("Q2 + 3 * SIN Q3", assignment.Expression);
    }

    // FN n: is the number of the function, then its assignment or its words (heidenhain 6).
    [Fact]
    public void FnFunction_KeepsItsNumberAndItsAssignment()
    {
        SourceBlock assign = Only("4 FN 1: Q1 = +Q2 + +5");
        SourceBlock jump = Only("5 FN 9: IF +Q1 EQU +Q3 GOTO LBL 5");

        Assert.Equal(["FN", "Q1"], Addresses(assign));
        Assert.Equal(1m, assign.Words[0].Number);
        Assert.Equal("+Q2 + +5", assign.Words[1].Text);
        Assert.Equal(["FN", "IF", "", "EQU", "", "GOTO", "LBL", ""], Addresses(jump));
    }

    // A quoted name is one word (heidenhain 1, LBL "NAME").
    [Fact]
    public void QuotedName_IsOneWord()
    {
        SourceBlock block = Only("6 LBL \"DRILL ROW\"");

        Assert.Equal("\"DRILL ROW\"", block.Words[1].Text);
    }

    private static SourceBlock Only(string line)
    {
        return Assert.Single(Tokenize(line + "\n"));
    }

    private static List<SourceBlock> Tokenize(string text)
    {
        return [.. new HeidenhainTokenizer().Tokenize(text)];
    }

    private static List<string> Addresses(SourceBlock block)
    {
        var addresses = new List<string>();
        foreach (SourceWord word in block.Words)
        {
            addresses.Add(word.Address);
        }

        return addresses;
    }
}
