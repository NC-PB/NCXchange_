using Ncx.Core.VirtualMachine.Handlers;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// PROGRAM=END, the executed end of a program (language 4.1): what it ends and what it resets, the column "Reset by"
/// of the modal summary (virtual machine 4).
/// </summary>
internal static class ProgramEndRules
{
    /// <summary>
    /// Ends the program and resets what the modal summary resets at PROGRAM=END; everything else is kept.
    /// </summary>
    public static void EndProgram(ChannelState state)
    {
        // PROGRAM=END ends execution of the program: ended, and the channel is finished (virtual machine 2.1, 2.8,
        // 3.6).
        state.Program.Ended = true;
        state.Program.Active = false;
        state.Finished = true;

        // Feed value and mode, compensation: PROGRAM=END resets them (virtual machine 4).
        state.Motion.Feed = null;
        state.Motion.FeedMode = FeedMode.PerMin;
        state.Motion.Comp = Compensation.Off;
        state.Unknown.Remove("F");

        // Spindle direction, rpm, vc, mode, sync and phase, css: PROGRAM=END sets OFF, mode SPINDLE, sync OFF (virtual
        // machine 4). "Sets OFF" applies to the items that have an OFF value, the direction and CSS (virtual machine
        // 2.4, language 4.11; wave-1 question #99).
        foreach (KeyValuePair<string, SpindleState> spindle in state.Spindles)
        {
            spindle.Value.Direction = SpindleDirection.Off;
            spindle.Value.Mode = SpindleMode.Spindle;
            spindle.Value.SyncPartner = null;
            spindle.Value.SyncPhase = null;
            spindle.Value.Css = false;
            state.Unknown.Remove(BlockContext.StateKey("PHASE", spindle.Key));
        }

        // Cylinder, polar, tcpm, rotary path and feed, tolerance: PROGRAM=END sets OFF and the defaults (virtual
        // machine 4); POLAR and CYLINDER going OFF leave their axes unknown in the workpiece frame (3.4), TCPM going
        // OFF the tool vector and the surface normal (D81).
        state.Frame.Cylinder = null;
        state.Frame.Polar = false;
        state.Frame.Tcpm = false;
        state.Frame.RotaryPath = RotaryPath.Full;
        state.Frame.RotaryFeed = RotaryFeed.DegMin;
        state.Frame.Tolerance = new ToleranceState { Value = null, Rotary = null, Mode = ToleranceMode.Finish };
        state.Unknown.Remove("CYLINDER");
        state.Unknown.Remove("TOLERANCE");
        state.Unknown.Remove(BlockContext.StateKey("TOLERANCE", "ROTARY"));
        FrameRules.LeaveTransformation(state, PositionFrame.Polar);
        FrameRules.LeaveTransformation(state, PositionFrame.Cylinder);
        state.Motion.ToolVector = null;
        state.Motion.SurfaceNormal = null;

        // Coolant: PROGRAM=END sets it OFF; the named functions keep their state (virtual machine 4).
        foreach (string channel in new List<string>(state.Coolant.Keys))
        {
            state.Coolant[channel] = false;
        }

        // Cycle: reset by PROGRAM=END (virtual machine 4).
        CycleHandlers.EndCycle(state);

        // Kept at PROGRAM=END: units, workplane, origin, the chain and diameter (explicit word only), the setpos
        // shifts, the tool in the spindle, the preload and the offsets, the named functions, the variables and the
        // workpiece holder (virtual machine 4).
    }
}
