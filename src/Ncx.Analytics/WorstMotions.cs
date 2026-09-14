namespace Ncx.Analytics;

/// <summary>
/// The ten motions with the smallest or the largest value of a report, the ten shortest blocks of the segment length
/// and the ten largest changes of the tool vector change (implementation 14, P4-03). Only the ten are kept while the
/// run streams by, so that a point list of millions of blocks runs (implementation 14, risks).
/// </summary>
internal sealed class WorstMotions
{
    /// <summary>
    /// The motions a list keeps: ten (implementation 14, P4-03).
    /// </summary>
    public const int Capacity = 10;

    private readonly bool _largest;
    private readonly List<WorstMotion> _motions = [];

    /// <summary>
    /// An empty list.
    /// </summary>
    /// <param name="largest">True for the largest values, false for the smallest.</param>
    public WorstMotions(bool largest)
    {
        _largest = largest;
    }

    /// <summary>
    /// The motions kept, the worst first; of equal values the earlier motion of the run first.
    /// </summary>
    public IReadOnlyList<WorstMotion> Motions => _motions;

    /// <summary>
    /// Keeps a motion when it is among the ten worst so far.
    /// </summary>
    public void Add(WorstMotion motion)
    {
        // A motion goes behind every motion that is as bad as it or worse, so that of equal values the earlier stays in
        // front and a later motion of the same value never pushes it out.
        int index = _motions.Count;
        while (index > 0 && IsWorse(motion.Value, _motions[index - 1].Value))
        {
            index--;
        }

        if (index >= Capacity)
        {
            return;
        }

        _motions.Insert(index, motion);
        if (_motions.Count > Capacity)
        {
            _motions.RemoveAt(Capacity);
        }
    }

    private bool IsWorse(double value, double than)
    {
        return _largest ? value > than : value < than;
    }
}
