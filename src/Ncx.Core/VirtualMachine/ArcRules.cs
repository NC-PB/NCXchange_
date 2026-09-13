using System.Globalization;
using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// ARC (virtual machine 3.2): the working plane of WORKPLANE, or the polar or the cylinder plane of D102; the start,
/// which must be known in the plane; the end, the start with the axis words applied; the arc resolved in the CENTER
/// form with the tolerance check, the R form with the center formula or the ANGLE form of D84 (ArcResolver), with the
/// arc tolerance of the configuration (D36); under DIAMETER=ON the X words halved (D60).
/// </summary>
internal static class ArcRules
{
    /// <summary>
    /// The arc tolerance from the machine configuration, by default 0.01 mm and 0.0005 in (virtual machine 3.2, D36).
    /// </summary>
    public static decimal Tolerance(VmOptions options, Units units)
    {
        // Machine-config names no key for the tolerance, so the options carry it, null for the defaults of D36 (wave-1
        // questions #54 and #91). Before UNITS, itself an ERROR, the millimetre default applies.
        if (options.ArcTolerance is decimal configured)
        {
            return configured;
        }

        return units == Units.Inch ? 0.0005m : 0.01m;
    }

    /// <summary>
    /// The working plane of ARC: under POLAR=ON the face plane of the X word and the C word, under CYLINDER=n the plane
    /// of the cylinder axis and the C word (D102), otherwise the plane of WORKPLANE with the G17, G18 and G19
    /// orientation (language 4.2, virtual machine 3.2).
    /// </summary>
    public static Plane PlaneOf(FrameState frame)
    {
        if (frame.Polar)
        {
            return Plane.Polar;
        }

        if (frame.Cylinder is not null)
        {
            return Plane.Cylinder;
        }

        return frame.Workplane switch
        {
            Workplane.ZX => Plane.ZX,
            Workplane.YZ => Plane.YZ,
            _ => Plane.XY,
        };
    }

    /// <summary>
    /// Executes an ARC block.
    /// </summary>
    /// <param name="context">The ARC block with its axis words resolved (step 2).</param>
    /// <param name="arcTolerance">The arc tolerance in the active units (D36).</param>
    /// <returns>The arc in the coordinates of its plane; null when it does not resolve.</returns>
    public static PlaneArc? Execute(BlockContext context, decimal arcTolerance)
    {
        ChannelState state = context.State;
        Plane plane = PlaneOf(state.Frame);
        ArcWords words = ArcWords.Of(context, plane);
        if (!CheckWords(context, plane, words))
        {
            return null;
        }

        // Start = current position, which must be known in the plane: both plane axes known in the frame the plane
        // programs in (virtual machine 3.2, D102).
        string? firstAxis = KeyOf(context, words.FirstEnd, plane.FirstAxis);
        string? secondAxis = KeyOf(context, words.SecondEnd, plane.SecondAxis);
        PositionFrame firstFrame = MotionRules.FrameOf(state, plane.FirstAxis);
        PositionFrame secondFrame = MotionRules.FrameOf(state, plane.SecondAxis);
        decimal? startFirst = firstAxis is null ? null : MotionRules.CoordinateIn(state, firstAxis, firstFrame);
        decimal? startSecond = secondAxis is null ? null : MotionRules.CoordinateIn(state, secondAxis, secondFrame);
        if (firstAxis is null
            || secondAxis is null
            || startFirst is not decimal first
            || startSecond is not decimal second)
        {
            StartUnknown(context, plane, words, firstAxis, secondAxis);
            return null;
        }

        // End = start with the axis words applied (virtual machine 3.2). A tool-axis word makes a helix: the arc turns
        // in the plane while the tool axis travels from its start to the word's end; where the tool axis is unknown,
        // the plane geometry does not need it.
        decimal? endFirst = EndOf(words.FirstEnd, first, state);
        decimal? endSecond = EndOf(words.SecondEnd, second, state);
        string? toolAxis = KeyOf(context, words.ToolEnd, plane.ToolAxis);
        decimal? toolStart = toolAxis is null
            ? null
            : MotionRules.CoordinateIn(state, toolAxis, MotionRules.FrameOf(state, plane.ToolAxis));
        decimal? toolEnd = EndOf(words.ToolEnd, toolStart, state);
        decimal helixStart = toolStart ?? toolEnd ?? 0m;
        decimal helixEnd = toolEnd ?? helixStart;

        ArcDirection direction = context.Block.Verb is Word verb && BlockContext.IdentOf(verb) == "CCW"
            ? ArcDirection.Counterclockwise
            : ArcDirection.Clockwise;
        Vec3 start = Vec3.FromDecimals(first, second, helixStart);
        decimal? centerFirst = CenterOf(words.FirstCenter, first, state);
        decimal? centerSecond = CenterOf(words.SecondCenter, second, state);
        ArcResult? result = null;
        if (words.Angle is Word angleWord)
        {
            // The ANGLE form: the start rotated about CENTER by ANGLE degrees in the direction of the verb; a tool-axis
            // word distributes its travel over the whole sweep (virtual machine 3.2, D84).
            if (MotionRules.NumberOf(angleWord) is decimal angle
                && centerFirst is decimal angleCenterFirst
                && centerSecond is decimal angleCenterSecond)
            {
                Vec3 angleCenter = Vec3.FromDecimals(angleCenterFirst, angleCenterSecond, helixStart);
                result = ArcResolver.ResolveAngleForm(direction, start, (double)helixEnd, angleCenter, angle);
            }
        }
        else if (endFirst is decimal knownEndFirst && endSecond is decimal knownEndSecond)
        {
            Vec3 end = Vec3.FromDecimals(knownEndFirst, knownEndSecond, helixEnd);
            if (words.Radius is Word radiusWord)
            {
                // The R form: the center from the signed radius (virtual machine 3.2).
                if (MotionRules.NumberOf(radiusWord) is decimal radius)
                {
                    result = ArcResolver.ResolveRadiusForm(direction, start, end, radius, arcTolerance);
                }
            }
            else if (centerFirst is decimal knownCenterFirst && centerSecond is decimal knownCenterSecond)
            {
                // The CENTER form, |start - center| against |end - center| within the tolerance (virtual machine 3.2,
                // D36).
                Vec3 center = Vec3.FromDecimals(knownCenterFirst, knownCenterSecond, helixStart);
                result = ArcResolver.ResolveCenterForm(direction, start, end, center, arcTolerance);
            }
        }

        // A value from an expression is not evaluated in STATIC mode: the arc stays unresolved, and its end is known
        // where the words give it (virtual machine 1, 3.2).
        if (result is null)
        {
            Place(state, firstAxis, firstFrame, words.Angle is null ? endFirst : null);
            Place(state, secondAxis, secondFrame, words.Angle is null ? endSecond : null);
            MoveToolAndOtherAxes(context, words);
            return null;
        }

        if (result.Arc is not Arc arc)
        {
            Report(context, result.Error, arcTolerance);
            return null;
        }

        // The end goes into the position store: in the CENTER and R forms as the words give it, in the ANGLE form the
        // start rotated about the center, a double that reaches the store only rounded to a decimal (virtual machine
        // 3.2; D62, phase 1 risks).
        if (words.Angle is null)
        {
            Place(state, firstAxis, firstFrame, endFirst);
            Place(state, secondAxis, secondFrame, endSecond);
        }
        else
        {
            int decimals = MotionRules.PositionDecimals(state.Frame.Units);
            Place(state, firstAxis, firstFrame, Vec3.RoundToDecimal(arc.End.X, decimals));
            Place(state, secondAxis, secondFrame, Vec3.RoundToDecimal(arc.End.Y, decimals));
        }

        MoveToolAndOtherAxes(context, words);

        // The VM keeps the arc, the computed center of the R form and the sweep of the ANGLE form with it, so that the
        // compiler can write either form, or full turns plus a rest (virtual machine 3.2, D84).
        return new PlaneArc(plane, arc);
    }

    // The words an ARC block takes (language 4.3; virtual machine 3.2, 5; D84): CENTER, R or ANGLE; the ANGLE form with
    // CENTER and without R or plane end-point words; CENTER on both plane axes and on no other axis.
    private static bool CheckWords(BlockContext context, Plane plane, ArcWords words)
    {
        Block block = context.Block;
        Diagnostics diagnostics = context.Diagnostics;
        if (!words.HasCenter && words.Radius is null && words.Angle is null)
        {
            diagnostics.Error(block, DiagnosticCodes.ArcWithoutCenterRadiusOrAngle,
                "ARC goes to its target with CENTER or R, or over a sweep ANGLE around CENTER, and this one has none "
                + "of them (language 4.3, virtual machine 3.2).");
            return false;
        }

        bool valid = true;
        if (words.Angle is not null)
        {
            // The ANGLE form: CENTER required, no plane end-point words, no R (virtual machine 3.2, D84).
            if (words.Radius is not null)
            {
                diagnostics.Error(block, DiagnosticCodes.ArcAngleWithRadius,
                    "ANGLE turns around CENTER and takes no R (language 4.3, virtual machine 3.2, D84).");
                valid = false;
            }

            if ((words.FirstEnd ?? words.SecondEnd) is Word endPoint)
            {
                diagnostics.Error(block, DiagnosticCodes.ArcAngleWithPlaneEndPoint,
                    $"ANGLE gives the end as the start turned about CENTER and takes no end-point word on the plane "
                    + $"axes, not {endPoint.ToCanonical()} (language 4.3, virtual machine 3.2, D84).");
                valid = false;
            }

            if (!words.HasCenter)
            {
                diagnostics.Error(block, DiagnosticCodes.ArcAngleWithoutCenter,
                    "ANGLE is a sweep around CENTER, and the block has no CENTER (language 4.3, virtual machine 3.2, "
                    + "D84).");
                valid = false;
            }
        }
        else if (words.HasCenter && words.Radius is not null)
        {
            // TODO(question): language 4.3 gives ARC "CENTER or R", and neither it nor virtual machine 5 says what an
            // ARC with both is; the block says two things of one arc and is an ERROR until that is answered.
            diagnostics.Error(block, DiagnosticCodes.ArcCenterWithRadius,
                "ARC goes to its target with CENTER or with R, and this one has both (language 4.3).");
            valid = false;
        }

        // TODO(question): virtual machine 3.2 runs the arc only in the working plane and requires CENTER on both plane
        // axes; what CENTER on another axis is (CENTER:Z in the XY plane) is not said. It is an ERROR until that is
        // answered.
        if (words.OutsideCenter is Word outside)
        {
            diagnostics.Error(block, DiagnosticCodes.ArcCenterOutsideThePlane,
                $"{outside.ToCanonical()} is no axis of the {plane.Name} plane of {plane.FirstAxis} and "
                + $"{plane.SecondAxis}; the arc runs only in the working plane (virtual machine 3.2).");
            valid = false;
        }

        // CENTER:X absolute or CENTER:IX relative to the start point; both plane axes required (virtual machine 3.2).
        if (words.HasCenter && (words.FirstCenter is null || words.SecondCenter is null))
        {
            diagnostics.Error(block, DiagnosticCodes.ArcCenterWithoutBothPlaneAxes,
                $"CENTER needs both axes of the {plane.Name} plane, {plane.FirstAxis} and {plane.SecondAxis}, "
                + "absolute or incremental (virtual machine 3.2).");
            valid = false;
        }

        return valid;
    }

    // Start = current position, which must be known in the plane: ERROR (virtual machine 3.2). What an absolute word
    // gives is known afterwards all the same; every coordinate that follows from the unknown start is unknown (3.1).
    // TODO(question): virtual machine 3.9 and 5 suppress an incremental word from an unknown position inside a
    // subprogram that no program of the file calls, and do not name an ARC from an unknown start, which has the same
    // reason: the start belongs to a caller that does not exist (D99). It is suppressed there as well until that is
    // answered.
    private static void StartUnknown(BlockContext context, Plane plane, ArcWords words, string? firstAxis,
        string? secondAxis)
    {
        context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.ArcStartUnknownInThePlane,
            $"{context.Block.Verb?.ToCanonical()} starts at the current position, and it is not known in the "
            + $"{plane.Name} plane of {plane.FirstAxis} and {plane.SecondAxis} (virtual machine 3.2).");

        ChannelState state = context.State;
        PlaceAbsolute(state, firstAxis, words.FirstEnd, MotionRules.FrameOf(state, plane.FirstAxis));
        PlaceAbsolute(state, secondAxis, words.SecondEnd, MotionRules.FrameOf(state, plane.SecondAxis));
        MoveToolAndOtherAxes(context, words);
    }

    // A plane axis after an arc from an unknown start: at the value of an absolute word, unknown otherwise.
    private static void PlaceAbsolute(ChannelState state, string? axis, Word? word, PositionFrame frame)
    {
        if (axis is null)
        {
            return;
        }

        decimal? end = word is not null && !MotionRules.IsIncremental(word) ? MotionRules.ValueOf(word, state) : null;
        Place(state, axis, frame, end);
    }

    // The arc ERRORs of the geometry, each with its code (virtual machine 3.2, 5; language 4.3; D36, D84).
    private static void Report(BlockContext context, ArcError? error, decimal arcTolerance)
    {
        Block block = context.Block;
        Diagnostics diagnostics = context.Diagnostics;
        string arc = block.Verb?.ToCanonical() ?? "ARC";
        string tolerance = arcTolerance.ToString(CultureInfo.InvariantCulture);
        switch (error)
        {
            case ArcError.InconsistentCenter:
                diagnostics.Error(block, DiagnosticCodes.ArcInconsistentCenter,
                    $"{arc}: the start and the end lie at radii from CENTER that differ by more than the arc tolerance "
                    + $"{tolerance}: inconsistent center (virtual machine 3.2, D36).");
                break;
            case ArcError.RadiusTooSmall:
                diagnostics.Error(block, DiagnosticCodes.ArcRadiusTooSmall,
                    $"{arc}: the end lies farther from the start than twice |R| plus the arc tolerance {tolerance}: "
                    + "radius too small (virtual machine 3.2, D36).");
                break;
            case ArcError.FullCircleWithRadius:
                diagnostics.Error(block, DiagnosticCodes.ArcFullCircleWithRadius,
                    $"{arc}: the end is the start, a full circle, which needs CENTER, not R (language 4.3, virtual "
                    + "machine 3.2).");
                break;
            case ArcError.RadiusZero:
                diagnostics.Error(block, DiagnosticCodes.ArcRadiusZero,
                    $"{arc}: R is a number, not 0 (language 4.3).");
                break;
            case ArcError.AngleNotGreaterThanZero:
                diagnostics.Error(block, DiagnosticCodes.ArcAngleNotGreaterThanZero,
                    $"{arc}: ANGLE is a sweep in degrees greater than 0 (language 4.3, D84).");
                break;
        }
    }

    // The key of the position store of an axis of the plane: the axis the block's word names (step 2), or, where the
    // block has no word on it, the axis the name resolves to (virtual machine 3.8 rule 3).
    private static string? KeyOf(BlockContext context, Word? word, string name)
    {
        if (word is not null && context.AxisOf.TryGetValue(word, out string? axis))
        {
            return axis;
        }

        return context.Resources.KeyOfAxis(name, context.State);
    }

    // The end of an axis: its start without a word, the value of X=, the start plus the value of IX=; X and IX halved
    // under DIAMETER=ON (virtual machine 3.1, 3.2, D60); null for an expression, and for IX from an unknown start.
    private static decimal? EndOf(Word? word, decimal? start, ChannelState state)
    {
        if (word is null)
        {
            return start;
        }

        decimal? value = MotionRules.ValueOf(word, state);
        return MotionRules.IsIncremental(word) ? start + value : value;
    }

    // CENTER:X is absolute, halved under DIAMETER=ON; CENTER:IX is relative to the start point of the arc and a radius
    // value (virtual machine 3.2, D60); null without the word or for an expression.
    private static decimal? CenterOf(Word? word, decimal start, ChannelState state)
    {
        if (word is null || word.Addr is not string address)
        {
            return null;
        }

        decimal? value = MotionRules.ValueOf(word, state);
        return address.StartsWith('I') ? start + value : value;
    }

    // The tool-axis word of a helix and the other axis words of the block move their axes as in a linear motion
    // (virtual machine 3.1, 3.2).
    private static void MoveToolAndOtherAxes(BlockContext context, ArcWords words)
    {
        if (words.ToolEnd is Word tool && context.AxisOf.TryGetValue(tool, out string? toolAxis))
        {
            MotionRules.MoveAxis(context, tool, toolAxis);
        }

        foreach (Word word in words.OtherAxes)
        {
            if (context.AxisOf.TryGetValue(word, out string? axis))
            {
                MotionRules.MoveAxis(context, word, axis);
            }
        }
    }

    // An axis of the plane at a coordinate of its frame, unknown for none.
    private static void Place(ChannelState state, string axis, PositionFrame frame, decimal? coordinate)
    {
        state.Motion.Position[axis] = coordinate is decimal known
            ? MotionRules.PositionAt(state, axis, frame, known)
            : AxisPosition.Unknown;
    }
}
