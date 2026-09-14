using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The runtime estimate (virtual machine 8, D64; architecture 9): per motion a trapezoidal velocity profile from the
/// machine configuration, the dwells and the plunges and dwells of the expanded cycles, the spindle starts and stops;
/// totals per tool, per SECTION and per channel, and the time of the run as the longest channel. The report always says
/// that it is an estimate.
/// </summary>
public sealed partial class RuntimeAnalytic : IAnalytic
{
    // The time of a program before its first SECTION.
    private const string BeforeFirstSection = "(before the first SECTION)";

    private readonly AnalyticOptions _options;
    private readonly RangeFilter _range;
    private readonly RuntimeEstimator _estimator;

    // The totals in the order they first got time, and by their keys.
    private readonly List<TimeTotal> _tools = [];
    private readonly Dictionary<string, TimeTotal> _toolsByKey = new(StringComparer.Ordinal);
    private readonly List<TimeTotal> _sections = [];
    private readonly Dictionary<string, TimeTotal> _sectionsByText = new(StringComparer.Ordinal);
    private readonly Dictionary<int, TimeTotal> _channels = [];

    // The line of the first SECTION of each text, which its row shows.
    private readonly Dictionary<string, int> _sectionLines = new(StringComparer.Ordinal);

    private bool _finished;

    /// <summary>
    /// The runtime estimate of one run.
    /// </summary>
    /// <param name="options">The machine, the names, the block range and the format of the run.</param>
    public RuntimeAnalytic(AnalyticOptions options)
    {
        _options = options;
        _range = new RangeFilter(options.Range);
        _estimator = new RuntimeEstimator(options.Machine, _range, Add);
    }

    /// <inheritdoc/>
    public BlockRange Range => _options.Range;

    /// <summary>
    /// The estimated time of the run in seconds: the longest channel (virtual machine 8); final after Report.
    /// </summary>
    internal double TotalSeconds
    {
        get
        {
            double longest = 0;
            foreach (TimeTotal channel in _channels.Values)
            {
                longest = Math.Max(longest, channel.Seconds);
            }

            return longest;
        }
    }

    /// <inheritdoc/>
    public void On(VmEvent vmEvent)
    {
        _range.On(vmEvent);
        if (vmEvent is SectionEvent section)
        {
            _sectionLines.TryAdd(section.Text, section.Block.Line);
        }

        _estimator.On(vmEvent);
    }

    /// <inheritdoc/>
    public string Report()
    {
        if (!_finished)
        {
            _estimator.Finish();
            _finished = true;
        }

        return Write();
    }

    /// <summary>
    /// The seconds charged to a tool, as NCX writes it: "1"; 0 for a tool that got none. Final after Report.
    /// </summary>
    internal double SecondsOfTool(string tool)
    {
        double seconds = 0;
        foreach (TimeTotal total in _tools)
        {
            seconds += total.Name == tool ? total.Seconds : 0;
        }

        return seconds;
    }

    /// <summary>
    /// The seconds charged to the SECTION of a text; 0 for a text that got none. Final after Report.
    /// </summary>
    internal double SecondsOfSection(string text)
    {
        return _sectionsByText.TryGetValue(text, out TimeTotal? total) ? total.Seconds : 0;
    }

    // Totals per tool, per section, per channel (virtual machine 8), over the blocks of the range (D67). A time counts
    // under the tool of the holder called last, as the distance under a tool does (virtual machine 7; wave-2 question
    // #29), and under the SECTION it stood in.
    private void Add(TimedStep step)
    {
        VmEvent vmEvent = step.Event;
        if (!_range.Contains(vmEvent))
        {
            return;
        }

        if (!_channels.TryGetValue(vmEvent.Channel, out TimeTotal? channel))
        {
            channel = new TimeTotal { Name = ReportText.Count(vmEvent.Channel) };
            _channels.Add(vmEvent.Channel, channel);
        }

        channel.Seconds += step.Seconds;
        ToolTotal(vmEvent).Seconds += step.Seconds;
        SectionTotal(step.Section).Seconds += step.Seconds;
    }

    private TimeTotal ToolTotal(VmEvent vmEvent)
    {
        string holder = vmEvent.After.LastHolder ?? "";
        string tool = vmEvent.After.Holders.TryGetValue(holder, out HolderSnapshot? state)
            ? ReportText.Tool(state.SpindleTool)
            : "none";
        string key = holder + " " + tool;
        if (!_toolsByKey.TryGetValue(key, out TimeTotal? total))
        {
            total = new TimeTotal { Name = tool, Detail = holder };
            _toolsByKey.Add(key, total);
            _tools.Add(total);
        }

        return total;
    }

    private TimeTotal SectionTotal(string? section)
    {
        string text = section ?? BeforeFirstSection;
        if (!_sectionsByText.TryGetValue(text, out TimeTotal? total))
        {
            string line = section is not null && _sectionLines.TryGetValue(section, out int first)
                ? ReportText.Count(first)
                : "";
            total = new TimeTotal { Name = text, Detail = line };
            _sectionsByText.Add(text, total);
            _sections.Add(total);
        }

        return total;
    }
}
