using Ncx.Analytics;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The block range of every analytic, --from 380 --to 600, NCX line numbers of the file (virtual machine 8, D67).
/// </summary>
public sealed class BlockRangeTests
{
    // VM 8, D67: --from 380 --to 600 holds both lines at its ends and nothing outside them.
    [Theory]
    [InlineData(379, false)]
    [InlineData(380, true)]
    [InlineData(490, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Contains_FromAndTo_HoldsTheLinesFromOneToTheOther(int line, bool inside)
    {
        var range = new BlockRange { From = 380, To = 600 };

        Assert.Equal(inside, range.Contains(line));
    }

    // --from alone runs to the end of the file, --to alone from its start.
    [Fact]
    public void Contains_OneEndOpen_RunsToTheEndOrFromTheStartOfTheFile()
    {
        var from = new BlockRange { From = 380 };
        var to = new BlockRange { To = 600 };

        Assert.False(from.Contains(379));
        Assert.True(from.Contains(100000));
        Assert.True(to.Contains(1));
        Assert.False(to.Contains(601));
    }

    // Without --from and --to an analytic looks at the whole file.
    [Fact]
    public void Whole_EveryLine_IsInside()
    {
        Assert.True(BlockRange.Whole.IsWhole);
        Assert.True(BlockRange.Whole.Contains(1));
        Assert.True(BlockRange.Whole.Contains(100000));
    }

    // The report names the range it covers.
    [Fact]
    public void Describe_EveryForm_NamesTheRange()
    {
        Assert.Equal("lines 380 to 600", new BlockRange { From = 380, To = 600 }.Describe());
        Assert.Equal("from line 380", new BlockRange { From = 380 }.Describe());
        Assert.Equal("to line 600", new BlockRange { To = 600 }.Describe());
        Assert.Equal("the whole file", BlockRange.Whole.Describe());
    }
}
