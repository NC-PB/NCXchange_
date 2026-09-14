using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The workpiece frame: ORIGIN followed by the transform chain in program order (language 4.2, virtual machine 2.1,
/// D31), the setpos shifts of SETPOS (3.4, D55, D101), what a change of the frame does to the position store (3.4,
/// D35, D57), and what a motion does to the machine position that the record of a setpos shift gives (3.4, D101).
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
    /// are 0. A shift from an expression is not evaluated in STATIC mode, and the entry holds it as UNKNOWN (virtual
    /// machine 1, the answer of wave-1 question #100): it cannot be folded, so the workpiece coordinate of its axis is
    /// unknown. Nothing moves, so an axis known in the MACHINE frame stays there, and one whose setpos shift was
    /// recorded against the machine position returns to that position (virtual machine 3.4, D35, D101).
    /// </summary>
    /// <param name="state">The channel state.</param>
    /// <param name="shift">The shift of each axis by its key in the position store; null for a shift from an
    /// expression.</param>
    public static void AppendShift(ChannelState state, IReadOnlyDictionary<string, decimal?> shift)
    {
        state.Frame.Chain.Add(new TransformEntry { Kind = TransformKind.Shift, Shift = shift });
        Fold(state, shift, sign: -1m);
        foreach (string axis in UnknownAxesOf(shift))
        {
            MarkUnknownOutsideMachineFrame(state, axis);
        }
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
    /// from the end only (language 4.2, virtual machine 2.1, D31). A RESET that removes shifts folds them back; one
    /// that removes a ROTATE, MIRROR, TILT or TILT_AXIS changes the frame and marks the position unknown (3.4). Without
    /// an entry of the kind nothing changes.
    /// </summary>
    public static void Cut(ChannelState state, TransformKind kind)
    {
        // With two shifts in the chain SHIFT=RESET cuts at the last one: a RESET of a kind removes that entry and
        // everything after it (virtual machine 2.1, the chain paragraph of language 4.2, D31), which is how the plural
        // of the SHIFT=RESET row of 4.2 reads (the answer of wave-1 question #95).
        int last = state.Frame.Chain.FindLastIndex(entry => entry.Kind == kind);
        if (last >= 0)
        {
            CutAt(state, last);
        }
    }

    /// <summary>
    /// ORIGIN selects the workpiece datum: it starts an empty chain and clears the setpos shifts (language 4.2,
    /// virtual machine 2.1, 3.4, 4, D31, D55). An axis whose workpiece coordinate came from a setpos shift loses it:
    /// back to the MACHINE frame at its machine position when the shift was recorded against the machine position and
    /// that position is known, unknown otherwise (D35, D101).
    /// </summary>
    public static void SelectOrigin(ChannelState state, int origin)
    {
        // TODO(question): virtual machine 3.4 lists the frame changes that mark the position unknown and does not list
        // ORIGIN; without a datum table the coordinates in another datum are not known (D35). An axis known in the
        // workpiece frame of the datum keeps its value here, with the chain removed as by a RESET, until D123 is
        // answered.
        state.Frame.Origin = origin;
        if (state.Frame.Chain.Count > 0)
        {
            CutAt(state, 0);
        }

        // The next ORIGIN clears the setpos shift, as G54 cancels a G50 or G92 setting (virtual machine 3.4). The
        // chain is folded back above, shifts that stood before a SETPOS included; the record of the SETPOS takes those
        // out again (ReturnToMachineFrame), and it goes with the shift it belongs to.
        foreach (string axis in new List<string>(state.Frame.SetposShift.Keys))
        {
            state.Frame.SetposShift[axis] = 0m;
            bool shiftUnknown = state.Unknown.Remove(SetposKey(axis));

            // A shift recorded against the machine position: without the shift the axis is known in the MACHINE frame
            // at its machine position and unknown in the workpiece frame, as after HOME or a machine-frame move
            // (virtual machine 3.4, D35, D101).
            bool returned = ReturnToMachineFrame(state, axis);
            bool recorded = state.Frame.SetposAgainstMachine.Remove(axis);
            if (returned)
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
            // known. A shift recorded against the machine position after a motion that left the machine position
            // unknown (FollowMotion): the workpiece coordinate came from the shift. Without the shift the axis is
            // unknown in every frame (D100, D101).
            if (shiftUnknown || recorded)
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
    /// <param name="context">The SETPOS block, which a diagnostic names.</param>
    /// <param name="axis">The key of the position store the axis word names.</param>
    /// <param name="declared">The declared value, halved under DIAMETER=ON; null when it is UNKNOWN.</param>
    /// <param name="directlyAfterHomeWithoutReference">True when a HOME of the axis found no reference point in the
    /// configuration and no block has named the axis since (D100, D101).</param>
    public static void Setpos(BlockContext context, string axis, decimal? declared,
        bool directlyAfterHomeWithoutReference)
    {
        // SETPOS needs the axis known in some frame; directly after a HOME of that axis that found no reference point
        // it is accepted as well; the ERROR remains for every other axis unknown in every frame (D101).
        ChannelState state = context.State;
        AxisPosition position = state.Motion.Position[axis];
        if (!position.Known && !directlyAfterHomeWithoutReference)
        {
            context.Diagnostics.Error(context.Block, DiagnosticCodes.SetposAxisUnknown,
                $"SETPOS {axis} needs the axis known in some frame, and it is unknown in every frame; only directly "
                + "after a HOME of the axis without a reference point is that accepted (virtual machine 3.4, D101).");
            return;
        }

        // A declared value from an expression is not evaluated in STATIC mode: the shift is UNKNOWN, and with it the
        // position the axis reads as (virtual machine 1). SETPOS moves nothing: an axis known in the MACHINE frame
        // keeps its machine position, and one whose setpos shift was recorded against the machine position returns to
        // it; the record goes with the shift it belongs to (virtual machine 3.4, D35, D101).
        string key = SetposKey(axis);
        if (declared is not decimal value)
        {
            state.Unknown.Add(key);
            MarkUnknownOutsideMachineFrame(state, axis);
            state.Frame.SetposAgainstMachine.Remove(axis);
            return;
        }

        // Known in the workpiece frame through an unknown setpos shift (D101): the store holds the coordinate itself
        // and the physical position is unknown, so newSetposShift = oldPos - declared stays unknown, and the axis reads
        // as the declared value (virtual machine 3.4, D101).
        if (position.Known && position.Frame == PositionFrame.Workpiece && state.Unknown.Contains(key))
        {
            state.Motion.Position[axis] = position with { Value = value };
            return;
        }

        // Known in some frame: newSetposShift = oldPos - declared. Known in the MACHINE frame only (after HOME or a
        // machine-frame move), the shift is recorded against the machine position and the axis becomes known in the
        // workpiece frame with the declared value (D101). The store keeps the machine position; nothing moved, so it is
        // known. The record keeps the known shifts of the chain on the axis, which the store never took in, so that
        // ORIGIN and a change of the frame find the machine position again (ReturnToMachineFrame). It also keeps what
        // the frame of the declared value holds besides known shifts: the shifts from an expression on the axis, the
        // workpiece holder, and whether a ROTATE, MIRROR, TILT or TILT_AXIS turns the axis, so that a later motion
        // tells whether it moved the machine by as much as the workpiece coordinate (FollowMotion).
        if (position.Known)
        {
            state.Frame.SetposShift[axis] = position.Value - value;
            state.Unknown.Remove(key);
            if (position.Frame == PositionFrame.Machine)
            {
                state.Motion.Position[axis] = position with { Frame = PositionFrame.Workpiece };
                state.Frame.SetposAgainstMachine[axis] = new SetposRecord(ChainShift(state, axis),
                    UnknownShiftsOn(state, axis), state.Frame.WorkpieceHolder, TurnedAt(context, axis),
                    MachinePositionKnown: true);
                return;
            }

            // A later SETPOS on an axis known in the workpiece frame, with its machine position known through the
            // record, records against the same store and keeps the record: the frame has not changed since, or the
            // axis would be back in the MACHINE frame (MarkUnknownOutsideMachineFrame). On any other position the new
            // shift is not recorded against the machine position: a position known in the polar or cylinder frame, or
            // one whose machine position a motion left unknown (FollowMotion). The record of the earlier shift goes
            // with that shift (D101).
            if (!MachinePositionThroughRecord(state, axis, position, out _))
            {
                state.Frame.SetposAgainstMachine.Remove(axis);
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
    /// The coordinate of an axis in the MACHINE frame: the stored value of an axis known there, or the machine position
    /// that follows from the store through the record of its setpos shift, the store plus the known shifts of the
    /// chain on the axis minus those at the SETPOS (virtual machine 3.4, D101); null while the machine position is
    /// unknown. For the limits of the validation, which are compared in the MACHINE frame (virtual machine 5, D100).
    /// </summary>
    public static decimal? MachineCoordinate(ChannelState state, string axis)
    {
        if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position) || !position.Known)
        {
            return null;
        }

        if (position.Frame == PositionFrame.Machine)
        {
            return position.Value;
        }

        if (!MachinePositionThroughRecord(state, axis, position, out SetposRecord record))
        {
            return null;
        }

        return position.Value + ChainShift(state, axis) - record.ChainShift;
    }

    /// <summary>
    /// After a motion block that moves in the workpiece frame (virtual machine 3.1 to 3.3), while a setpos shift
    /// recorded against the machine position stands. A motion that names some axes leaves the others as they were
    /// (3.4): nothing here changes a stored position, so an axis known in the MACHINE frame keeps its position through
    /// every motion that does not move it, in a turned frame as well. Only the machine position that the record of a
    /// setpos shift gives for an axis known in the workpiece frame is looked at, for an axis the block moved
    /// (MovedAxes) and for one the frame couples with such an axis (Couples). For a moved axis it stays known when the
    /// block moved the machine by as much as the workpiece coordinate of the axis alone
    /// (MovesAsItsWorkpieceCoordinate); otherwise the amount is one that only the kinematics module or the machine's
    /// convention for another holder gives, or that comes from an expression, and the machine position is unknown
    /// until a motion of the axis that moves it by as much (virtual machine 1, 3.4, 10, D57, D101). For a coupled axis
    /// it is unknown as well, a workaround the documents do not settle (the TODO(question) above Couples).
    /// </summary>
    /// <param name="context">The motion block, after its verb moved the axes.</param>
    /// <param name="before">The position store before the block moved anything.</param>
    public static void FollowMotion(BlockContext context, IReadOnlyDictionary<string, AxisPosition> before)
    {
        ChannelState state = context.State;
        List<string> moved = MovedAxes(context, before);
        foreach (string axis in new List<string>(state.Frame.SetposAgainstMachine.Keys))
        {
            if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position)
                || !position.Known
                || position.Frame != PositionFrame.Workpiece)
            {
                continue;
            }

            bool changed = moved.Contains(axis);
            bool coupled = Couples(context, axis, moved);
            if (!changed && !coupled)
            {
                continue;
            }

            SetposRecord record = state.Frame.SetposAgainstMachine[axis];
            state.Frame.SetposAgainstMachine[axis] = record with
            {
                MachinePositionKnown = !coupled && MovesAsItsWorkpieceCoordinate(context, axis, record),
            };
        }
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
    /// becomes unknown. The record of a setpos shift taken against the machine position stays with the shift (D101).
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
    /// workpiece frame; with a setpos shift recorded against the machine position and that position known through the
    /// record, it returns to that position in the MACHINE frame (virtual machine 3.4, D35, D101).
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
    // Since then Fold has taken the known SHIFT entries appended to the chain out of the store and given the removed
    // ones back, the ones that stood before the SETPOS among them, and the motions that kept the machine position known
    // moved the store by as much as the machine (FollowMotion); the record holds the sum of the known shifts at the
    // SETPOS. So the machine position is the store plus the known shifts of the chain on the axis minus that sum. A
    // shift from an expression is in neither sum: appending or removing one returns the axis here at once, before any
    // motion, and a motion keeps the machine position known only while the chain holds the ones of the SETPOS and no
    // other, so its unknown shift cancels out. The axis returns to the MACHINE frame there, and the record stays with
    // the setpos shift. False for an axis not known in the workpiece frame, for one whose machine position a motion
    // left unknown, and for one without a record (virtual machine 1, 3.4, D35, D101).
    private static bool ReturnToMachineFrame(ChannelState state, string axis)
    {
        if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position)
            || !MachinePositionThroughRecord(state, axis, position, out SetposRecord record))
        {
            return false;
        }

        decimal machine = position.Value + ChainShift(state, axis) - record.ChainShift;
        state.Motion.Position[axis] = new AxisPosition(machine, PositionFrame.Machine, Known: true);
        return true;
    }

    // The axis is known in the workpiece frame, and its machine position is known through the record of its setpos
    // shift (D101).
    private static bool MachinePositionThroughRecord(ChannelState state, string axis, AxisPosition position,
        out SetposRecord record)
    {
        return state.Frame.SetposAgainstMachine.TryGetValue(axis, out record)
            && record.MachinePositionKnown
            && position.Known
            && position.Frame == PositionFrame.Workpiece;
    }

    // The block moved the machine position of the axis by as much as its workpiece coordinate, and the machine position
    // follows from the store through the record (D101), when the workpiece frame relates to the machine frame on this
    // axis as the frame of the SETPOS did, through shifts alone (virtual machine 3.4):
    // - The holder of the SETPOS and of the motion is the machine's default workpiece holder. The frame of any other
    //   holder has +Z out of its own chuck, and the machine reaches it through its mirror or datum convention, which
    //   readers and compilers apply outside the VM (virtual machine 3.4, D57).
    // - Neither the frame of the SETPOS nor the frame of the motion turns or mirrors the axis (Turns). A turned frame
    //   relates to the machine frame through a conversion only the kinematics module makes (virtual machine 3.4, 10):
    //   the declared value of a SETPOS in such a frame, and a motion in one, give no machine position.
    // - The SHIFT entries from an expression on the axis are those of the SETPOS: their unknown shift is in the frame
    //   of the SETPOS and of the motion alike, and it cancels out. Any other is unknown (virtual machine 1).
    private static bool MovesAsItsWorkpieceCoordinate(BlockContext context, string axis, SetposRecord record)
    {
        ChannelState state = context.State;
        string? defaultHolder = context.Machine.Machine.DefaultWorkpiece;
        if (record.Holder != defaultHolder || state.Frame.WorkpieceHolder != defaultHolder || record.Turned)
        {
            return false;
        }

        int unknownShiftsOfTheSetpos = 0;
        foreach (TransformEntry entry in state.Frame.Chain)
        {
            if (ShiftsUnknown(entry, axis))
            {
                if (!record.UnknownShifts.Contains(entry, ReferenceEqualityComparer.Instance))
                {
                    return false;
                }

                unknownShiftsOfTheSetpos++;
            }

            if (Turns(context, entry, axis))
            {
                return false;
            }
        }

        return unknownShiftsOfTheSetpos == record.UnknownShifts.Count;
    }

    // The frame of a SETPOS turns or mirrors the axis when an entry of its chain does (Turns).
    private static bool TurnedAt(BlockContext context, string axis)
    {
        foreach (TransformEntry entry in context.State.Frame.Chain)
        {
            if (Turns(context, entry, axis))
            {
                return true;
            }
        }

        return false;
    }

    // The entry turns or mirrors the axis against the frame it stands in: a ROTATE turns the two axes of the working
    // plane where it stood about its tool axis, a MIRROR mirrors the axes it names, and a TILT or TILT_AXIS turns the
    // frame in space (language 4.2, D31, D82). A SHIFT turns nothing.
    private static bool Turns(BlockContext context, TransformEntry entry, string axis)
    {
        // TODO(question): language 4.2 tilts the frame by spatial angles about its axes and says nothing of a rotary
        // axis, or of a linear axis other than X, Y and Z, under a TILT or TILT_AXIS; whether a word of such an axis
        // moves it in the machine frame by as much as in the tilted frame is not said (wave-2 question #12 asks it for
        // the rotary axes), so a tilt turns every axis until that is answered.
        return entry.Kind switch
        {
            TransformKind.Rotate => PlaneAxes(context, entry.Workplane).Contains(axis),
            TransformKind.Mirror => Mirrors(context, entry, axis),
            TransformKind.Tilt or TransformKind.TiltAxis => true,
            _ => false,
        };
    }

    // Whether the frame couples the axis with another one the block moved: the two axes of the working plane where a
    // ROTATE stood, which it turns about its tool axis, or two of X, Y and Z under a TILT or TILT_AXIS, which tilts the
    // frame about its axes (language 4.2). A MIRROR and a SHIFT act on each axis by itself, and a rotary axis, or a
    // linear axis other than X, Y and Z, is coupled with none.
    // TODO(question): no document says whether a motion in a frame that a ROTATE, TILT or TILT_AXIS turns changes the
    // machine position of an axis the block does not name; none of D121 to D127 covers it. Virtual machine 3.4 says
    // "A motion that names some axes leaves the others as they were", so every stored position stays as it was, and an
    // axis known in the MACHINE frame keeps its position (FollowMotion). On a controller the turned frame moves such an
    // axis with the named ones, by an amount only the kinematics module knows (virtual machine 10). Until it is
    // answered, only the machine position that the record of a setpos shift derives for a coupled axis (D101) is taken
    // as unknown, so that the record claims no machine position the VM may not know; the same axis known in the
    // MACHINE frame keeps its position, and the two paths differ after such a motion.
    private static bool Couples(BlockContext context, string axis, List<string> moved)
    {
        foreach (TransformEntry entry in context.State.Frame.Chain)
        {
            string[] turned = entry.Kind switch
            {
                TransformKind.Rotate => PlaneAxes(context, entry.Workplane),
                TransformKind.Tilt or TransformKind.TiltAxis => SpaceAxes(context),
                _ => [],
            };
            if (turned.Contains(axis) && moved.Exists(other => other != axis && turned.Contains(other)))
            {
                return true;
            }
        }

        return false;
    }

    // The axes the block moved: every axis whose stored position it changed, and every axis it names that it left
    // unknown, which moved to a target the VM does not know, from a known position or an unknown one (virtual machine
    // 1, 3.1). The stored position is compared, not the words: RETRACT names no axis and moves the tool axis, a cycle
    // moves its drilling axis, and an axis that ends where it stood has not moved in the machine frame either.
    private static List<string> MovedAxes(BlockContext context, IReadOnlyDictionary<string, AxisPosition> before)
    {
        var moved = new List<string>();
        foreach (KeyValuePair<string, AxisPosition> axisPosition in context.State.Motion.Position)
        {
            bool changed = !before.TryGetValue(axisPosition.Key, out AxisPosition was) || was != axisPosition.Value;
            bool namedAndUnknown = !axisPosition.Value.Known && context.AxisOf.ContainsValue(axisPosition.Key);
            if (changed || namedAndUnknown)
            {
                moved.Add(axisPosition.Key);
            }
        }

        return moved;
    }

    // The keys of the two axes of a working plane in the position store (language 4.2, virtual machine 3.8 rule 3).
    private static string[] PlaneAxes(BlockContext context, Workplane workplane)
    {
        Plane plane = workplane switch
        {
            Workplane.ZX => Plane.ZX,
            Workplane.YZ => Plane.YZ,
            _ => Plane.XY,
        };
        return [KeyOf(context, plane.FirstAxis), KeyOf(context, plane.SecondAxis)];
    }

    // The keys of X, Y and Z, the axes a TILT or TILT_AXIS turns in space (language 4.2, virtual machine 3.8 rule 3).
    private static string[] SpaceAxes(BlockContext context)
    {
        Plane space = Plane.XY;
        return [KeyOf(context, space.FirstAxis), KeyOf(context, space.SecondAxis), KeyOf(context, space.ToolAxis)];
    }

    // A MIRROR names the axis among the axes it mirrors (language 4.2).
    private static bool Mirrors(BlockContext context, TransformEntry mirror, string axis)
    {
        foreach (string name in mirror.Mirrored)
        {
            if (KeyOf(context, name) == axis)
            {
                return true;
            }
        }

        return false;
    }

    // The key of the position store an axis name resolves to, quietly (virtual machine 3.8 rule 3); the name itself for
    // one no axis of the run carries.
    private static string KeyOf(BlockContext context, string name)
    {
        return context.Resources.KeyOfAxis(name, context.State) ?? name;
    }

    // The SHIFT entries of the chain that hold the shift on the axis as UNKNOWN (virtual machine 1).
    private static TransformEntry[] UnknownShiftsOn(ChannelState state, string axis)
    {
        var entries = new List<TransformEntry>();
        foreach (TransformEntry entry in state.Frame.Chain)
        {
            if (ShiftsUnknown(entry, axis))
            {
                entries.Add(entry);
            }
        }

        return [.. entries];
    }

    // A SHIFT entry whose shift on the axis came from an expression and is UNKNOWN (virtual machine 1).
    private static bool ShiftsUnknown(TransformEntry entry, string axis)
    {
        return entry.Kind == TransformKind.Shift && entry.Shift.TryGetValue(axis, out decimal? shift) && shift is null;
    }

    // The axes of a shift whose value came from an expression, which STATIC mode does not evaluate (virtual machine 1).
    private static List<string> UnknownAxesOf(IReadOnlyDictionary<string, decimal?> shift)
    {
        var axes = new List<string>();
        foreach (KeyValuePair<string, decimal?> axisShift in shift)
        {
            if (axisShift.Value is null)
            {
                axes.Add(axisShift.Key);
            }
        }

        return axes;
    }

    // The chain is unwound from the end: the entry at the index and everything after it go; removed shifts are folded
    // back into the position, and a removed rotation, mirror or tilt marks the position unknown (virtual machine 3.4).
    // A removed shift from an expression cannot be folded back: its axes are unknown outside the MACHINE frame, as
    // appending it left them, instead of folding back 0 (virtual machine 1, the answer of wave-1 question #100).
    private static void CutAt(ChannelState state, int index)
    {
        List<TransformEntry> chain = state.Frame.Chain;
        List<TransformEntry> removed = chain.GetRange(index, chain.Count - index);
        chain.RemoveRange(index, chain.Count - index);

        bool frameTurned = false;
        var unknownAxes = new List<string>();
        foreach (TransformEntry entry in removed)
        {
            if (entry.Kind == TransformKind.Shift)
            {
                Fold(state, entry.Shift, sign: 1m);
                unknownAxes.AddRange(UnknownAxesOf(entry.Shift));
            }
            else
            {
                frameTurned = true;
            }
        }

        if (frameTurned)
        {
            MarkUnknownOutsideMachineFrame(state);
            return;
        }

        foreach (string axis in unknownAxes)
        {
            MarkUnknownOutsideMachineFrame(state, axis);
        }
    }

    // The sum of the known SHIFT entries of the chain on an axis: what Fold has taken out of a store that was known
    // outside the MACHINE frame all along (virtual machine 3.4). A shift from an expression is in no sum; Fold never
    // took it in (virtual machine 1).
    private static decimal ChainShift(ChannelState state, string axis)
    {
        decimal sum = 0m;
        foreach (TransformEntry entry in state.Frame.Chain)
        {
            if (entry.Kind == TransformKind.Shift && entry.Shift.TryGetValue(axis, out decimal? shift)
                && shift is decimal known)
            {
                sum += known;
            }
        }

        return sum;
    }

    // newPos = oldPos - shift for a shift appended, oldPos + shift for one removed, on every axis known outside the
    // MACHINE frame; the machine frame does not move with the workpiece frame (virtual machine 3.4). A shift from an
    // expression is not folded; its axes become unknown outside the MACHINE frame instead (AppendShift, CutAt).
    private static void Fold(ChannelState state, IReadOnlyDictionary<string, decimal?> shift, decimal sign)
    {
        foreach (KeyValuePair<string, decimal?> axisShift in shift)
        {
            if (axisShift.Value is not decimal value
                || !state.Motion.Position.TryGetValue(axisShift.Key, out AxisPosition position)
                || !position.Known
                || position.Frame == PositionFrame.Machine)
            {
                continue;
            }

            state.Motion.Position[axisShift.Key] = position with { Value = position.Value + (sign * value) };
        }
    }
}
