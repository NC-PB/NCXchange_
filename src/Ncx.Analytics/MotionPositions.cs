using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics;

/// <summary>
/// The position of an axis where a motion starts and where it ends, and why the two do not make a known change
/// (virtual machine 2.2, 3.4, 7).
/// </summary>
internal static class MotionPositions
{
    /// <summary>
    /// The position of an axis where the motion starts; unknown for an axis the position store does not hold.
    /// </summary>
    public static AxisPosition StartOf(MotionEvent motion, string axis)
    {
        return motion.From.TryGetValue(axis, out AxisPosition position) ? position : AxisPosition.Unknown;
    }

    /// <summary>
    /// The position of an axis where the motion ends; unknown for an axis the position store does not hold.
    /// </summary>
    public static AxisPosition EndOf(MotionEvent motion, string axis)
    {
        return motion.To.TryGetValue(axis, out AxisPosition position) ? position : AxisPosition.Unknown;
    }

    /// <summary>
    /// Why an axis does not make a known change from its start to its end: none when it stands still or is known at
    /// both ends in one frame, the rule by which a MOTION has a length (virtual machine 7, 8).
    /// </summary>
    public static PositionGap Gap(AxisPosition start, AxisPosition end)
    {
        if (start == end)
        {
            return PositionGap.None;
        }

        if (!start.Known)
        {
            return PositionGap.StartUnknown;
        }

        if (!end.Known)
        {
            return PositionGap.EndUnknown;
        }

        return start.Frame == end.Frame ? PositionGap.None : PositionGap.TwoFrames;
    }

    /// <summary>
    /// True in the polar and the cylinder frame, where the word of a rotary axis is a length and no angle (virtual
    /// machine 3.1, D102).
    /// </summary>
    public static bool InTransformedPlane(AxisPosition position)
    {
        return position.Frame is PositionFrame.Polar or PositionFrame.Cylinder;
    }
}
