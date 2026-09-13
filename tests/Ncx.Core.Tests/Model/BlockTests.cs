using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// Blocks are built from words and read back through the lookups of architecture 4 (D106).
/// </summary>
public sealed class BlockTests
{
    // Keys with different addresses are different words (language 5 rule 4): OFFSET:LEN and OFFSET:RAD are read back
    // each by its address (phase 0, P0-02).
    [Fact]
    public void Find_OffsetLenAndOffsetRad_ReadsEachBackByItsAddress()
    {
        var offsetLength = new Word { Key = "OFFSET", Addr = "LEN", Value = new IntegerValue(1, "1") };
        var offsetRadius = new Word { Key = "OFFSET", Addr = "RAD", Value = new IntegerValue(1, "1") };
        var block = new Block { Line = 10, Words = [offsetLength, offsetRadius] };

        Assert.Same(offsetLength, block.Find("OFFSET", "LEN"));
        Assert.Same(offsetRadius, block.Find("OFFSET", "RAD"));
    }

    // The same key without an address is another word than the key with one (language 5 rule 4).
    [Fact]
    public void Find_AddressTheBlockDoesNotCarry_FindsNothing()
    {
        var offsetLength = new Word { Key = "OFFSET", Addr = "LEN", Value = new IntegerValue(1, "1") };
        var block = new Block { Line = 10, Words = [offsetLength] };

        Assert.Null(block.Find("OFFSET", "RAD"));
        Assert.Null(block.Find("OFFSET", null));
    }

    // Find by key alone reads the first word of that key in the order written, whatever its address.
    [Fact]
    public void Find_KeyAlone_ReadsTheFirstWordOfThatKeyAsWritten()
    {
        var coolantAir = new Word { Key = "COOLANT", Addr = "AIR", Value = new IdentValue("ON") };
        var coolantThrough = new Word { Key = "COOLANT", Addr = "THROUGH", Value = new IdentValue("ON") };
        var block = new Block { Line = 7, Words = [coolantAir, coolantThrough] };

        Assert.Same(coolantAir, block.Find("COOLANT"));
    }

    [Fact]
    public void Find_KeyTheBlockDoesNotCarry_FindsNothing()
    {
        var block = new Block { Line = 7, Words = [new Word { Key = "RAPID" }] };

        Assert.Null(block.Find("LINE"));
    }

    // The lookup of the plugin template: the block that switches the through-spindle coolant on (code-guidelines 11).
    [Fact]
    public void Has_CoolantThroughOn_IsTrueForThatWord()
    {
        var coolantThrough = new Word { Key = "COOLANT", Addr = "THROUGH", Value = new IdentValue("ON") };
        var block = new Block { Line = 7, Words = [coolantThrough] };

        Assert.True(block.Has("COOLANT", "THROUGH", "ON"));
    }

    [Fact]
    public void Has_CoolantThroughOff_IsFalseForOn()
    {
        var coolantThrough = new Word { Key = "COOLANT", Addr = "THROUGH", Value = new IdentValue("OFF") };
        var block = new Block { Line = 7, Words = [coolantThrough] };

        Assert.False(block.Has("COOLANT", "THROUGH", "ON"));
    }

    // COOLANT=ON is the default coolant channel, another word than COOLANT:THROUGH=ON (language 4.6, 5 rule 4).
    [Fact]
    public void Has_DefaultCoolantOn_IsFalseForTheThroughChannel()
    {
        var coolant = new Word { Key = "COOLANT", Value = new IdentValue("ON") };
        var block = new Block { Line = 7, Words = [coolant] };

        Assert.False(block.Has("COOLANT", "THROUGH", "ON"));
        Assert.True(block.Has("COOLANT", null, "ON"));
    }

    [Fact]
    public void Has_KeyAlone_IsTrueWhateverTheAddress()
    {
        var coolantThrough = new Word { Key = "COOLANT", Addr = "THROUGH", Value = new IdentValue("ON") };
        var block = new Block { Line = 7, Words = [coolantThrough] };

        Assert.True(block.Has("COOLANT"));
        Assert.False(block.Has("SPINDLE"));
    }

    // Numbers are compared as written, never reformatted (language 2 rule 5).
    [Fact]
    public void Has_NumberValue_ComparesTheNumberAsWritten()
    {
        var x = new Word { Key = "X", Value = new DecimalValue(10.50m, "10.50") };
        var block = new Block { Line = 7, Words = [new Word { Key = "LINE" }, x] };

        Assert.True(block.Has("X", null, "10.50"));
        Assert.False(block.Has("X", null, "10.5"));
    }

    // A word written without a value has the empty value text (language 3, Word).
    [Fact]
    public void Has_WordWithoutValue_MatchesTheEmptyValue()
    {
        var block = new Block { Line = 17, Words = [new Word { Key = "TOOL" }] };

        Assert.True(block.Has("TOOL", null, ""));
        Assert.False(block.Has("TOOL", null, "3"));
    }

    // A block never sorts its words; the canonical writer does (phase 0, P0-02).
    [Fact]
    public void Words_WrittenInFreeOrder_AreReadBackInThatOrder()
    {
        var block = new Block
        {
            Line = 14,
            Words =
            [
                new Word { Key = "Y", Value = new IntegerValue(2, "2") },
                new Word { Key = "LINE" },
                new Word { Key = "COMP", Value = new IdentValue("LEFT") },
                new Word { Key = "X", Value = new IntegerValue(7, "7") },
            ],
        };

        var keys = new List<string>();
        foreach (Word word in block.Words)
        {
            keys.Add(word.Key);
        }

        Assert.Equal(["Y", "LINE", "COMP", "X"], keys);
    }

    // SKIP without a value marks the block for the block skip switch (language 4.1).
    [Fact]
    public void Skip_BareSkip_IsSkippedWithoutASwitchNumber()
    {
        var block = new Block { Line = 30, Words = [new Word { Key = "SKIP" }, new Word { Key = "JUMP" }] };

        Assert.True(block.Skip);
        Assert.Null(block.SkipNumber);
    }

    // SKIP=n names the block skip switch n, 1 to 9 (language 4.1).
    [Fact]
    public void Skip_SkipThree_NamesBlockSkipSwitchThree()
    {
        var skip = new Word { Key = "SKIP", Value = new IntegerValue(3, "3") };
        var block = new Block { Line = 30, Words = [skip, new Word { Key = "JUMP" }] };

        Assert.True(block.Skip);
        Assert.Equal(3, block.SkipNumber);
    }

    [Fact]
    public void Skip_BlockWithoutSkip_IsNotSkipped()
    {
        var block = new Block { Line = 30, Words = [new Word { Key = "RAPID" }] };

        Assert.False(block.Skip);
        Assert.Null(block.SkipNumber);
    }

    // A block keeps its verb, its trailing comment with the semicolon and its source text (architecture 4, D92).
    [Fact]
    public void Block_BuiltWithVerbCommentAndSourceText_ReadsThemBack()
    {
        var line = new Word { Key = "LINE" };
        var block = new Block
        {
            Line = 20,
            Words = [line, new Word { Key = "Y", Value = new IntegerValue(2, "2") }],
            Verb = line,
            Comment = "; H14 L Y2 RL",
            SourceText = "LINE Y=2 COMP=LEFT                                      ; H14 L Y2 RL",
        };

        Assert.Equal(20, block.Line);
        Assert.Same(line, block.Verb);
        Assert.Equal("; H14 L Y2 RL", block.Comment);
        Assert.Equal("LINE Y=2 COMP=LEFT                                      ; H14 L Y2 RL", block.SourceText);
        Assert.False(block.IsGenerated);
        Assert.Null(block.OriginLine);
    }

    [Fact]
    public void Block_WithoutVerbAndComment_HasNeither()
    {
        var block = new Block { Line = 12, Words = [new Word { Key = "COOLANT", Value = new IdentValue("ON") }] };

        Assert.Null(block.Verb);
        Assert.Null(block.Comment);
        Assert.Null(block.SourceText);
    }

    // A generated block names the block it was generated for (language 4.15, D98).
    [Fact]
    public void Block_Generated_KeepsTheLineOfItsOrigin()
    {
        var spindleOff = new Word { Key = "SPINDLE", Addr = "MAIN", Value = new IdentValue("OFF") };
        var block = new Block { Line = 13, Words = [spindleOff], IsGenerated = true, OriginLine = 12 };

        Assert.True(block.IsGenerated);
        Assert.Equal(12, block.OriginLine);
    }
}
