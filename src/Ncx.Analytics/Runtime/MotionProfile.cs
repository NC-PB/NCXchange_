namespace Ncx.Analytics.Runtime;

/// <summary>
/// The trapezoidal velocity profile of one motion (virtual machine 8, D64): speeds in mm/s, lengths in mm,
/// acceleration in mm/s^2, the time in seconds.
/// </summary>
internal static class MotionProfile
{
    /// <summary>
    /// The seconds a motion takes under the trapezoidal profile.
    /// </summary>
    /// <param name="length">The length of the motion in mm.</param>
    /// <param name="speed">The speed it travels at, the commanded feed after its limits.</param>
    /// <param name="entry">The speed it enters with: 0 after a stop, the corner speed in continuous mode.</param>
    /// <param name="exit">The speed it leaves with.</param>
    /// <param name="acceleration">The acceleration and deceleration; null when the machine file gives none, and the
    /// motion then travels at its speed from start to end.</param>
    public static double Seconds(double length, double speed, double entry, double exit, double? acceleration)
    {
        if (length <= 0 || speed <= 0)
        {
            return 0;
        }

        if (acceleration is not double rate || rate <= 0)
        {
            return length / speed;
        }

        // The block accelerates from its entry speed to its speed, travels at it, and decelerates to its exit speed,
        // with deceleration equal to acceleration (virtual machine 8; D64).
        double start = Math.Min(entry, speed);
        double end = Math.Min(exit, speed);
        double accelerating = ((speed * speed) - (start * start)) / (2 * rate);
        double decelerating = ((speed * speed) - (end * end)) / (2 * rate);
        if (accelerating + decelerating <= length)
        {
            double travelling = length - accelerating - decelerating;
            return ((speed - start) / rate) + ((speed - end) / rate) + (travelling / speed);
        }

        // Too short to reach its speed: the block accelerates to the highest speed it can reach and decelerates at
        // once, a triangle instead of a trapezoid.
        double peak = Math.Sqrt(((2 * rate * length) + (start * start) + (end * end)) / 2);
        if (peak >= Math.Max(start, end))
        {
            return ((peak - start) / rate) + ((peak - end) / rate);
        }

        // Too short even to change from the entry to the exit speed: the speed changes evenly over the length.
        return 2 * length / (start + end);
    }
}
