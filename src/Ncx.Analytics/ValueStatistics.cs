namespace Ncx.Analytics;

/// <summary>
/// The count, the smallest, the largest and the mean of one quantity over the motions of a report (implementation 14,
/// P4-03: the minimum, maximum and mean, the extremes), summed motion by motion as the run streams by.
/// </summary>
internal sealed class ValueStatistics
{
    /// <summary>
    /// The motions counted.
    /// </summary>
    public long Count { get; private set; }

    /// <summary>
    /// The smallest value; 0 before the first.
    /// </summary>
    public double Minimum { get; private set; }

    /// <summary>
    /// The largest value; 0 before the first.
    /// </summary>
    public double Maximum { get; private set; }

    /// <summary>
    /// The sum of the values.
    /// </summary>
    public double Sum { get; private set; }

    /// <summary>
    /// The mean of the values; 0 before the first.
    /// </summary>
    public double Mean => Count == 0 ? 0 : Sum / Count;

    /// <summary>
    /// Counts the value of one motion.
    /// </summary>
    public void Add(double value)
    {
        Minimum = Count == 0 ? value : Math.Min(Minimum, value);
        Maximum = Count == 0 ? value : Math.Max(Maximum, value);
        Sum += value;
        Count++;
    }
}
