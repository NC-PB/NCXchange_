using Ncx.Readers.Siemens;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The SINUMERIK syntax (controllers siemens.md 1, 8, 11 rule 9): unit headers, N numbers, labels, ; comments, the /
/// and /n skip levels, the addresses with = and extensions, expressions as values, strings, call argument lists with
/// empty positions, the statements of the high-level language, DEFINE macros and the recipe block of the STAMA post.
/// </summary>
public sealed class SiemensTokenizerTests
{
    // An address with its number: X10, X-10.5, F.08, G0, M30 (siemens 1).
    [Fact]
    public void Word_AddressWithItsNumber_KeepsTheNumberAsWritten()
    {
        SourceBlock block = Only("N10 G0 X-10.5 F.08 M30");

        Assert.Equal(["N", "G", "X", "F", "M"], Addresses(block));
        Assert.Equal(-10.5m, block.Words[2].Number);
        Assert.Equal(".08", block.Words[3].Text);
        Assert.Equal(0.08m, block.Words[3].Number);
    }

    // The = is required after every multi-letter address and after an address with a numeric extension: CR=5, S2=1000,
    // M2=3, X1=10 (siemens 1).
    [Fact]
    public void EqualsSign_AfterAMultiLetterAddressOrAnExtension_GivesTheValue()
    {
        SourceBlock block = Only("CR=5 S2=1000 M2=3 X1=10");

        Assert.Equal(["CR", "S2", "M2", "X1"], Addresses(block));
        Assert.Equal([5m, 1000m, 3m, 10m], [.. block.Words.Select(word => word.Number!.Value)]);
    }

    // X=R1*2 and Z=R40 carry an expression as their value (siemens 1, 11 rule 9).
    [Fact]
    public void Value_AnExpression_IsKeptAsTheExpression()
    {
        SourceBlock block = Only("G1 X=R1*2 Z = R40");

        Assert.Equal("R1*2", block.Words[1].Expression);
        Assert.Equal("R40", block.Words[2].Expression);
        Assert.Null(block.Words[1].Number);
    }

    // A call keeps its argument list with its empty positions, a blank allowed before it: MCALL CYCLE81 (3,1,2,-50,)
    // of the corpus (siemens 7).
    [Fact]
    public void Call_WithEmptyPositions_KeepsItsArgumentList()
    {
        SourceBlock block = Only("MCALL CYCLE81 (3,1,2,-50,)");

        Assert.Equal(["MCALL", "CYCLE81"], Addresses(block));
        Assert.Equal(["3", "1", "2", "-50", ""], SiemensArguments.Split(block.Words[1].Text));
    }

    // A label NAME: stands at the block start, after the N number (siemens 8).
    [Fact]
    public void Label_AtTheBlockStart_IsAWordOfItsOwn()
    {
        SourceBlock block = Only("N10 Werkzeug_26_1: G0 X0");

        Assert.Equal(["N", ":", "G", "X"], Addresses(block));
        Assert.Equal("WERKZEUG_26_1", block.Words[1].Text);
    }

    // ; starts a comment at the end of a block, a ; inside a string belongs to the string (siemens 1).
    [Fact]
    public void Semicolon_StartsTheComment_ButNotInsideAString()
    {
        SourceBlock block = Only("MSG(\"A;B\") ; NEXT STEP");

        Assert.Equal("NEXT STEP", block.Comment);
        Assert.Equal("(\"A;B\")", block.Words[0].Text);
    }

    // / or /0 skips on the first level, /1 to /9 on the others (siemens 1; controller-mapping 1, SKIP).
    [Theory]
    [InlineData("/G0 X0", null)]
    [InlineData("/0 G0 X0", null)]
    [InlineData("/3 N10 G0 X0", 3)]
    public void Slash_InFrontOfTheBlock_IsTheSkipWithItsLevel(string line, int? level)
    {
        SourceBlock block = Only(line);

        Assert.True(block.BlockSkip);
        Assert.Equal(level, block.SkipSwitch);
    }

    // %_N_SHAFT_MPF begins a unit of an archive (siemens 1).
    [Fact]
    public void UnitHeader_IsAPercentWordWithTheUnit()
    {
        SourceBlock block = Only("%_N_SHAFT_MPF");

        Assert.Equal("%", block.Words[0].Address);
        Assert.Equal("_N_SHAFT_MPF", block.Words[0].Text);
    }

    // A statement of the high-level language is one word with the rest of the block (siemens 8).
    [Fact]
    public void Statement_IfWithItsConditionAndJump_IsOneWord()
    {
        SourceBlock block = Only("N20 IF R1 == 1 GOTOF LAB1");

        Assert.Equal(["N", "IF"], Addresses(block));
        Assert.Equal("R1 == 1 GOTOF LAB1", block.Words[1].Text);
    }

    // DEFINE NAME AS text defines a macro that the blocks after it read expanded (siemens 8; controller-mapping 6).
    [Fact]
    public void Define_Macro_IsExpandedInTheBlocksAfterIt()
    {
        List<SourceBlock> blocks = Tokenize("DEFINE SPINDLE_ON AS M3 S1000\nSPINDLE_ON\n");

        Assert.Equal(["M", "S"], Addresses(blocks[1]));
        Assert.Equal("SPINDLE_ON", blocks[1].Text);
    }

    // The recipe block of the STAMA post between its markers is not NC syntax and stays whole (machine-builders 2).
    [Fact]
    public void Recipe_BetweenItsMarkers_IsKeptWhole()
    {
        List<SourceBlock> blocks = Tokenize("<PROG_BEGIN_C1>\nGREZ_1=5\n<PROG_END_PAR>\nG0 X0\n");

        Assert.Equal(["<", "<", "<", "G"], [.. blocks.Select(block => block.Words[0].Address)]);
    }

    // Separators after the address are allowed: F 100 (siemens 1).
    [Fact]
    public void Blank_AfterASingleLetterAddress_BelongsToTheWord()
    {
        SourceBlock block = Only("G1 F 100");

        Assert.Equal(["G", "F"], Addresses(block));
        Assert.Equal(100m, block.Words[1].Number);
    }

    // An indexed address keeps its index: SPOS[2]=90, $P_UIFR[1, X, TR]= (siemens 5, 4).
    [Fact]
    public void Index_OfAnAddress_IsPartOfTheAddress()
    {
        SourceBlock block = Only("SPOS[2]=90 $P_UIFR[1, X, TR]=-56.667");

        Assert.Equal(["SPOS[2]", "$P_UIFR[1,X,TR]"], Addresses(block));
        Assert.Equal(-56.667m, block.Words[1].Number);
    }

    private static SourceBlock Only(string line)
    {
        return Tokenize(line + "\n")[0];
    }

    private static List<SourceBlock> Tokenize(string text)
    {
        return [.. new SiemensTokenizer().Tokenize(text)];
    }

    private static List<string> Addresses(SourceBlock block)
    {
        return [.. block.Words.Select(word => word.Address)];
    }
}
