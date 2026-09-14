using System.Text;

namespace Ncx.Analytics.ToolList;

// The report of the tool list: the title, what the columns count, and one row per tool use with a block in the range
// (virtual machine 8; architecture 9).
public sealed partial class ToolListAnalytic
{
    private string Write()
    {
        TableFormat format = _options.Format;
        var text = new StringBuilder();
        text.Append(TextTable.Line(ReportText.Title("Tool list", _options), format));
        text.Append(TextTable.Line(
            "One row per tool use, from TOOL_BEGIN to TOOL_END (virtual machine 8, architecture 9); preloaded: the "
            + "tool was preloaded before the change that brought it in.",
            format));
        text.Append(TextTable.Line(
            "Distances in mm over the motions of known length: cutting for LINE, ARC and the plunges of cycles, rapid "
            + "for RAPID, HOME, RETRACT and the rapid moves of cycles; unknown: the motions of unknown length.",
            format));
        text.Append(TextTable.Line(
            "rpm and feeds: those of the cutting motions; seconds: an estimate (virtual machine 8, D64), as the "
            + "runtime estimate charges it.",
            format));

        var table = new TextTable("tool", "holder", "preloaded", "rpm", "feeds", "offsets", "cutting", "rapid",
            "unknown", "blocks", "seconds");
        foreach (ToolUse use in _uses)
        {
            if (use.Blocks == 0)
            {
                continue;
            }

            table.Add(ReportText.Tool(use.Tool), use.Holder, use.Preloaded ? "yes" : "no", use.RpmText(),
                use.FeedsText(), use.OffsetsText(), ReportText.Distance(use.Cutting), ReportText.Distance(use.Rapid),
                ReportText.Count(use.Unknown), ReportText.Count(use.Blocks), ReportText.Seconds(use.Seconds));
        }

        text.Append('\n').Append(table.Write(format));
        return text.ToString();
    }
}
