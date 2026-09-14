using System.Globalization;

namespace ThroughCoolantSourceRule.Tests;

/// <summary>
/// The reader rule of D66: source blocks in, the NCX blocks the rule writes for them out
/// (architecture 9; machine-config 5a).
/// </summary>
public sealed class ThroughCoolantSourceRuleTests
{
    // D66: the three blocks a clutch machine writes for COOLANT:THROUGH=ON are the one word
    // again, on the line of the first of them.
    [Fact]
    public void Fold_M5M51M3S1500_IsOneThroughCoolantWord()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "M5"), [Source(11, "M51"), Source(12, "M3 S1500")],
            builder);

        Assert.Equal(3, folded);
        Assert.Equal(["10: COOLANT:THROUGH=ON"], Written(builder));
    }

    // The block numbers of the source are no part of what the blocks do.
    [Fact]
    public void Fold_BlocksWithBlockNumbers_AreFoldedAsWell()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "N10 M5"),
            [Source(11, "N20 M51"), Source(12, "N30 M3 S1500")], builder);

        Assert.Equal(3, folded);
        Assert.Equal(["10: COOLANT:THROUGH=ON"], Written(builder));
    }

    // A stop that another code follows is a stop: nothing is folded and nothing written.
    [Fact]
    public void Fold_StopFollowedByAnotherCode_FoldsNothing()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "M5"), [Source(11, "M8"), Source(12, "M3 S1500")],
            builder);

        Assert.Equal(0, folded);
        Assert.Empty(Written(builder));
    }

    // A stop that comes with a move is no part of the sequence, since folding it would lose
    // the move.
    [Fact]
    public void Fold_StopWithAMove_FoldsNothing()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "G0 Z50 M5"),
            [Source(11, "M51"), Source(12, "M3 S1500")], builder);

        Assert.Equal(0, folded);
        Assert.Empty(Written(builder));
    }

    // D231, D232: the reader offers Read one block and none after it, so Read claims nothing
    // and the reader reads the block itself.
    [Fact]
    public void Read_M5_ClaimsNothingWithoutTheBlocksAfterIt()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));
        var state = new SourceState(null, new Dictionary<string, int>());

        bool claimed = new ThroughCoolantSourceRule().Read(Source(10, "M5"), state, builder);

        Assert.False(claimed);
        Assert.Empty(Written(builder));
    }

    // A source block as a tokenizer reads it: "M3 S1500" is the words M3 and S1500.
    private static SourceBlock Source(int line, string text)
    {
        var words = new List<SourceWord>();
        foreach (string written in text.Split(' '))
        {
            string digits = written.Substring(1);
            words.Add(new SourceWord
            {
                Address = written.Substring(0, 1),
                Text = digits,
                Number = decimal.Parse(digits, CultureInfo.InvariantCulture),
            });
        }

        return new SourceBlock { Line = line, Text = text, Words = words };
    }

    // The NCX blocks the rule wrote, each as its line and its canonical text.
    private static List<string> Written(NcxBuilder builder)
    {
        var written = new List<string>();
        foreach (Block block in builder.Build().Blocks)
        {
            written.Add(block.Line.ToString(CultureInfo.InvariantCulture) + ": " + NcxWriter.WriteBlock(block));
        }

        return written;
    }
}
