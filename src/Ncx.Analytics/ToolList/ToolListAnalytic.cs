using Ncx.Analytics.Runtime;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.ToolList;

/// <summary>
/// The tool list (virtual machine 8; architecture 9): from TOOL_BEGIN and TOOL_END one row per tool use, with rpm,
/// feeds, offsets, the cutting and the rapid distance, the block count, the estimated time and whether the tool was
/// preloaded before its change, over the blocks of the range (D67).
/// </summary>
public sealed partial class ToolListAnalytic : IAnalytic
{
    private readonly AnalyticOptions _options;
    private readonly RangeFilter _range;
    private readonly RuntimeEstimator _estimator;

    // Every use in the order of its TOOL_BEGIN, and the use in the spindle of each holder by the holder's id.
    private readonly List<ToolUse> _uses = [];
    private readonly Dictionary<string, ToolUse> _inSpindle = new(StringComparer.Ordinal);

    // The first event of the block that runs, which is counted when the next block begins.
    private VmEvent? _block;
    private bool _finished;

    /// <summary>
    /// The tool list of one run.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    public ToolListAnalytic(AnalyticOptions options)
    {
        _options = options;
        _range = new RangeFilter(options.Range);
        _estimator = new RuntimeEstimator(options.Machine, _range, AddTime);
    }

    /// <inheritdoc/>
    public BlockRange Range => _options.Range;

    /// <summary>
    /// Every tool use in the order of its TOOL_BEGIN; those with a block in the range are the rows of the report.
    /// Final after Report.
    /// </summary>
    internal IReadOnlyList<ToolUse> Uses => _uses;

    /// <inheritdoc/>
    public void On(VmEvent vmEvent)
    {
        _range.On(vmEvent);

        // The estimate first: a tool change brings the path to rest, and the time of the motion before it goes to the
        // tool that made it, whose use is still in the spindle.
        _estimator.On(vmEvent);
        CountBlocks(vmEvent);
        switch (vmEvent)
        {
            case ToolEvent { Phase: EventPhase.Begin } begin:
                Begin(begin);
                break;
            case ToolEvent { Phase: EventPhase.End } end:
                _inSpindle.Remove(end.Holder);
                break;
            case MotionEvent motion:
                AddMotion(motion);
                break;
        }
    }

    /// <inheritdoc/>
    public string Report()
    {
        if (!_finished)
        {
            _estimator.Finish();
            if (_block is not null)
            {
                CountUnder(_block, 1);
            }

            _finished = true;
        }

        return Write();
    }

    // A tool entering the spindle begins a use (virtual machine 3.5, 7; architecture 9). It was preloaded when the
    // holder had it preloaded before the change, as the PRELOAD words left it (virtual machine 3.5, 8).
    // TODO(question): virtual machine 7 raises TOOL_END when a tool leaves the spindle and PROGRAM=END keeps it there
    // (virtual machine 4), so the last tool of a program gets no TOOL_END (wave-2 question #26). Its use stays in the
    // spindle until the next TOOL_BEGIN on its holder, or the end of the run, and the list names it with what it did,
    // until that is answered.
    private void Begin(ToolEvent begin)
    {
        bool preloaded = begin.Before.Holders.TryGetValue(begin.Holder, out HolderSnapshot? before)
            && before.Preloaded == begin.Tool;
        var use = new ToolUse { Tool = begin.Tool, Holder = begin.Holder, Preloaded = preloaded };
        _inSpindle[begin.Holder] = use;
        _uses.Add(use);
    }

    // The distance under the tool from the MOTION lengths (virtual machine 7, 8): cutting for LINE, ARC and the plunges
    // of a cycle, which reach the listener as LINE (virtual machine 3.3, D37), rapid for every other motion; a motion
    // of unknown length is in no distance and counted apart. The rpm and the feeds are those the tool cuts with, the
    // offsets those of its holder during the use.
    private void AddMotion(MotionEvent motion)
    {
        if (!_range.Contains(motion) || UseOf(motion) is not ToolUse use)
        {
            return;
        }

        if (motion.After.Holders.TryGetValue(use.Holder, out HolderSnapshot? holder))
        {
            use.AddOffsets(holder);
        }

        if (motion.Length is not decimal length)
        {
            use.Unknown++;
            return;
        }

        double millimetres = (double)length * CommandedFeed.MillimetresPerUnit(motion);
        if (motion.Verb is not (Verb.Line or Verb.Arc))
        {
            use.Rapid += millimetres;
            return;
        }

        use.Cutting += millimetres;
        if (motion.Feed is decimal feed)
        {
            use.AddFeed(feed, motion.FeedMode);
        }

        if (SpindleSpeed.RpmOf(motion, _options.Machine) is double rpm && rpm > 0)
        {
            use.AddRpm(rpm);
        }
    }

    // The estimated time goes to the use the event stood under.
    private void AddTime(TimedStep step)
    {
        if (_range.Contains(step.Event) && UseOf(step.Event) is ToolUse use)
        {
            use.Seconds += step.Seconds;
        }
    }

    // The block count under a tool runs from the block that brought it in to the block before the one that took it out
    // (virtual machine 7, TOOL_END): a block counts under the tool in the spindle of the holder called last after it
    // (wave-2 question #29). A block is counted when the next one begins, so that its use has begun. The events of one
    // block share one After; a block that raises no event (a LABEL, a COMMENT) is counted in INTERPRETED mode through
    // blocksExecuted (virtual machine 2.7) with the block before it, whose tool is still in the spindle, and is not
    // seen in STATIC mode, which does not count blocksExecuted. FILE=BEGIN and FILE=END are no executed blocks.
    private void CountBlocks(VmEvent vmEvent)
    {
        if (vmEvent is FileEvent || (_block is not null && ReferenceEquals(_block.After, vmEvent.After)))
        {
            return;
        }

        if (_block is not null)
        {
            long unseen = vmEvent.After.Flow.BlocksExecuted - _block.After.Flow.BlocksExecuted - 1;
            CountUnder(_block, 1 + Math.Max(unseen, 0));
        }

        _block = vmEvent;
    }

    private void CountUnder(VmEvent block, long blocks)
    {
        if (_range.Contains(block) && UseOf(block) is ToolUse use)
        {
            use.Blocks += blocks;
        }
    }

    // The use of the tool in the spindle of the holder called last after the event's block (virtual machine 2.3);
    // null when that spindle is empty.
    private ToolUse? UseOf(VmEvent vmEvent)
    {
        if (vmEvent.After.LastHolder is not string holder
            || !_inSpindle.TryGetValue(holder, out ToolUse? use)
            || !vmEvent.After.Holders.TryGetValue(holder, out HolderSnapshot? state))
        {
            return null;
        }

        return state.SpindleTool == use.Tool ? use : null;
    }
}
