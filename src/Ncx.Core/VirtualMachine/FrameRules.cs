using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The workpiece frame: ORIGIN followed by the transform chain in program order (language 4.2, virtual machine 2.1,
/// D31), the setpos shifts of SETPOS (3.4, D55, D101), and what a change of the frame does to the position store
/// (3.4, D35, D57).
/// </summary>
internal static class FrameRules
{
    /// <summary>
    /// The state key of the setpos shift of an axis, SETPOS:C, in ChannelState.Unknown (D101).
    /// </summary>
    public static string SetposKey(string axis)
    {
        return BlockContext.StateKey("SETPOS", axis);
    }

    /// <summary>
    /// SHIFT appends an entry to the chain in program order, in the frame that is active where the word stands, and is
    /// folded into the position: newPos = oldPos - shift (language 4.2, virtual machine 2.1, 3.4, D31). Omitted axes
    /// are 0.
    /// </summary>
    /// <param name="state">The channel state.</param>
    /// <param name="shift">The shift of each axis by its key in the position store.</param>
    public static void AppendShift(ChannelState state, IReadOnlyDictionary<string, decimal> shift)
    {
        state.Frame.Chain.Add(new TransformEntry { Kind = TransformKind.Shift, Shift = shift });
        Fold(state, shift, sign: -1m);
    }

    /// <summary>
    /// ROTATE, MIRROR, TILT and TILT_AXIS append an entry to the chain in program order (language 4.2, virtual machine
    /// 2.1, D31, D82); their change marks the position unknown in the new frame, because the kinematics module that
    /// could convert it is not part of the virtual machine (virtual machine 3.4, 10).
    /// </summary>
    public static void AppendTransform(ChannelState state, TransformEntry entry)
    {
        state.Frame.Chain.Add(entry);
        MarkUnknownOutsideMachineFrame(state);
    }

    /// <summary>
    /// The RESET forms cut the chain at the last entry of their kind and everything after it; the chain is unwound
    /// from the end only (language 4.2, virtual machine 2.1, D31). A RESET that removes shifts folds them back; one that
    /// removes a ROTATE, MIRROR, TILT or TILT_AXIS changes the frame and marks the position unknown (3.4). Without an
    /// entry of the kind nothing changes.
    /// </summary>
    public static void Cut(ChannelState state, TransformKind kind)
    {
        // TODO(question): the row SHIFT=RESET of language 4.2 says "Remove the shifts from the chain, and everything
        // appended after them", which with two shifts in the chain reaches back to the first; virtual machine 2.1, the
        // chain paragraph of 4.2 and the phase plan cut at the last entry of the kind, as done here.
        int last = state.Frame.Chain.FindLastIndex(entry => entry.Kind == kind);
        if (last >= 0)
        {
            CutAt(state, last);
        }
    }

    /// <summary>
    /// ORIGIN selects the workpiece datum: it starts an empty chain and clears the setpos shifts (language 4.2,
    /// virtual machine 2.1, 3.4, 4, D31, D55). An axis whose workpiece coordinate came from a setpos shift loses it:
    /// back to the MACHINE frame at its machine position when the shift was recorded against the machine position,
    /// unknown when the shift was unknown (D35, D101).
    /// </summary>
    public static void SelectOrigin(ChannelState state, int origin)
    {
        // TODO(question): virtual machine 3.4 lists the frame changes that mark the position unknown and does not list
        // ORIGIN; without a datum table the coordinates in another datum are not known (D35). An axis known in the
        // workpiece frame of the datum keeps its value here, with the chain removed as by a RESET, until that is
        // answered.
        state.Frame.Origin = origin;
        if (state.Frame.Chain.Count > 0)
        {
            CutAt(state, 0);
        }

        // The next ORIGIN clears the setpos shift, as G54 cancels a G50 or G92 setting (virtual machine 3.4). The
        // chain is folded back above, shifts that stood before a SETPOS included; the record of the SETPOS takes those
        // out again (ReturnToMachineFrame).
        foreach (string axis in new List<string>(state.Frame.SetposShift.Keys))
        {
            state.Frame.SetposShift[axis] = 0m;
            bool shiftUnknown = state.Unknown.Remove(SetposKey(axis));

            // A shift recorded against the machine position: without the shift the axis is known in the MACHINE frame
            // at its machine position and unknown in the workpiece frame, as after HOME or a machine-frame move
            // (virtual machine 3.4, D35, D101).
            if (ReturnToMachineFrame(state, axis))
            {
                continue;
            }

            // An axis known in the MACHINE frame stays known there: the machine frame does not move with the workpiece
            // frame (virtual machine 3.4, D35).
            if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position)
                || !position.Known
                || position.Frame != PositionFrame.Workpiece)
            {
                continue;
            }

            // A shift that is unknown (D101): the store holds the coordinate itself, and the machine position is not
            // known; without the shift the axis is unknown in every frame (D100, D101).
            if (shiftUnknown)
            {
                state.Motion.Position[axis] = AxisPosition.Unknown;
            }
        }
    }

    /// <summary>
    /// SETPOS moves nothing: for the axis it records a setpos shift such that the current position reads as the
    /// declared value, newSetposShift = oldPos - declared; the position store keeps its physical value and the
    /// workpiece coordinates are read through the setpos shift (virtual machine 3.4, D55, D101).
    /// </summary>
    /// <param name="state">The channel state.</param>
    /// <param name="axis">The key of the position store the axis word names.</param>
    /// <param name="declared">The declared value, halved under DIAMETER=ON; null when it is UNKNOWN.</param>
    /// <param name="directlyAfterHomeWithoutReference">True when a HOME of the axis found no reference point in the
    /// configuration and no block has named the axis since (D100, D101).</param>
    /// <param name="block">The SETPOS block, which a diagnostic names.</param>
    /// <param name="diagnostics">Where the ERROR goes.</param>
    public static void Setpos(ChannelState state, string axis, decimal? declared,
        bool directlyAfterHomeWithoutReference, Block block, Diagnostics diagnostics)
    {
        // SETPOS needs the axis known in some frame; directly after a HOME of that axis that found no reference point
        // it is accepted as well; the ERROR remains for every other axis unknown in every frame (D101).
        AxisPosition position = state.Motion.Position[axis];
        if (!position.Known && !directlyAfterHomeWithoutReference)
        {
            diagnostics.Error(block, DiagnosticCodes.SetposAxisUnknown,
                $"SETPOS {axis} needs the axis known in some frame, and it is unknown in every frame; only directly "
                + "after a HOME of the axis without a reference point is that accepted (virtual machine 3.4, D101).");
            return;
        }

        // A declared value from an expression is not evaluated in STATIC mode: the shift is UNKNOWN, and with it the
        // position the axis reads as (virtual machine 1). SETPOS moves nothing: an axis known in the MACHINE frame
        // keeps its machine position, and one whose setpos shift was recorded against the machine position returns to
        // it (virtual machine 3.4, D35, D101).
        string key = SetposKey(axis);
        if (declared is not decimal value)
        {
            state.Unknown.Add(key);
            MarkUnknownOutsideMachineFrame(state, axis);
            return;
        }

        // Known in the workpiece frame through an unknown setpos shift (D101): the store holds the coordinate itself and
        // the physical position is unknown, so newSetposShift = oldPos - declared stays unknown, and the axis reads as
        // the declared value (virtual machine 3.4, D101).
        if (position.Known && position.Frame == PositionFrame.Workpiece && state.Unknown.Contains(key))
        {
            state.Motion.Position[axis] = position with { Value = value };
            return;
        }

        // Known in some frame: newSetposShift = oldPos - declared. Known in the MACHINE frame only (after HOME or a
        // machine-frame move), the shift is recorded against the machine position and the axis becomes known in the
        // workpiece frame with the declared value (D101). The store keeps the machine position; the record keeps the
        // shifts of the chain on the axis, which the store never took in, so that ORIGIN and a change of the frame
        // find the machine position again (ReturnToMachineFrame). A later SETPOS on the axis records against the same
        // store and keeps the record.
        if (position.Known)
        {
            state.Frame.SetposShift[axis] = position.Value - value;
            state.Unknown.Remove(key);
            if (position.Frame == PositionFrame.Machine)
            {
                state.Motion.Position[axis] = position with { Frame = PositionFrame.Workpiece };
                state.Frame.SetposAgainstMachine[axis] = ChainShift(state, axis);
            }

            return;
        }

        // Directly after a HOME that found no reference point: the axis becomes known in the workpiece frame with the
        // declared value, its machine position stays unknown, and so does the shift (D100, D101). There is no physical
        // value for the store to keep, so it holds the declared value, which reads as stored (WorkpieceCoordinate).
        state.Frame.SetposShift[axis] = 0m;
        state.Unknown.Add(key);
        state.Frame.SetposAgainstMachine.Remove(axis);
        state.Motion.Position[axis] = new AxisPosition(value, PositionFrame.Workpiece, Known: true);
    }

    /// <summary>
    /// The coordinate of an axis in the workpiece frame as the program reads it, the stored value through the setpos
    /// shift (virtual machine 3.4); null while the axis is not known in the workpiece frame. Where the shift is unknown
    /// (D101) the store holds the coordinate itself.
    /// </summary>
    public static decimal? WorkpieceCoordinate(ChannelState state, string axis)
    {
        AxisPosition position = state.Motion.Position[axis];
        if (!position.Known || position.Frame != PositionFrame.Workpiece)
        {
            return null;
        }

        if (state.Unknown.Contains(SetposKey(axis)))
        {
            return position.Value;
        }

        return position.Value - state.Frame.SetposShift[axis];
    }

    /// <summary>
    /// The position that stores a coordinate of the workpiece frame as the program writes it: the physical value is
    /// the coordinate plus the setpos shift (virtual machine 3.4); where the shift is unknown (D101) the store holds
    /// the coordinate itself. For the motion rules of P1-03.
    /// </summary>
    public static AxisPosition WorkpiecePosition(ChannelState state, string axis, decimal coordinate)
    {
        decimal shift = state.Unknown.Contains(SetposKey(axis)) ? 0m : state.Frame.SetposShift[axis];
        return new AxisPosition(coordinate + shift, PositionFrame.Workpiece, Known: true);
    }

    /// <summary>
    /// WORKPIECE changes mark the position unknown in the new holder's frame until the next ORIGIN or motion with known
    /// coordinates; the frame of a holder is its own right-handed frame with +Z out of its chuck (virtual machine 3.4,
    /// D57). Selecting the holder that already holds the part changes nothing.
    /// </summary>
    public static void SelectWorkpiece(ChannelState state, string holder)
    {
        if (state.Frame.WorkpieceHolder == holder)
        {
            return;
        }

        state.Frame.WorkpieceHolder = holder;
        MarkUnknownOutsideMachineFrame(state);
    }

    /// <summary>
    /// POLAR=OFF and CYLINDER=OFF leave the axes of the transformation unknown in the workpiece frame until the next
    /// motion with known coordinates (virtual machine 3.4, D35, D102): a position known in the polar or cylinder frame
    /// becomes unknown.
    /// </summary>
    public static void LeaveTransformation(ChannelState state, PositionFrame frame)
    {
        foreach (string axis in new List<string>(state.Motion.Position.Keys))
        {
            if (state.Motion.Position[axis].Known && state.Motion.Position[axis].Frame == frame)
            {
                state.Motion.Position[axis] = AxisPosition.Unknown;
            }
        }
    }

    /// <summary>
    /// A change of the frame marks the position unknown in the new frame (virtual machine 3.4, D35, D101); see the
    /// overload for one axis.
    /// </summary>
    public static void MarkUnknownOutsideMachineFrame(ChannelState state)
    {
        foreach (string axis in new List<string>(state.Motion.Position.Keys))
        {
            MarkUnknownOutsideMachineFrame(state, axis);
        }
    }

    /// <summary>
    /// The frame of an axis changes while nothing moves: known in the workpiece, polar or cylinder frame, it becomes
    /// unknown; known in the MACHINE frame, it stays known there, because the machine frame does not move with the
    /// workpiece frame; with a setpos shift recorded against the machine position, it returns to that position in the
    /// MACHINE frame (virtual machine 3.4, D35, D101).
    /// </summary>
    public static void MarkUnknownOutsideMachineFrame(ChannelState state, string axis)
    {
        if (ReturnToMachineFrame(state, axis))
        {
            return;
        }

        if (state.Motion.Position.TryGetValue(axis, out AxisPosition position)
            && position.Known
            && position.Frame != PositionFrame.Machine)
        {
            state.Motion.Position[axis] = AxisPosition.Unknown;
        }
    }

    // D101: the setpos shift of the axis was recorded against its machine position, and the store kept that position.
    // Since then Fold has taken the SHIFT entries appended to the chain out of the store and given the removed ones
    // back, the ones that stood before the SETPOS among them; the record holds the sum of those. So the machine
    // position is the store plus the shifts of the chain on the axis minus the record. The axis returns to the MACHINE
    // frame there and leaves the record. False, with the record dropped, for an axis no longer known in the workpiece
    // frame, and for one without a record (virtual machine 3.4, D35, D101).
    private static bool ReturnToMachineFrame(ChannelState state, string axis)
    {
        if (!state.Frame.SetposAgainstMachine.Remove(axis, out decimal shiftAtSetpos)
            || !state.Motion.Position.TryGetValue(axis, out AxisPosition position)
            || !position.Known
            || position.Frame != PositionFrame.Workpiece)
        {
            return false;
        }

        decimal machine = position.Value + ChainShift(state, axis) - shiftAtSetpos;
        state.Motion.Position[axis] = new AxisPosition(machine, PositionFrame.Machine, Known: true);
        return true;
    }

    // The chain is unwound from the end: the entry at the index and everything after it go; removed shifts are folded
    // back into the position, and a removed rotation, mirror or tilt marks the position unknown (virtual machine 3.4).
    private static void CutAt(ChannelState state, int index)
    {
        List<TransformEntry> chain = state.Frame.Chain;
        List<TransformEntry> removed = chain.GetRange(index, chain.Count - index);
        chain.RemoveRange(index, chain.Count - index);

        bool frameTurned = false;
        foreach (TransformEntry entry in removed)
        {
            if (entry.Kind == TransformKind.Shift)
            {
                Fold(state, entry.Shift, sign: 1m);
            }
            else
            {
                frameTurned = true;
            }
        }

        if (frameTurned)
        {
            MarkUnknownOutsideMachineFrame(state);
        }
    }

    // The sum of the SHIFT entries of the chain on an axis: what Fold has taken out of a store that was known outside
    // the MACHINE frame all along (virtual machine 3.4).
    private static decimal ChainShift(ChannelState state, string axis)
    {
        decimal sum = 0m;
        foreach (TransformEntry entry in state.Frame.Chain)
        {
            if (entry.Kind == TransformKind.Shift && entry.Shift.TryGetValue(axis, out decimal shift))
            {
                sum += shift;
            }
        }

        return sum;
    }

    // newPos = oldPos - shift for a shift appended, oldPos + shift for one removed, on every axis known outside the
    // MACHINE frame; the machine frame does not move with the workpiece frame (virtual machine 3.4).
    private static void Fold(ChannelState state, IReadOnlyDictionary<string, decimal> shift, decimal sign)
    {
        foreach (KeyValuePair<string, decimal> axisShift in shift)
        {
            if (!state.Motion.Position.TryGetValue(axisShift.Key, out AxisPosition position)
                || !position.Known
                || position.Frame == PositionFrame.Machine)
            {
                continue;
            }

            state.Motion.Position[axisShift.Key] = position with { Value = position.Value + (sign * axisShift.Value) };
        }
    }
}
