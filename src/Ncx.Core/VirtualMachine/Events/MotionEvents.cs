using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The MOTION events of a block (virtual machine 7 as amended by F18): one for a RAPID, LINE, ARC, RETRACT or HOME, one
/// per motion of the sequence of an expanded CYCLE_CALL (3.3, D37), each with its length (8).
/// </summary>
internal static class MotionEvents
{
    // A sweep in degrees keeps three decimals, the resolution of a rotary axis on the common controls, as a length
    // keeps those of MotionRules.PositionDecimals; no document says how many (wave-1 question #46).
    private const int SweepDecimals = 3;

    /// <summary>
    /// The MOTION events of a block, in the order the machine moves.
    /// </summary>
    /// <param name="context">The block after step 6.</param>
    /// <param name="before">The state before the block.</param>
    /// <param name="after">The state after the block.</param>
    /// <param name="arc">The arc step 5 resolved; null for any other block and an arc that did not resolve.</param>
    /// <param name="cycleMotions">The motions of an expanded CYCLE_CALL; empty otherwise.</param>
    public static List<MotionEvent> Of(BlockContext context, ChannelSnapshot before, ChannelSnapshot after,
        PlaneArc? arc, IReadOnlyList<CycleMotion> cycleMotions)
    {
        var motions = new List<MotionEvent>();
        Block block = context.Block;

        // With the VM option ExpandCycles a CYCLE_CALL is raised as the individual MOTION events of its sequence
        // (virtual machine 3.3, D37).
        if (block.Verb?.Key == "CYCLE_CALL")
        {
            AddCycleMotions(motions, context, before, after, cycleMotions);
            return motions;
        }

        // MOTION at every RAPID, LINE, ARC and RETRACT (virtual machine 7).
        // TODO(question): virtual machine 7 raises MOTION at "every RAPID, LINE, ARC, RETRACT" and does not name HOME,
        // while architecture 5.1 raises MOTION for HOME with the other motion verbs and the runtime estimate charges
        // HOME at the rapid rate like RAPID (virtual machine 8); HOME is raised as a MOTION until that is answered.
        if (VerbOf(block.Verb?.Key) is not Verb verb)
        {
            return motions;
        }

        IReadOnlyDictionary<string, AxisPosition> from = before.Motion.Position;
        IReadOnlyDictionary<string, AxisPosition> to = after.Motion.Position;
        var motion = new MotionEvent
        {
            Channel = after.ChannelId,
            Block = block,
            Before = before,
            After = after,
            Verb = verb,
            From = from,
            To = to,
            ToolVector = after.Motion.ToolVector,
            SurfaceNormal = after.Motion.SurfaceNormal,
            Feed = verb is Verb.Rapid or Verb.Home ? null : FeedOf(after),
            FeedMode = after.Motion.FeedMode,
            Comp = after.Motion.Comp,
            Frame = FrameOf(block),
            Length = verb == Verb.Arc ? null : LengthOf(context, from, to, after.Frame.Units, [], 0d),
        };

        if (verb == Verb.Arc)
        {
            motion = WithArc(motion, context, arc, after.Frame.Units);
        }

        motions.Add(motion);
        return motions;
    }

    // An ARC carries its direction, and when it resolved its center, its sweep and its arc length, the travel of a
    // helix included (virtual machine 3.2, 7, 8; D84).
    private static MotionEvent WithArc(MotionEvent motion, BlockContext context, PlaneArc? arc, Units units)
    {
        ArcDirection direction = context.Block.Verb is Word verb && BlockContext.IdentOf(verb) == "CCW"
            ? ArcDirection.Counterclockwise
            : ArcDirection.Clockwise;
        if (arc is null)
        {
            return motion with { Direction = direction };
        }

        Plane plane = arc.Plane;
        string firstAxis = context.Resources.KeyOfAxis(plane.FirstAxis, context.State) ?? plane.FirstAxis;
        string secondAxis = context.Resources.KeyOfAxis(plane.SecondAxis, context.State) ?? plane.SecondAxis;
        int decimals = MotionRules.PositionDecimals(units);
        bool machineFrame = motion.Frame == PositionFrame.Machine;
        var center = new Dictionary<string, AxisPosition>(StringComparer.Ordinal)
        {
            [firstAxis] = Stored(context, firstAxis, plane.FirstAxis, Vec3.RoundToDecimal(arc.Arc.Center.X, decimals),
                machineFrame),
            [secondAxis] = Stored(context, secondAxis, plane.SecondAxis,
                Vec3.RoundToDecimal(arc.Arc.Center.Y, decimals), machineFrame),
        };

        // The arc turns r times the sweep in the plane; the tool axis and any other axis of the block travel along it.
        double inThePlane = arc.Arc.Radius * Math.Abs(arc.Arc.Sweep) * Math.PI / 180d;
        return motion with
        {
            Direction = direction,
            Center = center.AsReadOnly(),
            Sweep = Vec3.RoundToDecimal(arc.Arc.Sweep, SweepDecimals),
            Length = LengthOf(context, motion.From, motion.To, units, [firstAxis, secondAxis], inThePlane),
        };
    }

    // The motions of the sequence of an expanded CYCLE_CALL, each from where the one before it ended: RAPID for the
    // positioning, approach and retract, LINE at CYCLE_F for the feed (virtual machine 3.3, D37).
    private static void AddCycleMotions(List<MotionEvent> motions, BlockContext context, ChannelSnapshot before,
        ChannelSnapshot after, IReadOnlyList<CycleMotion> cycleMotions)
    {
        var position = new Dictionary<string, AxisPosition>(before.Motion.Position, StringComparer.Ordinal);
        foreach (CycleMotion cycleMotion in cycleMotions)
        {
            IReadOnlyDictionary<string, AxisPosition> from =
                new Dictionary<string, AxisPosition>(position).AsReadOnly();
            foreach (KeyValuePair<string, AxisPosition> target in cycleMotion.To)
            {
                position[target.Key] = target.Value;
            }

            IReadOnlyDictionary<string, AxisPosition> to = new Dictionary<string, AxisPosition>(position).AsReadOnly();
            motions.Add(new MotionEvent
            {
                Channel = after.ChannelId,
                Block = context.Block,
                Before = before,
                After = after,
                Verb = cycleMotion.Verb,
                From = from,
                To = to,
                ToolVector = after.Motion.ToolVector,
                SurfaceNormal = after.Motion.SurfaceNormal,
                Feed = cycleMotion.Verb == Verb.Line ? cycleMotion.Feed : null,
                FeedMode = after.Motion.FeedMode,
                Comp = after.Motion.Comp,
                Frame = FrameOf(context.Block),
                Length = LengthOf(context, from, to, after.Frame.Units, [], 0d),
            });
        }
    }

    // The length of a motion (virtual machine 8): the euclidean length over the axes that moved, with the length the
    // arc covers in its plane; null when an axis that moved is not known at both ends in one frame. A rotary axis turns
    // in degrees and is no part of a length, except in the polar and the cylinder plane, where its word is a length
    // (virtual machine 3.1, D102). A length computed in double reaches the event as a decimal rounded to the units'
    // decimals (D62; wave-1 question #46).
    private static decimal? LengthOf(BlockContext context, IReadOnlyDictionary<string, AxisPosition> from,
        IReadOnlyDictionary<string, AxisPosition> to, Units units, string[] planeAxes, double inThePlane)
    {
        double squares = inThePlane * inThePlane;
        foreach (KeyValuePair<string, AxisPosition> target in to)
        {
            AxisPosition start = from.TryGetValue(target.Key, out AxisPosition known) ? known : AxisPosition.Unknown;
            AxisPosition end = target.Value;
            if (start == end || planeAxes.Contains(target.Key) || !IsLength(context, target.Key, start, end))
            {
                continue;
            }

            if (!start.Known || !end.Known || start.Frame != end.Frame)
            {
                return null;
            }

            double travel = (double)(end.Value - start.Value);
            squares += travel * travel;
        }

        return Vec3.RoundToDecimal(Math.Sqrt(squares), MotionRules.PositionDecimals(units));
    }

    private static bool IsLength(BlockContext context, string axis, AxisPosition start, AxisPosition end)
    {
        return !context.Resources.IsRotary(axis) || (InTransformation(start) && InTransformation(end));
    }

    private static bool InTransformation(AxisPosition position)
    {
        return position.Frame is PositionFrame.Polar or PositionFrame.Cylinder;
    }

    // A plane coordinate of the arc as the position store holds it: in the workpiece frame through the setpos shift,
    // in every other frame as it is (virtual machine 3.4, D101, D102).
    private static AxisPosition Stored(BlockContext context, string axis, string axisName, decimal coordinate,
        bool machineFrame)
    {
        ChannelState state = context.State;
        PositionFrame frame = machineFrame ? PositionFrame.Machine : MotionRules.FrameOf(state, axisName);
        if (frame == PositionFrame.Workpiece && state.Frame.SetposShift.ContainsKey(axis))
        {
            return FrameRules.WorkpiecePosition(state, axis, coordinate);
        }

        return new AxisPosition(coordinate, frame, Known: true);
    }

    // feed.value as the motion sees it; null for none and for a feed from an expression (virtual machine 1).
    private static decimal? FeedOf(ChannelSnapshot after)
    {
        return after.Unknown.Contains("F") ? null : after.Motion.Feed;
    }

    // frame (block): a FRAME=MACHINE block and a HOME move in machine coordinates (virtual machine 2.1, 3 step 5, 3.4).
    private static PositionFrame FrameOf(Block block)
    {
        return block.Has("FRAME", null, "MACHINE") || block.Verb?.Key == "HOME"
            ? PositionFrame.Machine
            : PositionFrame.Workpiece;
    }

    private static Verb? VerbOf(string? key)
    {
        return key switch
        {
            "RAPID" => Verb.Rapid,
            "LINE" => Verb.Line,
            "ARC" => Verb.Arc,
            "RETRACT" => Verb.Retract,
            "HOME" => Verb.Home,
            _ => null,
        };
    }
}
