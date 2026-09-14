using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The commanded feed of a motion in mm/min (virtual machine 8): per minute as written, or per revolution times the
/// rpm of the spindle; the machine file gives its speeds in mm/min and mm/s^2 (machine-config 4), so an INCH program
/// is converted.
/// </summary>
internal static class CommandedFeed
{
    private const double MillimetresPerInch = 25.4;

    /// <summary>
    /// The millimetres of one unit of the program where the event stands: 25.4 under UNITS=INCH, 1 otherwise.
    /// </summary>
    public static double MillimetresPerUnit(VmEvent vmEvent)
    {
        return vmEvent.After.Frame.Units == Units.Inch ? MillimetresPerInch : 1d;
    }

    /// <summary>
    /// The commanded feed in mm/min: the F of the motion per minute, or per revolution times the rpm (virtual machine
    /// 8); null for a motion without a known feed, and for a feed per revolution while the spindle stands or its speed
    /// is unknown.
    /// </summary>
    public static double? MillimetresPerMinute(MotionEvent motion, MachineConfig machine)
    {
        if (motion.Feed is not decimal feed)
        {
            return null;
        }

        double perMinute = (double)feed * MillimetresPerUnit(motion);
        if (motion.FeedMode == FeedMode.PerMin)
        {
            return perMinute;
        }

        return SpindleSpeed.RpmOf(motion, machine) is double rpm && rpm > 0 ? perMinute * rpm : null;
    }
}
