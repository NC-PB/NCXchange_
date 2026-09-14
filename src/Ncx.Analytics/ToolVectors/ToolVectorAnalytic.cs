using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.ToolVectors;

/// <summary>
/// The tool vector change (virtual machine 8; architecture 9): for every MOTION the angle between the tool vector at
/// its start and at its end, the tool vector from TX TY TZ when the program gives them, else from the rotary axes
/// applied to the tool axis by the convention of <see cref="ToolVectorConvention"/>; the minimum, maximum and mean, a
/// histogram with configurable bins, the ten largest changes with their lines and the motions skipped and why, over
/// the blocks of the range (D67; implementation 14, P4-03). One of the two analytics CAM programmers use to judge
/// 5-axis output.
/// </summary>
public sealed partial class ToolVectorAnalytic : IAnalytic
{
    // A change keeps three decimals of a degree, as the sweep of an arc does (MotionEvents.SweepDecimals of the virtual
    // machine); the rounding also takes away the rest a turn and its return leave in double precision.
    private const int AngleDecimals = 3;

    private readonly AnalyticOptions _options;
    private readonly RangeFilter _range;
    private readonly ToolVectorConvention _convention;
    private readonly Histogram _histogram;
    private readonly VerbStatistics _statistics = new();
    private readonly WorstMotions _largest = new(largest: true);

    /// <summary>
    /// The tool vector change of one run, with the histogram over the default bounds.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    public ToolVectorAnalytic(AnalyticOptions options)
        : this(options, DefaultBins)
    {
    }

    /// <summary>
    /// The tool vector change of one run, with the histogram over the bounds given.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    /// <param name="bins">The bounds between the bins of the histogram in degrees, rising from above 0.</param>
    public ToolVectorAnalytic(AnalyticOptions options, IReadOnlyList<decimal> bins)
    {
        _options = options;
        _range = new RangeFilter(options.Range);
        _convention = new ToolVectorConvention(options.Machine);
        _histogram = new Histogram(bins);
    }

    /// <summary>
    /// The bounds between the bins of the histogram in degrees when no others are given: 0.01, 0.1, 0.5, 1, 2, 5, 10,
    /// 45, 90.
    /// </summary>
    // TODO(question): where a user gives the bins of the histogram, the question at SegmentAnalytic.DefaultBins; ncx
    // analyze writes these default bounds until it is answered.
    public static IReadOnlyList<decimal> DefaultBins { get; } = [0.01m, 0.1m, 0.5m, 1m, 2m, 5m, 10m, 45m, 90m];

    /// <inheritdoc/>
    public BlockRange Range => _options.Range;

    /// <summary>
    /// The changes per verb and over all, in degrees. Final after the run.
    /// </summary>
    internal VerbStatistics Statistics => _statistics;

    /// <summary>
    /// The histogram of the changes.
    /// </summary>
    internal Histogram Histogram => _histogram;

    /// <summary>
    /// The ten largest changes above 0, the largest first.
    /// </summary>
    internal IReadOnlyList<WorstMotion> Largest => _largest.Motions;

    /// <summary>
    /// The motions in the range, measured or skipped.
    /// </summary>
    internal int Motions { get; private set; }

    /// <summary>
    /// The motions skipped because the tool vector is not known where they start.
    /// </summary>
    internal int StartUnknown { get; private set; }

    /// <summary>
    /// The motions skipped because the tool vector is not known where they end.
    /// </summary>
    internal int EndUnknown { get; private set; }

    /// <summary>
    /// The motions skipped because a rotary axis turns from one frame into another.
    /// </summary>
    internal int BetweenFrames { get; private set; }

    /// <summary>
    /// The motions skipped because a rotary axis moves as a length of the polar or cylinder plane.
    /// </summary>
    internal int InTransformedPlane { get; private set; }

    private int Skipped => StartUnknown + EndUnknown + BetweenFrames + InTransformedPlane;

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

    // For every MOTION the angle between the tool vectors before and after (virtual machine 8).
    private void Measure(MotionEvent motion)
    {
        Motions++;

        // The tool vector from TX TY TZ when present, else from the rotary axis positions (P4-03); TX TY TZ stand from
        // their LINE until a rotary axis word or TCPM=OFF (virtual machine 2.2, D81). The WORKPLANE and the workpiece
        // holder are those the block moves under: its state words apply before its motion (virtual machine 3, steps 3
        // and 5).
        IReadOnlyList<decimal>? startWords = motion.Before.Motion.ToolVector;
        IReadOnlyList<decimal>? endWords = motion.ToolVector;
        Workplane workplane = motion.After.Frame.Workplane;
        string? holder = motion.After.Frame.WorkpieceHolder;
        if (startWords is null && endWords is null)
        {
            // A motion that turns no rotary axis of the convention keeps its tool vector: a change of 0, also where the
            // axes stand at angles the virtual machine does not know.
            if (!TurnsRotaryAxis(motion, holder))
            {
                Add(motion, 0, null, null);
                return;
            }

            if (!TurnIsKnown(motion, holder))
            {
                return;
            }
        }

        ToolVector? start = startWords is not null
            ? ToolVector.FromWords(startWords)
            : _convention.VectorAt(workplane, holder, motion.From);
        ToolVector? end = endWords is not null
            ? ToolVector.FromWords(endWords)
            : _convention.VectorAt(workplane, holder, motion.To);
        if (start is not ToolVector from)
        {
            StartUnknown++;
            return;
        }

        if (end is not ToolVector to)
        {
            EndUnknown++;
            return;
        }

        Add(motion, from.DegreesTo(to), from, to);
    }

    // True when a rotary axis of the convention that applies under the workpiece holder stands elsewhere at the end.
    private bool TurnsRotaryAxis(MotionEvent motion, string? holder)
    {
        foreach (ConventionAxis axis in _convention.Axes)
        {
            if (axis.AppliesWhile(holder)
                && MotionPositions.StartOf(motion, axis.NcxName) != MotionPositions.EndOf(motion, axis.NcxName))
            {
                return true;
            }
        }

        return false;
    }

    // A rotary axis that turns is known at both ends in one frame, as an axis of the length of a MOTION must be
    // (virtual machine 7, 8), and as an angle, not as a length of the polar or cylinder plane (virtual machine 3.1,
    // D102); otherwise the change is not known, and the motion is counted with the reason (implementation 14, P4-03).
    private bool TurnIsKnown(MotionEvent motion, string? holder)
    {
        foreach (ConventionAxis axis in _convention.Axes)
        {
            if (!axis.AppliesWhile(holder))
            {
                continue;
            }

            AxisPosition start = MotionPositions.StartOf(motion, axis.NcxName);
            AxisPosition end = MotionPositions.EndOf(motion, axis.NcxName);
            switch (MotionPositions.Gap(start, end))
            {
                case PositionGap.StartUnknown:
                    StartUnknown++;
                    return false;
                case PositionGap.EndUnknown:
                    EndUnknown++;
                    return false;
                case PositionGap.TwoFrames:
                    BetweenFrames++;
                    return false;
            }

            if (start != end && MotionPositions.InTransformedPlane(end))
            {
                InTransformedPlane++;
                return false;
            }
        }

        return true;
    }

    // A change counts in the statistics per verb and in the histogram; a change above 0 is a candidate for the ten
    // largest, with its line and its vectors (implementation 14, P4-03).
    private void Add(MotionEvent motion, double degrees, ToolVector? start, ToolVector? end)
    {
        double change = Math.Round(degrees, AngleDecimals);
        _statistics.Add(motion.Verb, change);
        _histogram.Add(change);
        if (change > 0 && start is ToolVector from && end is ToolVector to)
        {
            _largest.Add(new WorstMotion
            {
                Line = motion.Block.Line,
                Verb = motion.Verb,
                Value = change,
                Start = from.ToText(),
                End = to.ToText(),
            });
        }
    }
}
