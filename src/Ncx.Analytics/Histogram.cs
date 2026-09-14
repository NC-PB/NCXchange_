namespace Ncx.Analytics;

/// <summary>
/// A histogram of one quantity of a report, lengths or angles, over bins given by their bounds: a bin holds the values
/// from its lower bound up to, not including, its upper bound, the first bin starts at 0 and the last has no upper
/// bound (implementation 14, P4-03: a histogram with configurable bins).
/// </summary>
internal sealed class Histogram
{
    private readonly List<decimal> _bounds;
    private readonly long[] _counts;

    /// <summary>
    /// A histogram over the bins its bounds give.
    /// </summary>
    /// <param name="bounds">The bounds between the bins, rising from above 0: 0.01 and 0.1 make the bins 0 to 0.01,
    /// 0.01 to 0.1 and 0.1 and more.</param>
    public Histogram(IReadOnlyList<decimal> bounds)
    {
        // Bounds that do not rise from above 0 are a mistake of the caller (code-guidelines 6).
        decimal previous = 0;
        foreach (decimal bound in bounds)
        {
            if (bound <= previous)
            {
                throw new ArgumentException("The bounds of a histogram rise from above 0.", nameof(bounds));
            }

            previous = bound;
        }

        _bounds = new List<decimal>(bounds);
        _counts = new long[bounds.Count + 1];
    }

    /// <summary>
    /// The number of bins, one more than the bounds.
    /// </summary>
    public int BinCount => _counts.Length;

    /// <summary>
    /// The values counted into a bin; the first bin is 0.
    /// </summary>
    public long CountOf(int bin)
    {
        return _counts[bin];
    }

    /// <summary>
    /// Counts a value into its bin.
    /// </summary>
    public void Add(double value)
    {
        int bin = 0;
        while (bin < _bounds.Count && value >= (double)_bounds[bin])
        {
            bin++;
        }

        _counts[bin]++;
    }

    /// <summary>
    /// The histogram as a table: one row per bin, named by its bounds as they are written, with the motions in it.
    /// </summary>
    /// <param name="quantity">The column of the bins with its unit: "length (mm)".</param>
    public TextTable Table(string quantity)
    {
        var table = new TextTable(quantity, "motions");
        for (int bin = 0; bin < _counts.Length; bin++)
        {
            table.Add(Label(bin), ReportText.Count(_counts[bin]));
        }

        return table;
    }

    // A bin by its bounds: "0 to 0.01", "0.01 to 0.1", the last "100 and more".
    private string Label(int bin)
    {
        string lower = bin == 0 ? "0" : ReportText.Number(_bounds[bin - 1]);
        return bin == _bounds.Count ? lower + " and more" : lower + " to " + ReportText.Number(_bounds[bin]);
    }
}
