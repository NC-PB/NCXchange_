using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics;

/// <summary>
/// The statistics of one quantity per verb of the motions, RAPID, LINE, ARC, RETRACT and HOME, and over all of them
/// (virtual machine 7, MOTION; implementation 14, P4-03: the minimum, maximum and mean over the range).
/// </summary>
internal sealed class VerbStatistics
{
    // The verbs of MOTION in the order the rows of a report name them (virtual machine 7).
    private static readonly Verb[] s_verbs = [Verb.Rapid, Verb.Line, Verb.Arc, Verb.Retract, Verb.Home];

    private readonly Dictionary<Verb, ValueStatistics> _byVerb = [];

    /// <summary>
    /// The statistics over the motions of every verb.
    /// </summary>
    public ValueStatistics All { get; } = new();

    /// <summary>
    /// Counts the value of one motion under its verb and under all.
    /// </summary>
    public void Add(Verb verb, double value)
    {
        if (!_byVerb.TryGetValue(verb, out ValueStatistics? statistics))
        {
            statistics = new ValueStatistics();
            _byVerb.Add(verb, statistics);
        }

        statistics.Add(value);
        All.Add(value);
    }

    /// <summary>
    /// The statistics of the motions of one verb; null for a verb without a motion counted.
    /// </summary>
    public ValueStatistics? Of(Verb verb)
    {
        return _byVerb.TryGetValue(verb, out ValueStatistics? statistics) ? statistics : null;
    }

    /// <summary>
    /// The statistics as a table: a row per verb with motions, then the row of all, each with the motions counted and
    /// their minimum, maximum and mean.
    /// </summary>
    /// <param name="format">Writes a value with its decimals: ReportText.Distance, ReportText.Angle.</param>
    public TextTable Table(Func<double, string> format)
    {
        var table = new TextTable("verb", "motions", "minimum", "maximum", "mean");
        foreach (Verb verb in s_verbs)
        {
            if (_byVerb.TryGetValue(verb, out ValueStatistics? statistics))
            {
                AddRow(table, ReportText.VerbName(verb), statistics, format);
            }
        }

        AddRow(table, "all", All, format);
        return table;
    }

    // A row without a motion has no minimum, maximum or mean.
    private static void AddRow(TextTable table, string name, ValueStatistics statistics, Func<double, string> format)
    {
        if (statistics.Count == 0)
        {
            table.Add(name, "0", "-", "-", "-");
            return;
        }

        table.Add(name, ReportText.Count(statistics.Count), format(statistics.Minimum), format(statistics.Maximum),
            format(statistics.Mean));
    }
}
