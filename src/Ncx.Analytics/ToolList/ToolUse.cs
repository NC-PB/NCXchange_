using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.ToolList;

/// <summary>
/// One row of the tool list: one use of a tool, from the TOOL_BEGIN that brings it into the spindle of its holder to
/// the TOOL_END that takes it out, with its rpm, feeds, offsets, distances, blocks, estimated time and whether it was
/// preloaded (virtual machine 8; architecture 9: one row per tool use).
/// </summary>
internal sealed class ToolUse
{
    private readonly List<int> _lengthOffsets = [];
    private readonly List<int> _radiusOffsets = [];
    private readonly List<int> _combinedOffsets = [];
    private decimal? _perMinuteLowest;
    private decimal? _perMinuteHighest;
    private decimal? _perRevolutionLowest;
    private decimal? _perRevolutionHighest;
    private double? _rpmLowest;
    private double? _rpmHighest;

    /// <summary>
    /// The tool, a number or a name (language 4.4).
    /// </summary>
    public required ToolRef Tool { get; init; }

    /// <summary>
    /// The resource id of its holder.
    /// </summary>
    public required string Holder { get; init; }

    /// <summary>
    /// True when the tool was preloaded before the change that brought it in (virtual machine 3.5, 8).
    /// </summary>
    public required bool Preloaded { get; init; }

    /// <summary>
    /// The cutting distance in mm: LINE, ARC and the plunges of the cycles (implementation 14, P4-02).
    /// </summary>
    public double Cutting { get; set; }

    /// <summary>
    /// The rapid distance in mm: RAPID, HOME, RETRACT and the rapid moves of the cycles.
    /// </summary>
    public double Rapid { get; set; }

    /// <summary>
    /// The motions of unknown length, which are in neither distance.
    /// </summary>
    public int Unknown { get; set; }

    /// <summary>
    /// The blocks executed under the tool (virtual machine 7, TOOL_END).
    /// </summary>
    public long Blocks { get; set; }

    /// <summary>
    /// The estimated seconds under the tool (virtual machine 8).
    /// </summary>
    public double Seconds { get; set; }

    /// <summary>
    /// Records the feed of a cutting motion.
    /// </summary>
    public void AddFeed(decimal feed, FeedMode mode)
    {
        if (mode == FeedMode.PerMin)
        {
            _perMinuteLowest = Math.Min(_perMinuteLowest ?? feed, feed);
            _perMinuteHighest = Math.Max(_perMinuteHighest ?? feed, feed);
        }
        else
        {
            _perRevolutionLowest = Math.Min(_perRevolutionLowest ?? feed, feed);
            _perRevolutionHighest = Math.Max(_perRevolutionHighest ?? feed, feed);
        }
    }

    /// <summary>
    /// Records the rpm of a cutting motion.
    /// </summary>
    public void AddRpm(double rpm)
    {
        _rpmLowest = Math.Min(_rpmLowest ?? rpm, rpm);
        _rpmHighest = Math.Max(_rpmHighest ?? rpm, rpm);
    }

    /// <summary>
    /// Records the offset registers of the holder during a motion (virtual machine 2.3); register 0 is none.
    /// </summary>
    public void AddOffsets(HolderSnapshot holder)
    {
        AddRegister(_lengthOffsets, holder.OffsetLen);
        AddRegister(_radiusOffsets, holder.OffsetRad);
        AddRegister(_combinedOffsets, holder.OffsetCombined);
    }

    /// <summary>
    /// The rpm range of the cutting motions, "1592" or "636-3000"; "-" without one.
    /// </summary>
    public string RpmText()
    {
        if (_rpmLowest is not double lowest || _rpmHighest is not double highest)
        {
            return "-";
        }

        string low = Math.Round(lowest).ToString(CultureInfo.InvariantCulture);
        string high = Math.Round(highest).ToString(CultureInfo.InvariantCulture);
        return low == high ? low : low + "-" + high;
    }

    /// <summary>
    /// The feeds of the cutting motions per feed mode: "2387 PER_MIN, 1.5 PER_REV", "200-800 PER_MIN"; "-" without one.
    /// </summary>
    public string FeedsText()
    {
        var feeds = new List<string>();
        if (_perMinuteLowest is decimal perMinute && _perMinuteHighest is decimal perMinuteHighest)
        {
            feeds.Add(Span(perMinute, perMinuteHighest) + " PER_MIN");
        }

        if (_perRevolutionLowest is decimal perRevolution && _perRevolutionHighest is decimal perRevolutionHighest)
        {
            feeds.Add(Span(perRevolution, perRevolutionHighest) + " PER_REV");
        }

        return feeds.Count == 0 ? "-" : string.Join(", ", feeds);
    }

    /// <summary>
    /// The offset registers used, by the words that set them: "LEN 1, RAD 1", "OFFSET 5"; "-" without one.
    /// </summary>
    public string OffsetsText()
    {
        var offsets = new List<string>();
        AddText(offsets, "LEN", _lengthOffsets);
        AddText(offsets, "RAD", _radiusOffsets);
        AddText(offsets, "OFFSET", _combinedOffsets);
        return offsets.Count == 0 ? "-" : string.Join(", ", offsets);
    }

    private static void AddRegister(List<int> registers, int register)
    {
        if (register != 0 && !registers.Contains(register))
        {
            registers.Add(register);
        }
    }

    private static void AddText(List<string> offsets, string word, List<int> registers)
    {
        if (registers.Count == 0)
        {
            return;
        }

        var numbers = new List<string>();
        foreach (int register in registers)
        {
            numbers.Add(register.ToString(CultureInfo.InvariantCulture));
        }

        offsets.Add(word + " " + string.Join(" ", numbers));
    }

    private static string Span(decimal lowest, decimal highest)
    {
        string low = ReportText.Number(lowest);
        string high = ReportText.Number(highest);
        return low == high ? low : low + "-" + high;
    }
}
