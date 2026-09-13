using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// CYCLE_CALL, the execution of the active cycle (language 4.7, virtual machine 3.3): the sequence of the built-in
/// drilling family along the drilling axis, from the clearance plane to the depth and back to the retract plane, the
/// position afterwards, and the individual motions of the sequence under the option ExpandCycles (D37); a
/// CYCLE:&lt;controller&gt;=n cycle positions the plane axes only (D94).
/// </summary>
internal static class CycleRules
{
    private const string ClearanceKey = "CLEARANCE";
    private const string DepthKey = "DEPTH";
    private const string SafeKey = "SAFE";
    private const string CycleRetractKey = "CYCLE_RETRACT";
    private const string PeckKey = "PECK";
    private const string CycleFeedKey = "CYCLE_F";

    // The built-in cycles whose names say how they move (language 4.7): CHIP_BREAK breaks the chip without leaving the
    // hole, TAP backs out along its thread behind the spindle reversal.
    private const string ChipBreakCycle = "CHIP_BREAK";
    private const string TapCycle = "TAP";

    /// <summary>
    /// Executes a CYCLE_CALL block.
    /// </summary>
    /// <param name="context">The CYCLE_CALL block with its axis words resolved (step 2).</param>
    /// <param name="expandCycles">The VM option ExpandCycles (D37).</param>
    /// <returns>The individual motions of the sequence under ExpandCycles; empty without it, and for a cycle whose
    /// sequence the VM does not know.</returns>
    public static IReadOnlyList<CycleMotion> Call(BlockContext context, bool expandCycles)
    {
        Block block = context.Block;
        CycleState cycle = context.State.Cycle;

        // CYCLE_CALL requires cycle.name not OFF (virtual machine 3.3, 5), a cycle rule, suppressed inside a subprogram
        // that no program of the file calls (3.9, D99). The axis words of the block position their axes all the same.
        if (cycle.Name is not string name)
        {
            context.CallerRuleDiagnostics.Error(block, DiagnosticCodes.CycleCallWithoutCycle,
                "CYCLE_CALL executes the active cycle, and no CYCLE is defined (virtual machine 3.3).");
            MotionRules.MoveAxes(context);
            return [];
        }

        // For a CYCLE:<controller>=n cycle the VM does not know the sequence: the call positions the plane axes at the
        // axis words of the call block, the drilling axis is unknown afterwards, and ExpandCycles raises no MOTION
        // events for it (virtual machine 3.3, D94).
        // TODO(question): a cycle of the cycle catalog (CYCLE=RECT_POCKET, language 4.7.1) carries its own parameters
        // (virtual machine 5), and virtual machine 3.3 gives the sequence of the built-in family and of the native form
        // only; the VM does not know the sequence of a catalog cycle either and calls it like the native form until
        // that is answered.
        if (cycle.Controller is not null || !DrillingFamily.Names.Contains(name))
        {
            PositionTheHoleOnly(context, cycle);
            return [];
        }

        // A built-in drilling cycle requires known DEPTH and CLEARANCE (virtual machine 3.3, 5), a cycle rule,
        // suppressed inside a subprogram that no program of the file calls (3.9, D99).
        // TODO(question): virtual machine 3.3 requires "known DEPTH and CLEARANCE", and 5 names the ERROR "without
        // DEPTH/CLEARANCE"; a DEPTH or CLEARANCE from an expression, unknown in STATIC mode (virtual machine 1), is no
        // ERROR here, and the positions that follow from it are unknown, until that is answered.
        if (ParameterOf(cycle, DepthKey) is null || ParameterOf(cycle, ClearanceKey) is null)
        {
            context.CallerRuleDiagnostics.Error(block, DiagnosticCodes.CycleCallWithoutDepthOrClearance,
                $"CYCLE_CALL of {name} needs DEPTH and CLEARANCE on its CYCLE block (virtual machine 3.3).");
            PositionTheHoleOnly(context, cycle);
            return [];
        }

        // The drilling axis is AXIS of the cycle, by default the tool axis of the active WORKPLANE (virtual machine
        // 3.3); AXIS naming an axis that is not a linear axis of the machine is an ERROR (virtual machine 5, D59).
        string? drillingAxis = context.Resources.ResolveAxis(cycle.Axis, block, context.State, context.Diagnostics);
        if (drillingAxis is not null && context.Resources.IsRotary(drillingAxis))
        {
            context.Diagnostics.Error(block, DiagnosticCodes.CycleAxisNotLinear,
                $"AXIS={cycle.Axis} names a rotary axis; the drilling axis of a cycle is a linear axis of the machine "
                + "(language 4.7, virtual machine 3.3, D59).");
            drillingAxis = null;
        }

        if (drillingAxis is null)
        {
            MotionRules.MoveAxes(context);
            return [];
        }

        return Drill(context, name, drillingAxis, expandCycles);
    }

    // The sequence of the built-in family (virtual machine 3.3): rapid in the plane to the axis words at the current
    // drilling-axis position (if any), rapid to CLEARANCE, feed to DEPTH with CYCLE_F (pecking with PECK, dwell with
    // CYCLE_DWELL, spindle reversal for TAP), retract to CLEARANCE or SAFE per CYCLE_RETRACT. Position afterwards:
    // plane axes at the call point, drilling axis at the retract plane. The planes are absolute coordinates along the
    // drilling axis in the active frame (language 4.7), and under DIAMETER=ON the X values of an AXIS=X cycle are
    // halved like every other X (virtual machine 3.3, D59, D60).
    private static List<CycleMotion> Drill(BlockContext context, string name, string drillingAxis, bool expandCycles)
    {
        ChannelState state = context.State;
        CycleState cycle = state.Cycle;
        PositionFrame frame = MotionRules.FrameOf(state, cycle.Axis);
        AxisPosition clearance = PlaneAt(state, ClearanceKey, drillingAxis, frame);
        AxisPosition depth = PlaneAt(state, DepthKey, drillingAxis, frame);

        // TODO(question): CYCLE_RETRACT=SAFE retracts to SAFE, and SAFE is optional (language 4.7); without SAFE the
        // retract plane is not given, and the drilling axis is unknown after the call until that is answered.
        bool toSafe = ParameterOf(cycle, CycleRetractKey) is Word retract && BlockContext.IdentOf(retract) == SafeKey;
        AxisPosition retractPlane = toSafe ? PlaneAt(state, SafeKey, drillingAxis, frame) : clearance;

        // TODO(question): the other axis words of the call block position the hole (virtual machine 3.3); what a word
        // on the drilling axis itself does in the call block is not said. It moves its axis with the rapid to the
        // hole, before the sequence, until that is answered.
        Dictionary<string, AxisPosition> hole = PositionTheHole(context);
        state.Motion.Position[drillingAxis] = retractPlane;
        if (!expandCycles)
        {
            return [];
        }

        // With the VM option ExpandCycles the call is raised as the individual MOTION events (virtual machine 3.3,
        // D37).
        // TODO(question): virtual machine 3.3 names the pecking with PECK, the dwell with CYCLE_DWELL, the spindle
        // reversal for TAP and the retract without saying how each moves. Here a known PECK greater than 0 splits the
        // feed into pecks of that depth; between the pecks the tool returns at rapid to CLEARANCE and back to the depth
        // it reached (PECK, the deep hole cycle with full retract, language 4.7), except for CHIP_BREAK, which feeds on
        // without leaving the hole (its chip-breaking retract is a setting of the control); the retract is a rapid,
        // except for TAP, which feeds out at CYCLE_F behind the spindle reversal; the dwell moves nothing. This holds
        // until that is answered.
        decimal? feed = ParameterOf(cycle, CycleFeedKey) is Word feedWord ? MotionRules.NumberOf(feedWord) : null;
        var motions = new List<CycleMotion>();
        if (hole.Count > 0)
        {
            motions.Add(new CycleMotion { Verb = Verb.Rapid, To = hole });
        }

        motions.Add(Rapid(drillingAxis, clearance));
        foreach (AxisPosition peck in Pecks(cycle, clearance, depth))
        {
            motions.Add(Feed(drillingAxis, peck, feed));
            if (name != ChipBreakCycle)
            {
                motions.Add(Rapid(drillingAxis, clearance));
                motions.Add(Rapid(drillingAxis, peck));
            }
        }

        motions.Add(Feed(drillingAxis, depth, feed));
        motions.Add(name == TapCycle ? Feed(drillingAxis, retractPlane, feed) : Rapid(drillingAxis, retractPlane));
        return motions;
    }

    // The depths a PECK greater than 0 reaches before DEPTH, from CLEARANCE toward DEPTH in steps of PECK, a radius
    // value (language 4.7, D60); none without a known PECK, CLEARANCE and DEPTH. The store holds both planes through
    // the same setpos shift, so the steps are taken on the stored values.
    private static List<AxisPosition> Pecks(CycleState cycle, AxisPosition clearance, AxisPosition depth)
    {
        var pecks = new List<AxisPosition>();
        if (!clearance.Known
            || !depth.Known
            || ParameterOf(cycle, PeckKey) is not Word peckWord
            || MotionRules.NumberOf(peckWord) is not decimal peck
            || peck <= 0)
        {
            return pecks;
        }

        decimal direction = depth.Value < clearance.Value ? -1m : 1m;
        for (decimal reached = clearance.Value + (direction * peck);
            direction * (depth.Value - reached) > 0;
            reached += direction * peck)
        {
            pecks.Add(depth with { Value = reached });
        }

        return pecks;
    }

    // The axis words of the call block position the hole, each moving its axis as a RAPID does (virtual machine 3.1,
    // 3.3); a call without axis words executes at the current position (language 4.7). The positions the axes reached.
    private static Dictionary<string, AxisPosition> PositionTheHole(BlockContext context)
    {
        var hole = new Dictionary<string, AxisPosition>(StringComparer.Ordinal);
        foreach (Word word in context.Block.Words)
        {
            if (context.AxisOf.TryGetValue(word, out string? axis))
            {
                MotionRules.MoveAxis(context, word, axis);
                hole[axis] = context.State.Motion.Position[axis];
            }
        }

        return hole;
    }

    // The plane axes go to the axis words of the call block, and the drilling axis is unknown afterwards: the VM does
    // not know the sequence (virtual machine 3.3, D94).
    private static void PositionTheHoleOnly(BlockContext context, CycleState cycle)
    {
        MotionRules.MoveAxes(context);
        Dictionary<string, AxisPosition> position = context.State.Motion.Position;
        if (context.Resources.KeyOfAxis(cycle.Axis, context.State) is string drillingAxis
            && position.ContainsKey(drillingAxis))
        {
            position[drillingAxis] = AxisPosition.Unknown;
        }
    }

    // A plane along the drilling axis, SURFACE, CLEARANCE, DEPTH or SAFE, at its coordinate in the active frame; under
    // DIAMETER=ON the X values of an AXIS=X cycle are halved (virtual machine 3.3, D60). Unknown without the word or
    // for an expression (virtual machine 1).
    private static AxisPosition PlaneAt(ChannelState state, string key, string drillingAxis, PositionFrame frame)
    {
        CycleState cycle = state.Cycle;
        if (ParameterOf(cycle, key) is not Word word || MotionRules.NumberOf(word) is not decimal value)
        {
            return AxisPosition.Unknown;
        }

        decimal coordinate = DiameterRules.ToRadius(word, value, state.Frame.Diameter, cycle.Axis);
        return MotionRules.PositionAt(state, drillingAxis, frame, coordinate);
    }

    // A parameter word of the active cycle by its key; null when the CYCLE block did not write it (virtual machine
    // 2.6).
    private static Word? ParameterOf(CycleState cycle, string key)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word;
            }
        }

        return null;
    }

    private static CycleMotion Rapid(string axis, AxisPosition to)
    {
        return new CycleMotion { Verb = Verb.Rapid, To = new Dictionary<string, AxisPosition> { [axis] = to } };
    }

    private static CycleMotion Feed(string axis, AxisPosition to, decimal? feed)
    {
        return new CycleMotion
        {
            Verb = Verb.Line,
            To = new Dictionary<string, AxisPosition> { [axis] = to },
            Feed = feed,
        };
    }
}
