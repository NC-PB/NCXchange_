using System.Globalization;

namespace Ncx.Analytics;

/// <summary>
/// The block range of an analytic: NCX line numbers of the file, --from 380 --to 600, both lines included, so that a
/// segment analysis can look at one operation instead of the whole program; for a job, per channel file (virtual
/// machine 8, D67). Either end may stay open: --from alone runs to the end of the file, --to alone from its start.
/// </summary>
public sealed record BlockRange
{
    /// <summary>
    /// The whole file: neither --from nor --to.
    /// </summary>
    public static BlockRange Whole { get; } = new();

    /// <summary>
    /// --from: the first line of the range; null for the start of the file.
    /// </summary>
    public int? From { get; init; }

    /// <summary>
    /// --to: the last line of the range; null for the end of the file.
    /// </summary>
    public int? To { get; init; }

    /// <summary>
    /// True for the whole file.
    /// </summary>
    public bool IsWhole => From is null && To is null;

    /// <summary>
    /// True when the NCX line lies in the range, both ends included.
    /// </summary>
    /// <param name="line">The 1-based line of a block; a generated block has the line of its origin.</param>
    public bool Contains(int line)
    {
        bool afterFrom = From is not int from || line >= from;
        bool beforeTo = To is not int to || line <= to;
        return afterFrom && beforeTo;
    }

    /// <summary>
    /// The range as a report names it: "lines 380 to 600", "from line 380", "to line 600", "the whole file".
    /// </summary>
    public string Describe()
    {
        if (From is int from && To is int to)
        {
            return string.Create(CultureInfo.InvariantCulture, $"lines {from} to {to}");
        }

        if (From is int first)
        {
            return string.Create(CultureInfo.InvariantCulture, $"from line {first}");
        }

        if (To is int last)
        {
            return string.Create(CultureInfo.InvariantCulture, $"to line {last}");
        }

        return "the whole file";
    }
}
