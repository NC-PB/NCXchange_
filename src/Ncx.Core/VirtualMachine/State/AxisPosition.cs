namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The position of one axis: its coordinate, the frame the coordinate is known in, and whether it is known at all
/// (virtual machine 2.2, 3.4). A small immutable value, replaced whole when the axis moves.
/// </summary>
/// <param name="Value">The coordinate in <paramref name="Frame"/>; meaningless while <paramref name="Known"/> is
/// false.</param>
/// <param name="Frame">The frame the coordinate is known in; UNKNOWN for an axis unknown in every frame.</param>
/// <param name="Known">True when <paramref name="Value"/> is the position of the axis in
/// <paramref name="Frame"/>.</param>
public readonly record struct AxisPosition(decimal Value, PositionFrame Frame, bool Known)
{
    /// <summary>
    /// An axis unknown in every frame: the start position of an axis without home (virtual machine 2.2, 3.4).
    /// </summary>
    public static AxisPosition Unknown { get; } = new(0m, PositionFrame.Unknown, Known: false);
}
