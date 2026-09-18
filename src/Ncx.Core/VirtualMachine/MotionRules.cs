using Ncx.Core.Catalog;
using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// Linear motion (virtual machine 3.1): the target of every axis word, X= replacing and IX= adding to the current
/// value, in the frame the word programs in: the workpiece frame through the setpos shifts (3.4), the MACHINE frame
/// under FRAME=MACHINE (D35), the polar and the cylinder frame under POLAR=ON and CYLINDER=n (D102); and the rules
/// every motion keeps: UNITS before the first motion, a feed for LINE, one form per axis. ARC, RETRACT and CYCLE_CALL
/// take their targets from here. Inside a subprogram that no program of the file calls the rules that depend on a
/// caller are suppressed (3.9, D99).
/// </summary>
internal static class MotionRules
{
    // The verbs of the blocks that move (language 2 rule 2, 4.3; virtual machine 2.2, 3 step 5).
    private static readonly string[] s_motionVerbs = ["RAPID", "LINE", "ARC", "RETRACT", "HOME", "CYCLE_CALL"];

    /// <summary>
    /// Tells whether a block moves: its verb is RAPID, LINE, ARC, RETRACT, HOME or CYCLE_CALL (language 2 rule 2,
    /// virtual machine 3 step 5).
    /// </summary>
    public static bool IsMotion(Block block)
    {
        return block.Verb is Word verb && s_motionVerbs.Contains(verb.Key);
    }

    /// <summary>
    /// Motion before UNITS: ERROR (virtual machine 3.1, 5; language 4.1, D11). HOME moves as well (language 2 rule 2,
    /// virtual machine 3 step 5). The rule depends on the caller's state and is suppressed inside a subprogram that no
    /// program of the file calls (3.9, D99).
    /// </summary>
    public static void CheckUnits(BlockContext context)
    {
        if (context.State.Frame.Units != Units.Unknown)
        {
            return;
        }

        context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.MotionBeforeUnits,
            $"{context.Block.Verb?.ToCanonical()} moves before UNITS; UNITS=MM or UNITS=INCH is required before the "
            + "first motion (language 4.1, virtual machine 3.1).");
    }

    /// <summary>
    /// Absolute and incremental words may be mixed in one block, one form per axis (language 4.3): X=10 with IX=5, or
    /// CENTER:X with CENTER:IX, is an ERROR.
    /// </summary>
    public static void CheckOneFormPerAxis(BlockContext context)
    {
        var firstWordOfAxis = new Dictionary<string, Word>(StringComparer.Ordinal);
        foreach (Word word in context.Block.Words)
        {
            if (FormKey(word, context) is not string axis)
            {
                continue;
            }

            if (firstWordOfAxis.TryGetValue(axis, out Word? first))
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.TwoFormsOfOneAxis,
                    $"{first.ToCanonical()} and {word.ToCanonical()} both give {axis}; absolute and incremental words "
                    + "may be mixed in one block, one form per axis (language 4.3).");
                continue;
            }

            firstWordOfAxis[axis] = word;
        }
    }

    /// <summary>
    /// RAPID and LINE move every axis their words name to its target (virtual machine 3.1). LINE with feed.value none
    /// is an ERROR, suppressed inside a subprogram that no program of the file calls (3.9, D99); a feed from an
    /// expression is set with its value unknown (virtual machine 1), so it counts as a feed.
    /// </summary>
    public static void MoveStraight(BlockContext context)
    {
        // TODO(question): virtual machine 3.1 and 5 name "LINE without feed"; ARC moves at the active feed as well
        // (language 4.3) and is not named, so only LINE is checked until D184 is answered.
        if (context.Block.Verb?.Key == "LINE" && context.State.Motion.Feed is null)
        {
            context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.LineWithoutFeed,
                "LINE moves at the active feed, and no F is set (virtual machine 3.1).");
        }

        MoveAxes(context);
    }

    /// <summary>
    /// Every axis word of the block moves its axis to its target; the axes the block does not mention keep their value
    /// (virtual machine 3.1).
    /// </summary>
    public static void MoveAxes(BlockContext context)
    {
        foreach (Word word in context.Block.Words)
        {
            if (context.AxisOf.TryGetValue(word, out string? axis))
            {
                MoveAxis(context, word, axis);
            }
        }
    }

    /// <summary>
    /// Under POLAR=ON X and C, under CYLINDER=n the cylinder axis and C are in the frame of the transformation: from
    /// the first motion under it their positions are known in that frame and unknown in the workpiece frame (virtual
    /// machine 3.1, 3.4, D102). Before the verb moves anything, an axis of the transformation that the block does not
    /// name and that is not known in its frame yet becomes unknown: an axis has one position frame (2.2), and the VM
    /// does not convert a workpiece or machine coordinate into the plane of the transformation, which is the kinematics
    /// module's (3.1, D54). The record of a setpos shift taken against the machine position stays, as after POLAR=OFF,
    /// so that the next motion with known coordinates after the transformation finds the machine position (D101).
    /// </summary>
    public static void EnterTransformation(BlockContext context)
    {
        ChannelState state = context.State;
        if (!state.Frame.Polar && state.Frame.Cylinder is null)
        {
            return;
        }

        // POLAR=ON before CYLINDER=n, as the plane of ARC and the frame of a word take them
        // (ArcRules.PlaneOf, FrameOf).
        Plane plane = ArcRules.PlaneOf(state.Frame);
        PositionFrame frame = state.Frame.Polar ? PositionFrame.Polar : PositionFrame.Cylinder;
        string[] names = [plane.FirstAxis, plane.SecondAxis];
        foreach (string name in names)
        {
            if (context.Resources.KeyOfAxis(name, state) is not string axis
                || context.AxisOf.ContainsValue(axis)
                || !state.Motion.Position.TryGetValue(axis, out AxisPosition position)
                || (position.Known && position.Frame == frame))
            {
                continue;
            }

            state.Motion.Position[axis] = AxisPosition.Unknown;
        }
    }

    /// <summary>
    /// Target = current position with the word applied: X= replaces, IX= adds to the current value, in the frame the
    /// word programs in; under DIAMETER=ON X and IX are halved (virtual machine 3.1, D60). IX from an unknown position
    /// is an ERROR, and inside a subprogram that no program of the file calls the position becomes unknown instead
    /// (3.9, D99). A word from an expression is not evaluated in STATIC mode and leaves the axis unknown (virtual
    /// machine 1).
    /// </summary>
    /// <param name="context">The motion block.</param>
    /// <param name="word">An axis word of the block.</param>
    /// <param name="axis">The key of the position store the word names (step 2).</param>
    public static void MoveAxis(BlockContext context, Word word, string axis)
    {
        ChannelState state = context.State;
        PositionFrame frame = FrameOf(state, AxisName(word));
        decimal? target = ValueOf(word, state);
        if (IsIncremental(word))
        {
            // TODO(question): virtual machine 3.1 adds IX to "the current value" and D101 lets SETPOS accept an axis
            // known in some frame; whether IX may add to a value known in another frame than the one the block programs
            // in (after HOME the axis is known in the MACHINE frame only, D35) is not said. The current value is taken
            // in the frame of the block, and a value unknown there is the ERROR, until D183 is answered.
            if (CoordinateIn(state, axis, frame) is not decimal current)
            {
                context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.IncrementalFromUnknownPosition,
                    $"{word.ToCanonical()} adds to the current value of {axis}, and it is not known in the "
                    + $"{FrameName(frame)} frame (virtual machine 3.1).");
                state.Motion.Position[axis] = AxisPosition.Unknown;
                return;
            }

            target = current + target;
        }

        state.Motion.Position[axis] = target is decimal known
            ? PositionAt(state, axis, frame, known)
            : AxisPosition.Unknown;
    }

    /// <summary>
    /// The frame an axis word programs in (virtual machine 3.1, 3.4): a FRAME=MACHINE block moves in machine
    /// coordinates (D35); under POLAR=ON the X word and the C word are coordinates of the polar frame, under
    /// CYLINDER=n the cylinder axis and the C word coordinates of the cylinder frame (D102); every other word is a
    /// coordinate of the workpiece frame.
    /// </summary>
    /// <param name="state">The channel state.</param>
    /// <param name="axisName">The axis as the word writes it, without the I of the incremental form: X, C, Z2.</param>
    public static PositionFrame FrameOf(ChannelState state, string axisName)
    {
        FrameState frame = state.Frame;
        if (frame.MachineFrameBlock)
        {
            return PositionFrame.Machine;
        }

        if (frame.Polar && (axisName == Plane.Polar.FirstAxis || axisName == Plane.Polar.SecondAxis))
        {
            return PositionFrame.Polar;
        }

        if (frame.Cylinder is not null
            && (axisName == Plane.Cylinder.FirstAxis || axisName == Plane.Cylinder.SecondAxis))
        {
            return PositionFrame.Cylinder;
        }

        return PositionFrame.Workpiece;
    }

    /// <summary>
    /// The current coordinate of an axis in a frame: in the workpiece frame the stored value read through the setpos
    /// shift (virtual machine 3.4), in every other frame the stored value when the axis is known there; null when the
    /// axis is not known in that frame.
    /// </summary>
    public static decimal? CoordinateIn(ChannelState state, string axis, PositionFrame frame)
    {
        if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position) || !position.Known)
        {
            return null;
        }

        if (frame == PositionFrame.Workpiece)
        {
            return FrameRules.WorkpieceCoordinate(state, axis);
        }

        return position.Frame == frame ? position.Value : null;
    }

    /// <summary>
    /// The position that stores a coordinate of a frame: in the workpiece frame the physical value, the coordinate
    /// through the setpos shift (virtual machine 3.4, D101), in every other frame the coordinate itself.
    /// </summary>
    public static AxisPosition PositionAt(ChannelState state, string axis, PositionFrame frame, decimal coordinate)
    {
        return frame == PositionFrame.Workpiece
            ? FrameRules.WorkpiecePosition(state, axis, coordinate)
            : new AxisPosition(coordinate, frame, Known: true);
    }

    /// <summary>
    /// The value of an axis or CENTER word as the position store takes it: the number, halved under DIAMETER=ON for X,
    /// IX and the absolute CENTER:X (D60); null for an expression, which STATIC mode does not evaluate (virtual machine
    /// 1).
    /// </summary>
    public static decimal? ValueOf(Word word, ChannelState state)
    {
        // Every X word is halved, the X of a FRAME=MACHINE block among them; whether the machine coordinates of an X
        // axis programmed in diameters (home, limits) are diameters is wave-1 question #4.
        return NumberOf(word) is decimal value
            ? DiameterRules.ToRadius(word, value, state.Frame.Diameter, cycleAxis: null)
            : null;
    }

    /// <summary>
    /// The number of a word as written; null for an expression or a word without a number.
    /// </summary>
    public static decimal? NumberOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }

    /// <summary>
    /// The axis an axis word names as written, without the I of the incremental form: X of IX, Z2 of IZ2 (language 4.3,
    /// D93).
    /// </summary>
    public static string AxisName(Word word)
    {
        if (word.Definition is not null)
        {
            return WithoutIncrement(word.Key);
        }

        return WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis) ? machineAxis.AxisName : word.Key;
    }

    /// <summary>
    /// The incremental form of an axis word, IX or IZ2 (language 4.3, D93).
    /// </summary>
    public static bool IsIncremental(Word word)
    {
        if (word.Definition is not null)
        {
            return word.Key.StartsWith('I');
        }

        return WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis) && machineAxis.IsIncremental;
    }

    /// <summary>
    /// An axis name without the I of the incremental form, X of IX and of the CENTER address IX (language 3, KEY: no
    /// axis name starts with I).
    /// </summary>
    public static string WithoutIncrement(string name)
    {
        return name.StartsWith('I') ? name.Substring(1) : name;
    }

    /// <summary>
    /// The decimals a coordinate computed in double keeps when it goes back into the position store (phase 1 risks,
    /// D62): 3 in millimetres and 4 in inches, the resolution of the common controls.
    /// </summary>
    /// <remarks>
    /// No document says how many decimals "the units' decimals" are (wave-1 question #46); these are the workaround.
    /// </remarks>
    public static int PositionDecimals(Units units)
    {
        return units == Units.Inch ? 4 : 3;
    }

    // The name of a frame in a message.
    private static string FrameName(PositionFrame frame)
    {
        return frame switch
        {
            PositionFrame.Machine => "MACHINE",
            PositionFrame.Polar => "polar",
            PositionFrame.Cylinder => "cylinder",
            _ => "workpiece",
        };
    }

    // One form per axis is kept under the axis of an axis word, and under CENTER and its plane axis for a CENTER word.
    private static string? FormKey(Word word, BlockContext context)
    {
        if (context.AxisOf.TryGetValue(word, out string? axis))
        {
            return axis;
        }

        return word.Key == "CENTER" && word.Addr is string address ? "CENTER:" + WithoutIncrement(address) : null;
    }
}
