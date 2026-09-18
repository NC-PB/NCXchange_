using System.Text;

namespace Ncx.Analytics.Runtime;

// The report of the runtime estimate: the title, the values of the machine file it used, the totals per tool, section
// and channel, what it could not time, and the estimated time of the run (virtual machine 8; implementation 14, P4-02
// and risks).
public sealed partial class RuntimeAnalytic
{
    private string Write()
    {
        TableFormat format = _options.Format;
        MachineDynamics dynamics = _estimator.Dynamics;
        var text = new StringBuilder();

        // The report always says "estimate" and carries the machine file and every value it used, the rotary axes
        // included, which limit a motion in the polar and cylinder plane, so that the maintainer corrects the file
        // instead of the code (implementation 14, P4-02 and risks); without dynamics it says that it falls back to
        // distance over feed (virtual machine 8).
        text.Append(TextTable.Line(ReportText.Title("Runtime estimate", _options), format));
        text.Append(TextTable.Line(
            "An estimate from the values of the machine file, not a measured cycle time (virtual machine 8, D64).",
            format));
        text.Append(TextTable.Line(dynamics.ProfileLine(), format));
        text.Append('\n').Append(dynamics.LinearAxisTable().Write(format));
        text.Append('\n').Append(TextTable.Line(MachineDynamics.RotaryAxisLine(), format));
        text.Append(dynamics.RotaryAxisTable().Write(format));
        text.Append('\n').Append(dynamics.SpindleTable().Write(format));

        // Totals per tool, per section, per channel (virtual machine 8).
        text.Append('\n').Append(Totals("tool", "holder", _tools).Write(format));
        text.Append('\n').Append(Totals("section", "line", _sections).Write(format));
        text.Append('\n').Append(Totals("channel", null, ChannelsInOrder()).Write(format));

        text.Append('\n');
        text.Append(TextTable.Line(
            $"Not timed: {ReportText.Count(_estimator.UnknownLength)} motions of unknown length, "
            + $"{ReportText.Count(_estimator.WithoutSpeed)} motions without a known feed or rapid rate, "
            + $"{ReportText.Count(_estimator.UnknownCycles)} cycle calls whose motions the virtual machine does not "
            + $"know, {ReportText.Count(_estimator.UnknownDwells)} dwells of unknown seconds.",
            format));
        text.Append(TextTable.Line(
            "Not in the estimate: the travel of rotary axes outside the polar and cylinder plane "
            + $"({ReportText.Count(_estimator.RotaryMoves)} motions turned one; the runtime of rotary moves belongs to "
            + "the kinematics module, virtual machine 9), tool changes, waits at marks.",
            format));

        // The time of the run is the longest channel; the job time with the waits at the marks comes with the job
        // scheduler (virtual machine 8, 3.7).
        // TODO: the job time as the longest channel including its waits at the marks, from SYNC_WAIT and SYNC_RELEASE,
        // when the job scheduler raises them (implementation 16, P6-01).
        text.Append(TextTable.Line(
            $"Estimated time: {ReportText.Seconds(TotalSeconds)} s ({ReportText.Clock(TotalSeconds)}), the longest "
            + "channel.",
            format));
        return text.ToString();
    }

    // One table of totals: the name, its detail when it has one, the seconds and the time in hours, minutes and
    // seconds.
    private static TextTable Totals(string name, string? detail, List<TimeTotal> totals)
    {
        TextTable table = detail is null
            ? new TextTable(name, "seconds", "time")
            : new TextTable(name, detail, "seconds", "time");
        foreach (TimeTotal total in totals)
        {
            string seconds = ReportText.Seconds(total.Seconds);
            string clock = ReportText.Clock(total.Seconds);
            if (detail is null)
            {
                table.Add(total.Name, seconds, clock);
            }
            else
            {
                table.Add(total.Name, total.Detail, seconds, clock);
            }
        }

        return table;
    }

    private List<TimeTotal> ChannelsInOrder()
    {
        var channels = new List<int>(_channels.Keys);
        channels.Sort();
        var totals = new List<TimeTotal>();
        foreach (int channel in channels)
        {
            totals.Add(_channels[channel]);
        }

        return totals;
    }
}
