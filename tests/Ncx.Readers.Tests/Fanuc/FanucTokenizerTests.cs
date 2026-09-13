using Ncx.Readers.Fanuc;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The tokenizer of Fanuc and ISO programs (controllers fanuc.md 1, 2, 7; phase 3, P3-02): %, O, N, comments anywhere,
/// / and /n, addresses with signed numbers with a leading or a trailing dot, #n variables, [ ] expressions, the words
/// of custom macro B, several G and M per block, CR LF and LF.
/// </summary>
public sealed class FanucTokenizerTests
{
    // X70. and I-.534 are normal output of CAM systems and are taken as written (controllers fanuc.md 2).
    [Fact]
    public void Numbers_LeadingAndTrailingDots_AreNumbersAsWritten()
    {
        SourceBlock block = Single("N10 G1 X70. Y-.534 Z+5");

        Assert.Equal(["N", "G", "X", "Y", "Z"], Addresses(block));
        Assert.Equal("70.", block.Find("X")!.Text);
        Assert.Equal(70m, block.Find("X")!.Number);
        Assert.Equal(-0.534m, block.Find("Y")!.Number);
        Assert.Equal(5m, block.Find("Z")!.Number);
    }

    // Words need no blanks between them: N100M428, G0G40G99Z0.05 (the Nakamura program).
    [Fact]
    public void Words_WithoutBlanks_AreSplitAtEveryAddress()
    {
        SourceBlock block = Single("G0G40G99Z0.05");

        Assert.Equal(["G", "G", "G", "Z"], Addresses(block));
        Assert.Equal(0.05m, block.Find("Z")!.Number);
    }

    // ( ... ) is a comment, anywhere in the block (controllers fanuc.md 1); the comments of a block are one comment.
    [Fact]
    public void Comments_AnywhereInTheBlock_AreTheCommentOfTheBlock()
    {
        SourceBlock block = Single("G0 (FIRST) X1. (SECOND)");

        Assert.Equal(["G", "X"], Addresses(block));
        Assert.Equal("FIRST SECOND", block.Comment);
    }

    // / or /n at the block start is the optional block skip (controllers fanuc.md 1).
    [Fact]
    public void BlockSkip_SlashAndSlashN_AreTheSkipAndItsSwitch()
    {
        SourceBlock bare = Single("/M30");
        SourceBlock numbered = Single("/2 M30");

        Assert.True(bare.BlockSkip);
        Assert.Null(bare.SkipSwitch);
        Assert.Equal(2, numbered.SkipSwitch);
        Assert.Equal(["M"], Addresses(numbered));
    }

    // #n variables and [ ] expressions are values of an address (controllers fanuc.md 7).
    [Fact]
    public void Values_VariablesAndBrackets_AreExpressions()
    {
        SourceBlock block = Single("G01G98B-[#502+#11099]F4800 G53X#528");

        Assert.Equal("-[#502+#11099]", block.Find("B")!.Expression);
        Assert.Equal("#528", block.Find("X")!.Expression);
        Assert.Null(block.Find("X")!.Number);
    }

    // #n = ... assigns; the word keeps the variable as its text and the right side as its expression.
    [Fact]
    public void Assignment_HashEquals_KeepsTheVariableAndTheRightSide()
    {
        SourceBlock block = Single("#528=0(WZW. *X*)");

        SourceWord assignment = Assert.Single(block.Words);
        Assert.Equal("#", assignment.Address);
        Assert.Equal("528", assignment.Text);
        Assert.Equal("0", assignment.Expression);
        Assert.Equal("WZW. *X*", block.Comment);
    }

    // IF, GOTO, WHILE, DO, END and THEN are words of their own (controllers fanuc.md 7).
    [Fact]
    public void MacroWords_IfGoto_AreWordsOfTheirOwn()
    {
        SourceBlock block = Single("IF[#503EQ0]GOTO9090");

        Assert.Equal(["IF", "GOTO"], Addresses(block));
        Assert.Equal("[#503EQ0]", block.Find("IF")!.Expression);
        Assert.Equal(9090m, block.Find("GOTO")!.Number);
    }

    // The 30i writes extended axis names with an equals sign, C2=180. (D30).
    [Fact]
    public void ExtendedAxisName_WithItsEqualsSign_IsTheAddressOfTheAxis()
    {
        SourceBlock block = Single("G0 C2=180.");

        Assert.Equal(["G", "C2"], Addresses(block));
        Assert.Equal(180m, block.Find("C2")!.Number);
    }

    // ,C2 and ,R4 attached to a motion block are the chamfer and the rounding (controllers fanuc.md 4).
    [Fact]
    public void CornerWords_CommaCAndCommaR_AreWordsOfTheirOwn()
    {
        SourceBlock block = Single("G1 X10. ,C2.");

        Assert.Equal(["G", "X", ",C"], Addresses(block));
        Assert.Equal(2m, block.Find(",C")!.Number);
    }

    // CR LF and LF give the same blocks, and a blank or comment-only line is a block without words (D92).
    [Fact]
    public void LineEndings_CrLfAndLf_GiveTheSameBlocks()
    {
        List<SourceBlock> crLf = [.. new FanucTokenizer().Tokenize("%\r\nO0001 (A)\r\n\r\n(B)\r\nM30\r\n%\r\n")];
        List<SourceBlock> lf = [.. new FanucTokenizer().Tokenize("%\nO0001 (A)\n\n(B)\nM30\n%\n")];

        Assert.Equal(6, crLf.Count);
        Assert.Equal(lf.Count, crLf.Count);
        Assert.True(crLf[2].IsTrivia);
        Assert.True(crLf[3].IsTrivia);
        Assert.Equal("B", crLf[3].Comment);
        Assert.Equal("O0001 (A)", crLf[1].Text);
    }

    private static SourceBlock Single(string line)
    {
        return Assert.Single(new FanucTokenizer().Tokenize(line));
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
