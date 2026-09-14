namespace Ncx.Analytics.Runtime;

/// <summary>
/// One row of a total of the runtime estimate: a tool with its holder, a SECTION with the line it first stands on, or a
/// channel, and the seconds charged to it (virtual machine 8: totals per tool, per section, per channel).
/// </summary>
internal sealed class TimeTotal
{
    /// <summary>
    /// The tool, the text of the SECTION, or the channel.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The holder of a tool, the line of a SECTION; empty for a channel.
    /// </summary>
    public string Detail { get; init; } = "";

    /// <summary>
    /// The seconds charged so far.
    /// </summary>
    public double Seconds { get; set; }
}
