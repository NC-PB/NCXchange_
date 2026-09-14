using System.Text;

namespace Ncx.Analytics.ToolVectors;

// The report of the tool vector change: the title, what it measures, the convention it applies with the rotary axes of
// the machine, documented in the report header until the kinematics module exists (implementation 14, P4-03), the
// changes per verb and over all, the histogram, the ten largest changes and the motions skipped and why (virtual
// machine 8).
public sealed partial class ToolVectorAnalytic
{
    private string Write()
    {
        TableFormat format = _options.Format;
        var text = new StringBuilder();
        text.Append(TextTable.Line(ReportText.Title("Tool vector change", _options), format));
        text.Append(TextTable.Line(
            "Per MOTION the angle in degrees between the tool vector at its start and at its end (virtual machine 8): "
            + "TX TY TZ where the program gives them (D81), else the rotary axes of [[axis]] applied to the tool axis.",
            format));
        text.Append(TextTable.Line(
            "The convention until the kinematics module exists (implementation 14, P4-03), not a pose: the tool axis "
            + "is the normal of the WORKPLANE; a rotary axis named A, B or C turns it about the machine X, Y or Z "
            + "axis, in the order of [[axis]]; a head axis turns the tool, a table axis the workpiece, and with it "
            + "the tool vector the other way, while its owner holds the workpiece.",
            format));
        text.Append(TextTable.Line(
            "A bin of the histogram holds the changes from its lower bound up to, not including, its upper bound.",
            format));
        text.Append('\n').Append(AxesText(format));
        text.Append('\n').Append(_statistics.Table(ReportText.Angle).Write(format));
        text.Append('\n').Append(_histogram.Table("change (degrees)").Write(format));
        text.Append('\n').Append(LargestText(format));
        text.Append('\n').Append(TextTable.Line(
            $"Skipped: {ReportText.Count(Skipped)} of {ReportText.Count(Motions)} motions: "
            + $"{ReportText.Count(StartUnknown)} with the tool vector unknown at the start, "
            + $"{ReportText.Count(EndUnknown)} unknown at the end, {ReportText.Count(BetweenFrames)} with a rotary "
            + $"axis from one frame into another, {ReportText.Count(InTransformedPlane)} with a rotary axis that is a "
            + "length of the polar or cylinder plane (virtual machine 3.1, 3.4, 8).",
            format));
        return text.ToString();
    }

    // The rotary axes the convention applies, in its order, so that the reader of the report sees what was turned.
    private string AxesText(TableFormat format)
    {
        if (_convention.Axes.Count == 0)
        {
            return TextTable.Line(
                "Rotary axes: none named A, B or C in [[axis]]; only TX TY TZ change the tool vector, which is "
                + "otherwise the normal of the WORKPLANE.",
                format);
        }

        var table = new TextTable("axis", "id", "about", "turns");
        foreach (ConventionAxis axis in _convention.Axes)
        {
            table.Add(axis.NcxName, axis.Id, axis.About, axis.Turns());
        }

        return table.Write(format);
    }

    // The ten largest changes with their lines and the vectors at the start and the end, the largest first
    // (implementation 14, P4-03); a change of 0 is none of them.
    private string LargestText(TableFormat format)
    {
        if (_largest.Motions.Count == 0)
        {
            return TextTable.Line("No motion in the range changes the tool vector.", format);
        }

        var table = new TextTable("line", "verb", "change (degrees)", "from", "to");
        foreach (WorstMotion motion in _largest.Motions)
        {
            table.Add(ReportText.Count(motion.Line), ReportText.VerbName(motion.Verb), ReportText.Angle(motion.Value),
                motion.Start, motion.End);
        }

        return TextTable.Line("The ten largest changes:", format) + table.Write(format);
    }
}
