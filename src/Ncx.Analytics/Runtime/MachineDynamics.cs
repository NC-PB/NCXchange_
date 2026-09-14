using Ncx.Core.Machine;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The values of the machine file the runtime estimate uses (virtual machine 8, D64): rapid, max_feed and
/// acceleration of every [[axis]] (machine-config 4), block_time, path_mode and corner_speed of [dynamics]
/// (machine-config 4), accel_time of every spindle table (machine-config 5). The report lists every one of them, so
/// that the maintainer corrects the file instead of the code (implementation 14, risks).
/// </summary>
internal sealed class MachineDynamics
{
    private const double SecondsPerMinute = 60d;

    private readonly MachineConfig _machine;

    /// <summary>
    /// The dynamics of one machine file, or of the built-in default machine of D103, which has none.
    /// </summary>
    public MachineDynamics(MachineConfig machine)
    {
        _machine = machine;
    }

    /// <summary>
    /// True with [dynamics] in the machine file: the trapezoidal profile; without it the estimate falls back to
    /// distance over feed (virtual machine 8).
    /// </summary>
    public bool HasProfile => _machine.Dynamics is not null;

    /// <summary>
    /// block_time: the seconds the control needs per block; null when [dynamics] gives none.
    /// </summary>
    public double? BlockTime => _machine.Dynamics?.BlockTime is decimal seconds ? (double)seconds : null;

    /// <summary>
    /// True for path_mode = "continuous", which carries the speed through corners; "exact_stop", and a [dynamics]
    /// without path_mode, brakes to zero in every block (virtual machine 8).
    /// </summary>
    public bool Continuous => _machine.Dynamics?.PathMode == PathMode.Continuous;

    /// <summary>
    /// corner_speed in mm/s, from the mm/min of the file (machine-config 4); 0 without one, which stops in every
    /// corner.
    /// </summary>
    public double CornerSpeed =>
        _machine.Dynamics?.CornerSpeed is decimal perMinute ? (double)perMinute / SecondsPerMinute : 0;

    /// <summary>
    /// The [[axis]] of an axis name of the program (virtual machine 3.8 rule 3); null for an axis the machine does not
    /// declare.
    /// </summary>
    public AxisDef? Axis(string ncxName)
    {
        return _machine.ResolveAxis(ncxName);
    }

    /// <summary>
    /// accel_time of a spindle, from the [spindle.ROLE] table of a role that names it: the seconds from stop to rpm_max
    /// (machine-config 5); null when no table gives one.
    /// </summary>
    /// <param name="spindle">The resource id of the spindle, S1.</param>
    public double? AccelTime(string spindle)
    {
        foreach (KeyValuePair<string, string> role in _machine.Roles)
        {
            if (role.Value == spindle
                && _machine.SpindleTables.TryGetValue(role.Key, out FunctionTable? table)
                && table.AccelTime is decimal seconds)
            {
                return (double)seconds;
            }
        }

        return null;
    }

    /// <summary>
    /// The line of the report that says how the motions are timed.
    /// </summary>
    public string ProfileLine()
    {
        if (_machine.Dynamics is not DynamicsConfig dynamics)
        {
            return "Motions: distance over feed, RAPID, HOME and RETRACT distance over the rapid rate; the machine has "
                + "no [dynamics] (virtual machine 8).";
        }

        string pathMode = dynamics.PathMode switch
        {
            PathMode.Continuous => "continuous",
            PathMode.ExactStop => "exact_stop",
            _ => "none (exact stop)",
        };
        return $"Motions: trapezoidal profile (virtual machine 8, D64), path_mode {pathMode}, corner_speed "
            + $"{ReportText.Number(dynamics.CornerSpeed)} mm/min, "
            + $"block_time {ReportText.Number(dynamics.BlockTime)} s.";
    }

    /// <summary>
    /// The values of every linear axis of the machine, which the profile limits and accelerates by; rotary axes add no
    /// travel (virtual machine 9).
    /// </summary>
    public TextTable AxisTable()
    {
        var table = new TextTable("axis", "rapid (mm/min)", "max_feed (mm/min)", "acceleration (mm/s^2)");
        foreach (AxisDef axis in _machine.Axes)
        {
            if (axis.Kind == AxisKind.Linear)
            {
                table.Add(axis.NcxName, ReportText.Number(axis.Rapid), ReportText.Number(axis.MaxFeed),
                    ReportText.Number(axis.Acceleration));
            }
        }

        return table;
    }

    /// <summary>
    /// accel_time of every spindle of the machine, which a spindle start or stop adds.
    /// </summary>
    public TextTable SpindleTable()
    {
        var table = new TextTable("spindle", "accel_time (s)");
        foreach (ResourceDef resource in _machine.Resources)
        {
            if (resource.IsSpindle)
            {
                decimal? accelTime = AccelTime(resource.Id) is double seconds ? (decimal)seconds : null;
                table.Add(resource.Id, ReportText.Number(accelTime));
            }
        }

        return table;
    }
}
