using Ncx.Analytics.Runtime;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Segments;

/// <summary>
/// The segment length (virtual machine 8; architecture 9): for every MOTION in the workpiece frame whose positions are
/// known, the distance between its start and its end point, the arc length for an arc; the minimum, maximum and mean, a
/// histogram with configurable bins, the ten shortest motions with their lines and the motions skipped and why, over
/// the blocks of the range (D67; implementation 14, P4-03). One of the two analytics CAM programmers use to judge
/// 5-axis output.
/// </summary>
public sealed partial class SegmentAnalytic : IAnalytic
{
    private readonly AnalyticOptions _options;
    private readonly RangeFilter _range;
    private readonly Histogram _histogram;
    private readonly VerbStatistics _statistics = new();
    private readonly WorstMotions _shortest = new(largest: false);

    /// <summary>
    /// The segment length of one run, with the histogram over the default bounds.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    public SegmentAnalytic(AnalyticOptions options)
        : this(options, DefaultBins)
    {
    }

    /// <summary>
    /// The segment length of one run, with the histogram over the bounds given.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    /// <param name="bins">The bounds between the bins of the histogram in mm, rising from above 0.</param>
    public SegmentAnalytic(AnalyticOptions options, IReadOnlyList<decimal> bins)
    {
        _options = options;
        _range = new RangeFilter(options.Range);
        _histogram = new Histogram(bins);
    }

    /// <summary>
    /// The bounds between the bins of the histogram in mm when no others are given: 0.01, 0.1, 0.5, 1, 5, 10, 50, 100.
    /// </summary>
    // TODO(question): implementation 14 (P4-03) asks for a histogram "with configurable bins" and names no place where
    // a user gives them: architecture 10, the one list of the options of ncx, has no such option of ncx analyze, and
    // neither ncx.toml nor the machine file has a key for them. The bounds are a parameter of the analytic that a
    // front end or a plugin can pass, and ncx analyze writes these default bounds, until that is answered.
    public static IReadOnlyList<decimal> DefaultBins { get; } = [0.01m, 0.1m, 0.5m, 1m, 5m, 10m, 50m, 100m];

    /// <inheritdoc/>
    public BlockRange Range => _options.Range;

    /// <summary>
    /// The lengths per verb and over all, in mm. Final after the run.
    /// </summary>
    internal VerbStatistics Statistics => _statistics;

    /// <summary>
    /// The histogram of the lengths.
    /// </summary>
    internal Histogram Histogram => _histogram;

    /// <summary>
    /// The ten shortest motions, the shortest first.
    /// </summary>
    internal IReadOnlyList<WorstMotion> Shortest => _shortest.Motions;

    /// <summary>
    /// The motions in the range, measured or skipped.
    /// </summary>
    internal int Motions { get; private set; }

    /// <summary>
    /// The motions skipped because they move in the machine frame: HOME, FRAME=MACHINE.
    /// </summary>
    internal int InMachineFrame { get; private set; }

    /// <summary>
    /// The motions skipped because an axis of their length is unknown where they start.
    /// </summary>
    internal int FromUnknown { get; private set; }

    /// <summary>
    /// The motions skipped because an axis of their length is unknown where they end.
    /// </summary>
    internal int ToUnknown { get; private set; }

    /// <summary>
    /// The motions skipped because an axis of their length is known at both ends, but in two frames.
    /// </summary>
    internal int BetweenFrames { get; private set; }

    /// <summary>
    /// The arcs skipped because they did not resolve in their plane: a full circle from an unknown start.
    /// </summary>
    internal int UnresolvedArcs { get; private set; }

    private int Skipped => InMachineFrame + FromUnknown + ToUnknown + BetweenFrames + UnresolvedArcs;

    /// <inheritdoc/>
    public void On(VmEvent vmEvent)
    {
        _range.On(vmEvent);
        if (vmEvent is MotionEvent motion && _range.Contains(motion))
        {
            Measure(motion);
        }
    }

    /// <inheritdoc/>
    public string Report()
    {
        return Write();
    }

    // Every MOTION is measured (virtual machine 8): RAPID, LINE, ARC, RETRACT and HOME, and the motions of an expanded
    // CYCLE_CALL, which analyze raises (D37).
    private void Measure(MotionEvent motion)
    {
        Motions++;

        // In the workpiece frame (implementation 14, P4-03): a FRAME=MACHINE block and a HOME move in machine
        // coordinates (virtual machine 2.1, 3 step 5, 3.4, 7), and are skipped.
        if (motion.Frame == PositionFrame.Machine)
        {
            InMachineFrame++;
            return;
        }

        // With known positions: the MOTION carries the euclidean length over the linear axes, the arc length for an
        // arc, and none when an axis it moved is not known at both ends in one frame (virtual machine 7, 8).
        if (motion.Length is not decimal length)
        {
            CountUnknown(motion);
            return;
        }

        // In mm, as the tool list gives its distances: an INCH program is converted.
        double millimetres = (double)length * CommandedFeed.MillimetresPerUnit(motion);
        _statistics.Add(motion.Verb, millimetres);
        _histogram.Add(millimetres);
        _shortest.Add(new WorstMotion { Line = motion.Block.Line, Verb = motion.Verb, Value = millimetres });
    }

    // Why a motion has no length (implementation 14, P4-03: the skipped blocks with unknown positions and why): the
    // first axis of its length that is not known at both ends in one frame gives the reason. When every such axis is,
    // the motion is an ARC that did not resolve, a full circle from a start not known in its plane (virtual machine
    // 3.2).
    private void CountUnknown(MotionEvent motion)
    {
        foreach (KeyValuePair<string, AxisPosition> target in motion.To)
        {
            AxisPosition start = MotionPositions.StartOf(motion, target.Key);
            if (!IsLength(target.Key, start, target.Value))
            {
                continue;
            }

            switch (MotionPositions.Gap(start, target.Value))
            {
                case PositionGap.StartUnknown:
                    FromUnknown++;
                    return;
                case PositionGap.EndUnknown:
                    ToUnknown++;
                    return;
                case PositionGap.TwoFrames:
                    BetweenFrames++;
                    return;
            }
        }

        UnresolvedArcs++;
    }

    // A rotary axis turns in degrees and is no part of a length, except in the polar and the cylinder plane, where its
    // word is a length (virtual machine 3.1, 8, D102), as the MOTION measures it.
    private bool IsLength(string axis, AxisPosition start, AxisPosition end)
    {
        bool rotary = _options.Machine.ResolveAxis(axis)?.Kind == AxisKind.Rotary;
        return !rotary || (MotionPositions.InTransformedPlane(start) && MotionPositions.InTransformedPlane(end));
    }
}
