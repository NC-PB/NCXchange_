using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Analytics;

/// <summary>
/// How the reports write their numbers and their title: distances with the three decimals of a position, seconds with
/// one decimal and as hours, minutes and seconds, always with the invariant culture (code-guidelines 3.4).
/// </summary>
internal static class ReportText
{
    // A distance keeps the three decimals of a position in millimetres (MotionRules.PositionDecimals of the virtual
    // machine), without trailing zeros.
    private const string DistanceFormat = "0.###";

    // Seconds with one decimal: an estimate is no more exact than that.
    private const string SecondsFormat = "0.0";

    private const string NumberFormat = "0.############";

    /// <summary>
    /// The first line of a report: what it is, of which file, over which range, on which machine, from which run:
    /// "Runtime estimate: 3D_FRAESEN.ncx, the whole file, on fanuc-mill-30i.toml, INTERPRETED run".
    /// </summary>
    public static string Title(string analytic, AnalyticOptions options)
    {
        string mode = options.Mode == ExecutionMode.Interpreted ? "INTERPRETED" : "STATIC";
        return $"{analytic}: {options.FileName}, {options.Range.Describe()}, on {options.MachineName}, {mode} run";
    }

    /// <summary>
    /// A distance in millimetres: 550.856.
    /// </summary>
    public static string Distance(double millimetres)
    {
        return millimetres.ToString(DistanceFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A time in seconds with one decimal: 12.3.
    /// </summary>
    public static string Seconds(double seconds)
    {
        return seconds.ToString(SecondsFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A time as hours, minutes and whole seconds: 0:05:24.
    /// </summary>
    public static string Clock(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Round(seconds));
        return string.Create(CultureInfo.InvariantCulture,
            $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}");
    }

    /// <summary>
    /// A value of the machine file or the program as it is written, without trailing zeros: 2387, 1.5; "none" when the
    /// file gives none.
    /// </summary>
    public static string Number(decimal? value)
    {
        return value is decimal number ? number.ToString(NumberFormat, CultureInfo.InvariantCulture) : "none";
    }

    /// <summary>
    /// A count: 4.
    /// </summary>
    public static string Count(long count)
    {
        return count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A tool as NCX writes it, 1 or "FRAESER"; "none" for the empty spindle, tool 0 (language 4.4).
    /// </summary>
    public static string Tool(ToolRef tool)
    {
        return tool == new ToolRef(0) ? "none" : tool.ToString();
    }
}
