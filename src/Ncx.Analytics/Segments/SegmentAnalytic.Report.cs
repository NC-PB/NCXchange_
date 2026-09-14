using System.Text;

namespace Ncx.Analytics.Segments;

// The report of the segment length: the title, what it measures, the lengths per verb and over all, the histogram, the
// ten shortest motions with their lines and the motions skipped and why (virtual machine 8; implementation 14, P4-03).
public sealed partial class SegmentAnalytic
{
    private string Write()
    {
        TableFormat format = _options.Format;
        var text = new StringBuilder();
        text.Append(TextTable.Line(ReportText.Title("Segment length", _options), format));
        text.Append(TextTable.Line(
            "Per MOTION the distance in mm between its start and its end point, the arc length for an ARC (virtual "
            + "machine 8), over the motions of the workpiece frame whose positions are known (implementation 14, "
            + "P4-03).",
            format));
        text.Append(TextTable.Line(
            "A bin of the histogram holds the lengths from its lower bound up to, not including, its upper bound.",
            format));
        text.Append('\n').Append(_statistics.Table(ReportText.Distance).Write(format));
        text.Append('\n').Append(_histogram.Table("length (mm)").Write(format));
        text.Append('\n').Append(ShortestText(format));
        text.Append('\n').Append(TextTable.Line(
            $"Skipped: {ReportText.Count(Skipped)} of {ReportText.Count(Motions)} motions: "
            + $"{ReportText.Count(InMachineFrame)} in the machine frame (HOME, FRAME=MACHINE), "
            + $"{ReportText.Count(FromUnknown)} from an unknown position, {ReportText.Count(ToUnknown)} to an unknown "
            + $"position, {ReportText.Count(BetweenFrames)} from one frame into another, "
            + $"{ReportText.Count(UnresolvedArcs)} arcs that did not resolve (virtual machine 3.4, 8).",
            format));
        return text.ToString();
    }

    // The ten shortest motions with their lines, the shortest first (implementation 14, P4-03).
    private string ShortestText(TableFormat format)
    {
        if (_shortest.Motions.Count == 0)
        {
            return TextTable.Line("No motion in the range has a known length.", format);
        }

        var table = new TextTable("line", "verb", "length (mm)");
        foreach (WorstMotion motion in _shortest.Motions)
        {
            table.Add(ReportText.Count(motion.Line), ReportText.VerbName(motion.Verb),
                ReportText.Distance(motion.Value));
        }

        return TextTable.Line("The ten shortest motions:", format) + table.Write(format);
    }
}
