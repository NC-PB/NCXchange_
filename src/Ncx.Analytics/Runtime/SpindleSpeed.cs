using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The speed of the spindle a motion's feed per revolution follows (virtual machine 8): the rpm of RPM, or under CSS
/// the rpm that VC gives at the current X radius, capped by RPM_MAX.
/// </summary>
internal static class SpindleSpeed
{
    private const double MetresPerFoot = 0.3048;
    private const double MillimetresPerMetre = 1000d;

    /// <summary>
    /// The spindle of the tool: the spindle of the holder of the last TOOL, the default spindle for a holder without
    /// one, as virtual machine 5 reads the spindle of the current tool holder; null when the machine has none.
    /// </summary>
    // TODO(question): virtual machine 8 multiplies a feed per revolution by "rpm" without naming the spindle. It is the
    // spindle of the current tool holder, as virtual machine 5 checks it before a LINE, which on a turret with a driven
    // tool spindle (millturn1.toml) is that spindle and not the work spindle a turning tool cuts on: the case of wave-2
    // question #46, until that is answered.
    public static string? SpindleOf(VmEvent vmEvent, MachineConfig machine)
    {
        string? holderSpindle = vmEvent.After.LastHolder is string holder
            ? machine.FindResource(holder)?.Spindle
            : null;
        return holderSpindle ?? machine.ResolveDefaultSpindle()?.Id;
    }

    /// <summary>
    /// The rpm of the tool's spindle during a motion: 0 while it stands, the rpm of RPM while it turns, under CSS the
    /// rpm from VC and the X radius capped by RPM_MAX (virtual machine 8); null when a value it needs is unknown.
    /// </summary>
    public static double? RpmOf(MotionEvent motion, MachineConfig machine)
    {
        ChannelSnapshot state = motion.After;
        if (SpindleOf(motion, machine) is not string spindle
            || !state.Spindles.TryGetValue(spindle, out SpindleSnapshot? speed))
        {
            return null;
        }

        if (speed.Direction == SpindleDirection.Off)
        {
            return 0;
        }

        if (!speed.Css)
        {
            return state.Unknown.Contains("RPM:" + spindle) ? null : (double)speed.Rpm;
        }

        return CssRpm(motion, spindle, speed);
    }

    // Under CSS the rpm follows from VC and the current X radius, capped by RPM_MAX (virtual machine 8): VC in m/min,
    // or ft/min under UNITS=INCH (language 4.11), over the circumference at the radius, which the virtual machine
    // stores (D28, D60).
    // TODO(question): virtual machine 8 takes "the current X radius" without saying where along a motion that changes X
    // (a facing cut); the radius is the one the motion starts at, until that is answered.
    private static double? CssRpm(MotionEvent motion, string spindle, SpindleSnapshot speed)
    {
        ChannelSnapshot state = motion.After;
        if (speed.Vc is not decimal cuttingSpeed || state.Unknown.Contains("VC:" + spindle))
        {
            return null;
        }

        double? limit = speed.RpmMax is decimal rpmMax && !state.Unknown.Contains("RPM_MAX:" + spindle)
            ? (double)rpmMax
            : null;
        if (!motion.From.TryGetValue("X", out AxisPosition x)
            || !x.Known
            || x.Frame is not (PositionFrame.Workpiece or PositionFrame.Polar))
        {
            return null;
        }

        double radius = Math.Abs((double)x.Value) * CommandedFeed.MillimetresPerUnit(motion);
        if (radius <= 0)
        {
            return limit;
        }

        double metresPerMinute = state.Frame.Units == Units.Inch
            ? (double)cuttingSpeed * MetresPerFoot
            : (double)cuttingSpeed;
        double rpm = metresPerMinute * MillimetresPerMetre / (2 * Math.PI * radius);
        return limit is double cap ? Math.Min(rpm, cap) : rpm;
    }
}
