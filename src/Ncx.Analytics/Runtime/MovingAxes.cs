using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The axes a motion moves and the limits they set (virtual machine 8): the smallest rapid, the smallest max_feed and
/// the smallest acceleration of the linear axes that move, and whether a rotary axis turned.
/// </summary>
internal sealed record MovingAxes
{
    /// <summary>
    /// The smallest rapid of the moving linear axes that give one, in mm/min; null when none does.
    /// </summary>
    public double? Rapid { get; init; }

    /// <summary>
    /// The smallest max_feed of the moving linear axes that give one, in mm/min; null when none does.
    /// </summary>
    public double? MaxFeed { get; init; }

    /// <summary>
    /// The smallest acceleration of the moving linear axes that give one, in mm/s^2; null when none does.
    /// </summary>
    public double? Acceleration { get; init; }

    /// <summary>
    /// True when a rotary axis turned in the motion.
    /// </summary>
    public bool TurnsRotary { get; init; }

    /// <summary>
    /// The moving axes of a motion: every axis whose position differs between From and To, and both plane axes of an
    /// ARC that turns about its center.
    /// </summary>
    public static MovingAxes Of(MotionEvent motion, MachineDynamics dynamics)
    {
        double? rapid = null;
        double? maxFeed = null;
        double? acceleration = null;
        bool turnsRotary = false;
        foreach (KeyValuePair<string, AxisPosition> target in motion.To)
        {
            AxisPosition start = motion.From.TryGetValue(target.Key, out AxisPosition known)
                ? known
                : AxisPosition.Unknown;
            if (start == target.Value && !TurnsAbout(motion, target.Key))
            {
                continue;
            }

            // The commanded feed is limited by the max_feed of every axis that moves, and the block accelerates with
            // the smallest acceleration of the moving axes (virtual machine 8). A rotary axis turns in degrees: the
            // runtime of rotary moves belongs to the kinematics module (virtual machine 9), so it sets no limit here,
            // except in the polar and the cylinder plane, where its word is a length like the others (virtual machine
            // 3.1, D102).
            AxisDef? axis = dynamics.Axis(target.Key);
            if (axis?.Kind == AxisKind.Rotary && !(InPlane(start) && InPlane(target.Value)))
            {
                turnsRotary = true;
                continue;
            }

            rapid = Smaller(rapid, axis?.Rapid);
            maxFeed = Smaller(maxFeed, axis?.MaxFeed);
            acceleration = Smaller(acceleration, axis?.Acceleration);
        }

        return new MovingAxes
        {
            Rapid = rapid,
            MaxFeed = maxFeed,
            Acceleration = acceleration,
            TurnsRotary = turnsRotary,
        };
    }

    // An ARC that resolved turns about its center on both axes of its plane, also where one of them ends on the
    // coordinate it started from: on both for a full circle, start = end, and on one for a half circle (virtual machine
    // 3.2). Its center names the two plane axes (virtual machine 7); the arc has moved when its sweep is not 0.
    private static bool TurnsAbout(MotionEvent motion, string axis)
    {
        return motion.Verb == Verb.Arc
            && motion.Sweep is decimal sweep && sweep != 0
            && motion.Center is not null && motion.Center.ContainsKey(axis);
    }

    private static bool InPlane(AxisPosition position)
    {
        return position.Frame is PositionFrame.Polar or PositionFrame.Cylinder;
    }

    // The smaller of a limit found so far and the value of one more axis; an axis without the value sets no limit.
    private static double? Smaller(double? found, decimal? value)
    {
        if (value is not decimal given)
        {
            return found;
        }

        return found is double limit ? Math.Min(limit, (double)given) : (double)given;
    }
}
