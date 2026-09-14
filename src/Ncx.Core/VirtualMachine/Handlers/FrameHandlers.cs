using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The frame words of language 4.2 and the units and path tolerance of 4.1: the rows of virtual machine 2.1. The frame
/// verbs SHIFT, TILT, TILT_AXIS and SETPOS with their axis words are step 4, not state words (virtual machine 3); their
/// RESET forms are state words and handled here.
/// </summary>
internal static class FrameHandlers
{
    // The value of the reset forms of SHIFT, TILT, TILT_AXIS and ROTATE (language 4.2).
    private const string Reset = "RESET";

    /// <summary>
    /// Registers the frame words.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["UNITS"] = ApplyUnits;
        handlers["WORKPLANE"] = ApplyWorkplane;
        handlers["ORIGIN"] = ApplyOrigin;
        handlers["FRAME"] = ApplyFrame;
        handlers["DIAMETER"] = ApplyDiameter;
        handlers["SHIFT"] = ApplyShiftReset;
        handlers["TILT"] = ApplyTiltReset;
        handlers["TILT_AXIS"] = ApplyTiltAxisReset;
        handlers["ROTATE"] = ApplyRotate;
        handlers["MIRROR"] = ApplyMirror;
        handlers["CYLINDER"] = ApplyCylinder;
        handlers["POLAR"] = ApplyPolar;
        handlers["TCPM"] = ApplyTcpm;
        handlers["ROTARY_PATH"] = ApplyRotaryPath;
        handlers["ROTARY_FEED"] = ApplyRotaryFeed;
        handlers["TOLERANCE"] = ApplyTolerance;
        handlers["TOLERANCE_MODE"] = ApplyToleranceMode;
    }

    // UNITS=MM or INCH, UNKNOWN until then (language 4.1, virtual machine 2.1).
    private static void ApplyUnits(Word word, BlockContext context)
    {
        context.State.Frame.Units = BlockContext.IdentOf(word) == "INCH" ? Units.Inch : Units.Mm;
    }

    // WORKPLANE=XY, ZX or YZ, the tool axis perpendicular to it (language 4.2).
    private static void ApplyWorkplane(Word word, BlockContext context)
    {
        context.State.Frame.Workplane = WorkplaneOf(word);
    }

    private static Workplane WorkplaneOf(Word word)
    {
        return BlockContext.IdentOf(word) switch
        {
            "ZX" => Workplane.ZX,
            "YZ" => Workplane.YZ,
            _ => Workplane.XY,
        };
    }

    // ORIGIN=n selects the workpiece datum, empties the chain and clears the setpos shifts (language 4.2, virtual
    // machine 2.1, 3.4, D31).
    private static void ApplyOrigin(Word word, BlockContext context)
    {
        if (BlockContext.IntegerOf(word) is int origin)
        {
            FrameRules.SelectOrigin(context.State, origin);
        }
    }

    // FRAME=MACHINE: the coordinates of this block refer to the machine datum, for one block (language 4.2, virtual
    // machine 2.1, D35); step 6 ends it.
    private static void ApplyFrame(Word word, BlockContext context)
    {
        context.State.Frame.MachineFrameBlock = BlockContext.IdentOf(word) == "MACHINE";
    }

    // DIAMETER=ON or OFF: the X words are diameters, and the virtual machine stores radii (language 4.2, D28, D60).
    private static void ApplyDiameter(Word word, BlockContext context)
    {
        context.State.Frame.Diameter = BlockContext.IdentOf(word) == "ON";
    }

    // SHIFT=RESET removes the shift from the chain and everything appended after it (language 4.2, virtual machine
    // 2.1, D31); SHIFT with axis words is the verb of step 4.
    private static void ApplyShiftReset(Word word, BlockContext context)
    {
        ApplyReset(word, context, TransformKind.Shift);
    }

    // TILT=RESET removes the tilt and what follows it (language 4.2); TILT with angles is the verb of step 4.
    private static void ApplyTiltReset(Word word, BlockContext context)
    {
        ApplyReset(word, context, TransformKind.Tilt);
    }

    // TILT_AXIS=RESET removes it and what follows (language 4.2, D82); TILT_AXIS with angles is the verb of step 4.
    private static void ApplyTiltAxisReset(Word word, BlockContext context)
    {
        ApplyReset(word, context, TransformKind.TiltAxis);
    }

    private static void ApplyReset(Word word, BlockContext context, TransformKind kind)
    {
        if (BlockContext.IdentOf(word) == Reset)
        {
            FrameRules.Cut(context.State, kind);
        }
    }

    // ROTATE=deg appends a rotation of the working plane about the tool axis to the chain; ROTATE=RESET removes it and
    // what follows (language 4.2, virtual machine 2.1).
    private static void ApplyRotate(Word word, BlockContext context)
    {
        if (BlockContext.IdentOf(word) == Reset)
        {
            FrameRules.Cut(context.State, TransformKind.Rotate);
            return;
        }

        // TODO(question): a chain entry has no UNKNOWN form for an angle from an expression in STATIC mode (virtual
        // machine 1); the entry keeps 0 for it, and the position is unknown after the rotation anyway (3.4).
        decimal angle = NumberOf(word) ?? 0m;

        // The rotation turns the working plane where it stands (language 4.2, D31): the state words of a block do not
        // depend on each other (virtual machine 3 step 3), so a WORKPLANE of the same block counts in either order.
        Workplane plane = context.Block.Find("WORKPLANE") is Word workplane
            ? WorkplaneOf(workplane)
            : context.State.Frame.Workplane;
        FrameRules.AppendTransform(context.State,
            new TransformEntry { Kind = TransformKind.Rotate, Angle = angle, Workplane = plane });
    }

    // MIRROR=X or MIRROR=X,Y appends the mirrored axes to the chain (language 4.2, virtual machine 2.1).
    private static void ApplyMirror(Word word, BlockContext context)
    {
        // TODO(question): language 4.2 gives MIRROR the value OFF where the other chain words have RESET; OFF is read
        // as the reset form of the mirror, cutting the chain at the last mirror, until that is answered.
        if (BlockContext.IdentOf(word) == "OFF")
        {
            FrameRules.Cut(context.State, TransformKind.Mirror);
            return;
        }

        IReadOnlyList<string> axes = word.Value switch
        {
            ListValue list => list.Items,
            IdentValue axis => [axis.Name],
            _ => [],
        };
        FrameRules.AppendTransform(context.State, new TransformEntry { Kind = TransformKind.Mirror, Mirrored = axes });
    }

    // CYLINDER=n switches the cylinder surface transformation on with the reference radius n, CYLINDER=OFF off; there
    // is no ON form (language 4.2, D96). Going off leaves the axes of the cylinder frame unknown in the workpiece frame
    // until the next motion with known coordinates (virtual machine 3.4, D102).
    private static void ApplyCylinder(Word word, BlockContext context)
    {
        FrameState frame = context.State.Frame;
        if (BlockContext.IdentOf(word) == "OFF")
        {
            frame.Cylinder = null;
            context.State.Unknown.Remove("CYLINDER");
            FrameRules.LeaveTransformation(context.State, PositionFrame.Cylinder);
            return;
        }

        // A radius from an expression is UNKNOWN in STATIC mode (virtual machine 1); the transformation is on.
        frame.Cylinder = context.TryNumber(word, "CYLINDER", out decimal radius) ? radius : frame.Cylinder ?? 0m;
    }

    // POLAR=ON or OFF: the face transformation. Going off leaves X and C unknown in the workpiece frame until the next
    // motion with known coordinates (language 4.2, virtual machine 3.4, D102); the first motion under it puts them into
    // the polar frame (MotionRules.EnterTransformation).
    private static void ApplyPolar(Word word, BlockContext context)
    {
        bool on = BlockContext.IdentOf(word) == "ON";
        context.State.Frame.Polar = on;
        if (!on)
        {
            FrameRules.LeaveTransformation(context.State, PositionFrame.Polar);
        }
    }

    // TCPM=ON or OFF, state only (language 4.2, D54); the tool vector and the surface normal are unknown again after
    // TCPM=OFF (virtual machine 2.2, D81).
    private static void ApplyTcpm(Word word, BlockContext context)
    {
        bool on = BlockContext.IdentOf(word) == "ON";
        context.State.Frame.Tcpm = on;
        if (!on)
        {
            context.State.Motion.ToolVector = null;
            context.State.Motion.SurfaceNormal = null;
        }
    }

    // ROTARY_PATH=SHORTEST or FULL, state only (language 4.2, D86).
    private static void ApplyRotaryPath(Word word, BlockContext context)
    {
        context.State.Frame.RotaryPath = BlockContext.IdentOf(word) == "SHORTEST"
            ? RotaryPath.Shortest
            : RotaryPath.Full;
    }

    // ROTARY_FEED=MM_MIN or DEG_MIN, state only (language 4.2, D86).
    private static void ApplyRotaryFeed(Word word, BlockContext context)
    {
        context.State.Frame.RotaryFeed = BlockContext.IdentOf(word) == "MM_MIN" ? RotaryFeed.MmMin : RotaryFeed.DegMin;
    }

    // TOLERANCE=value or OFF, TOLERANCE:ROTARY=degrees: state only, written through by the compiler (language 4.1,
    // virtual machine 2.1, D85). OFF returns to the control's default; the rotary tolerance stands only with TOLERANCE.
    private static void ApplyTolerance(Word word, BlockContext context)
    {
        FrameState frame = context.State.Frame;
        if (word.Addr is not null)
        {
            string rotaryKey = BlockContext.StateKey("TOLERANCE", word.Addr);
            decimal? rotary = context.TryNumber(word, rotaryKey, out decimal degrees)
                ? degrees
                : frame.Tolerance.Rotary;
            frame.Tolerance = frame.Tolerance with { Rotary = rotary };
            return;
        }

        if (BlockContext.IdentOf(word) == "OFF")
        {
            frame.Tolerance = frame.Tolerance with { Value = null, Rotary = null };
            context.State.Unknown.Remove("TOLERANCE");
            context.State.Unknown.Remove(BlockContext.StateKey("TOLERANCE", "ROTARY"));
            return;
        }

        decimal? tolerance = context.TryNumber(word, "TOLERANCE", out decimal value)
            ? value
            : frame.Tolerance.Value ?? 0m;
        frame.Tolerance = frame.Tolerance with { Value = tolerance };
    }

    // TOLERANCE_MODE=FINISH or ROUGH (language 4.1, D85).
    private static void ApplyToleranceMode(Word word, BlockContext context)
    {
        ToleranceMode mode = BlockContext.IdentOf(word) == "ROUGH" ? ToleranceMode.Rough : ToleranceMode.Finish;
        context.State.Frame.Tolerance = context.State.Frame.Tolerance with { Mode = mode };
    }

    // A number of a chain word; null for an expression, which STATIC mode does not evaluate (virtual machine 1).
    private static decimal? NumberOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }
}
