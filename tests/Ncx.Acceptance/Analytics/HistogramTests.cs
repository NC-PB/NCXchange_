using Ncx.Analytics;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The histogram of the segment length and the tool vector change, over configurable bins (implementation 14, P4-03).
/// </summary>
public sealed class HistogramTests
{
    // P4-03: a bin holds the values from its lower bound up to, not including, its upper bound; the first bin starts at
    // 0 and the last has no upper bound. 0 and 0.009 fall into 0 to 0.01, 0.01 into 0.01 to 0.1, 0.1 and 5 into 0.1 and
    // more.
    [Fact]
    public void Add_ValueOnABound_FallsIntoTheBinThatStartsThere()
    {
        var histogram = new Histogram([0.01m, 0.1m]);

        foreach (double value in new[] { 0, 0.009, 0.01, 0.1, 5 })
        {
            histogram.Add(value);
        }

        Assert.Equal(3, histogram.BinCount);
        Assert.Equal(2, histogram.CountOf(0));
        Assert.Equal(1, histogram.CountOf(1));
        Assert.Equal(2, histogram.CountOf(2));
    }

    // The table names each bin by its bounds, as the bounds are written, and gives the motions in it.
    [Fact]
    public void Table_Bounds_NamesEachBinByItsBounds()
    {
        var histogram = new Histogram([0.01m, 0.1m]);
        histogram.Add(0.05);

        Assert.Equal("length (mm),motions\n0 to 0.01,0\n0.01 to 0.1,1\n0.1 and more,0\n",
            histogram.Table("length (mm)").Write(TableFormat.Csv));
    }

    // Bounds that do not rise from above 0 are a mistake of the caller (code-guidelines 6).
    [Fact]
    public void Constructor_BoundsThatDoNotRise_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Histogram([1m, 1m]));
        Assert.Throws<ArgumentException>(() => new Histogram([0m, 1m]));
    }
}
