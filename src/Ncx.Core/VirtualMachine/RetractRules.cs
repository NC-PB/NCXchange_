using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// RETRACT, the retract along the tool axis (language 4.3, virtual machine 3.1a, D83): only the tool axis moves, away
/// from the workpiece, by the given distance or bare to the machine limit of that axis; under a tilt the moved axes
/// become unknown without the kinematics module.
/// </summary>
internal static class RetractRules
{
    // The linear axes whose combination the tool axis of a tilted plane is (language 4.2).
    private static readonly string[] s_linearAxes = [Plane.XY.FirstAxis, Plane.XY.SecondAxis, Plane.XY.ToolAxis];

    /// <summary>
    /// Executes a RETRACT block.
    /// </summary>
    public static void Retract(BlockContext context)
    {
        Block block = context.Block;
        ChannelState state = context.State;

        // A RETRACT with a feed: ERROR (virtual machine 3.1a, 5). One with other axis words the parser reports: RETRACT
        // carries no axis words (language 5 rule 2).
        if (block.Find("F") is Word feed)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.RetractWithFeed,
                "RETRACT moves the tool axis the way the control retracts it and takes no feed, not "
                + $"{feed.ToCanonical()} (virtual machine 3.1a, D83).");
            return;
        }

        // Under a tilt the direction is known only with the kinematics module; without it the moved axes become
        // unknown, which is what the unknown rule after machine-frame moves already does (virtual machine 3.1a, D35).
        // The tool axis of a tilted plane is a combination of X, Y and Z, so each of them may move.
        if (UnderATilt(state))
        {
            foreach (string name in s_linearAxes)
            {
                if (context.Resources.KeyOfAxis(name, state) is string axis && state.Motion.Position.ContainsKey(axis))
                {
                    state.Motion.Position[axis] = AxisPosition.Unknown;
                }
            }

            return;
        }

        // Without an active TILT or TILT_AXIS the tool axis is the axis perpendicular to the WORKPLANE, and the VM
        // moves it (virtual machine 3.1a).
        string toolAxisName = CycleState.ToolAxisOf(state.Frame.Workplane);
        if (context.Resources.ResolveAxis(toolAxisName, block, state, context.Diagnostics) is not string toolAxis)
        {
            return;
        }

        // Bare, to the machine limit of that axis: the limits from the configuration, unknown when there are none. The
        // limits are machine coordinates (D100), so the axis is known in the MACHINE frame afterwards and unknown in
        // the workpiece frame, as after a machine-frame move (D35). Away from the workpiece is the upper limit: the
        // tool axis points out of the workpiece toward the tool (D57).
        Word retract = block.Verb ?? throw new InvalidOperationException("RetractRules executes RETRACT blocks only.");
        if (retract.Value is NoValue)
        {
            state.Motion.Position[toolAxis] = context.Machine.ResolveAxis(toolAxis)?.Max is decimal limit
                ? new AxisPosition(limit, PositionFrame.Machine, Known: true)
                : AxisPosition.Unknown;
            return;
        }

        // RETRACT=50: by that distance along the tool axis, away from the workpiece, in the frame the axis is known in;
        // a distance from an expression, or an axis unknown in every frame, leaves the axis unknown (virtual machine 1,
        // 3.1a). The distance is a length along the tool axis and never a diameter (D60).
        AxisPosition position = state.Motion.Position[toolAxis];
        state.Motion.Position[toolAxis] = position.Known && MotionRules.NumberOf(retract) is decimal distance
            ? position with { Value = position.Value + distance }
            : AxisPosition.Unknown;
    }

    // An active TILT or TILT_AXIS: an entry of either kind in the transform chain (language 4.2, D82).
    private static bool UnderATilt(ChannelState state)
    {
        foreach (TransformEntry entry in state.Frame.Chain)
        {
            if (entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis)
            {
                return true;
            }
        }

        return false;
    }
}
