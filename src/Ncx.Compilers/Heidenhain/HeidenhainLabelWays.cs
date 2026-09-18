namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The labels of the program being written with what each found on the way the text runs into it, and the ways of the
/// jumps forward that wait for their label: a way into a label is followed where both the jump and the label are
/// written (language 4.9; HeidenhainArrivals).
/// </summary>
internal sealed class HeidenhainLabelWays
{
    // A step stands once in the walk of a compile, a block of a subprogram once per walk (D99), so a step names its
    // label in its walk.
    private readonly Dictionary<BlockStep, HeidenhainLabelWay> _labels = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<BlockStep, List<HeidenhainArrival>> _waiting = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Forgets the labels of the program before: a jump goes to a label of its own section (language 4.9).
    /// </summary>
    public void Clear()
    {
        _labels.Clear();
        _waiting.Clear();
    }

    /// <summary>
    /// Records what the label of the step found on the way the text runs into it.
    /// </summary>
    public void Record(BlockStep label, HeidenhainLabelWay way)
    {
        _labels[label] = way;
    }

    /// <summary>
    /// What the label of the step found on the way the text runs into it; null while it is not written.
    /// </summary>
    public HeidenhainLabelWay? WayOf(BlockStep label)
    {
        return _labels.TryGetValue(label, out HeidenhainLabelWay? way) ? way : null;
    }

    /// <summary>
    /// Keeps a way for the label of the step, which is not written yet.
    /// </summary>
    public void Wait(BlockStep label, HeidenhainArrival arrival)
    {
        if (!_waiting.TryGetValue(label, out List<HeidenhainArrival>? arrivals))
        {
            arrivals = [];
            _waiting.Add(label, arrivals);
        }

        arrivals.Add(arrival);
    }

    /// <summary>
    /// The ways that wait for the label of the step, each given once.
    /// </summary>
    public List<HeidenhainArrival> TakeWaiting(BlockStep label)
    {
        if (!_waiting.Remove(label, out List<HeidenhainArrival>? arrivals))
        {
            return [];
        }

        return arrivals;
    }
}
