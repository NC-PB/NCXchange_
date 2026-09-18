using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The arcs of a Fanuc block (controllers fanuc.md 4; controller-mapping 2; language 4.3; virtual machine 3.2): R
/// signed as the block writes it, or the centre as I J K incremental from the start point, and a sweep beyond 360
/// degrees split into full turns and a rest, since a Fanuc arc turns at most once (controller-mapping 2, ANGLE).
/// </summary>
internal static class FanucArcs
{
    /// <summary>
    /// R or I J K of an arc after its axis words.
    /// </summary>
    public static void Add(FanucBlock write)
    {
        Block block = write.Block;
        PlaneAxes(write, out string first, out string second, out _);
        if (block.Find("R") is Word radius)
        {
            // R signed: positive up to 180 degrees, negative beyond (language 4.3, R; controllers fanuc.md 4).
            write.Written(radius);
            string? value = FanucExpressions.WordValue(write, FanucMotion.DecimalsAddress(write, first), radius.Value,
                1m);
            if (value is not null)
            {
                write.Main.Word("R" + value);
            }

            return;
        }

        write.Written("CENTER");
        MotionEvent? motion = MotionOf(write.Step);
        string? firstOffset = CentreOffset(write, motion, first);
        string? secondOffset = CentreOffset(write, motion, second);
        if (firstOffset is not null && secondOffset is not null)
        {
            write.Main.Word(CentreLetter(first) + firstOffset);
            write.Main.Word(CentreLetter(second) + secondOffset);
        }
    }

    /// <summary>
    /// An arc over a sweep ANGLE (D84): each full turn ends at its start point, the rest at the end point of the
    /// virtual machine, and the travel of the tool axis is shared out over the sweep (virtual machine 3.2, ANGLE form;
    /// controller-mapping 2, ANGLE: split into arcs of at most 360 degrees with I J K).
    /// </summary>
    public static void WriteTurns(FanucBlock write, List<FanucAxisWord> axes)
    {
        write.Written("ANGLE");
        write.Written("CENTER");
        foreach (FanucAxisWord axis in axes)
        {
            write.Written(axis.Word);
        }

        PlaneAxes(write, out string first, out string second, out string toolAxis);
        MotionEvent? motion = MotionOf(write.Step);
        if (motion?.Sweep is not decimal sweep || !Known(motion.From, first, second) || !Known(motion.To, first, second)
            || motion.Center is null || !Known(motion.Center, first, second))
        {
            write.Error(DiagnosticCodes.FanucArcStartUnknown,
                "The start, the centre or the end of the sweep is not known, so its turns cannot be written with I J K "
                + "(controllers fanuc.md 4; D84).");
            return;
        }

        // The turn points are absolute: G90 on a mill and in systems B and C; system A has no G90 and G91, where G90 is
        // the simple turning cycle, and writes them with the absolute letters X Z (controllers fanuc.md 3).
        if (write.System != GcodeSystem.A)
        {
            FanucMotion.Change(write, FanucCodes.Distance, "G90");
        }

        string code = write.Block.Verb?.Value.ToCanonical() == "CCW" ? "G3" : "G2";
        string centre = CentreLetter(first) + Offset(write, motion, first) + " " + CentreLetter(second)
            + Offset(write, motion, second);
        bool helix = motion.From.TryGetValue(toolAxis, out AxisPosition fromTool) && fromTool.Known
            && motion.To.TryGetValue(toolAxis, out AxisPosition toTool) && toTool.Known
            && fromTool.Value != toTool.Value;
        decimal swept = 0m;
        bool firstTurn = true;
        for (decimal remaining = sweep; remaining > 0m; remaining -= 360m)
        {
            decimal turn = Math.Min(remaining, 360m);
            swept += turn;
            bool last = remaining <= 360m;
            var words = new List<string>
            {
                Point(write, first, (last ? motion.To : motion.From)[first].Value),
                Point(write, second, (last ? motion.To : motion.From)[second].Value),
            };
            if (helix)
            {
                decimal from = motion.From[toolAxis].Value;
                decimal to = motion.To[toolAxis].Value;
                words.Add(Point(write, toolAxis, from + ((to - from) * swept / sweep)));
            }

            words.Add(centre);
            if (firstTurn)
            {
                foreach (string word in words)
                {
                    write.Main.Word(word);
                }

                firstTurn = false;
            }
            else
            {
                write.Following.Add(code + " " + string.Join(" ", words));
            }
        }
    }

    // The two axes of the working plane and its tool axis, X C under polar interpolation (language 4.2; D102).
    private static void PlaneAxes(FanucBlock write, out string first, out string second, out string toolAxis)
    {
        FrameSnapshot frame = write.After.Frame;
        (first, second, toolAxis) = frame.Polar ? ("X", "C", "Z") : frame.Workplane switch
        {
            Workplane.ZX => ("Z", "X", "Y"),
            Workplane.YZ => ("Y", "Z", "X"),
            _ => ("X", "Y", "Z"),
        };
    }

    // I J K name the centre offset along X, Y and Z whatever the plane; under polar interpolation J is the offset along
    // C (controllers fanuc.md 4).
    private static string CentreLetter(string axis)
    {
        return axis switch
        {
            "X" => "I",
            "Y" or "C" => "J",
            _ => "K",
        };
    }

    // The centre offset of one plane axis: CENTER:IX as the block writes it, a radius value (D60), or the absolute
    // CENTER:X as the offset from the start point, which the virtual machine resolved (virtual machine 3.2).
    private static string? CentreOffset(FanucBlock write, MotionEvent? motion, string axis)
    {
        if (write.Block.Find("CENTER", "I" + axis) is Word incremental)
        {
            return FanucExpressions.WordValue(write, FanucMotion.DecimalsAddress(write, axis), incremental.Value, 1m);
        }

        if (motion?.Center is not null && Known(motion.Center, axis, axis) && Known(motion.From, axis, axis))
        {
            return Offset(write, motion, axis);
        }

        write.Error(DiagnosticCodes.FanucArcStartUnknown,
            $"The start of the arc is not known, so the offset of its centre along {axis} cannot be written as "
            + $"{CentreLetter(axis)} (controllers fanuc.md 4; controller-mapping 2, CENTER:X).");
        return null;
    }

    // The centre minus the start point, in radius values as the virtual machine stores them (D28, D60).
    private static string Offset(FanucBlock write, MotionEvent motion, string axis)
    {
        return write.FormatComputed(FanucMotion.DecimalsAddress(write, axis),
            motion.Center![axis].Value - motion.From[axis].Value);
    }

    // A point of the virtual machine as the machine writes it, a diameter where it writes one (D60).
    private static string Point(FanucBlock write, string axis, decimal value)
    {
        string letter = FanucAxes.LetterOf(write, axis, incremental: false) ?? axis;
        return letter + write.FormatComputed(FanucMotion.DecimalsAddress(write, axis),
            value * FanucAxes.StateFactor(write, axis));
    }

    private static bool Known(IReadOnlyDictionary<string, AxisPosition> points, string first, string second)
    {
        return points.TryGetValue(first, out AxisPosition one) && one.Known
            && points.TryGetValue(second, out AxisPosition two) && two.Known;
    }

    private static MotionEvent? MotionOf(BlockStep step)
    {
        foreach (VmEvent vmEvent in step.Events)
        {
            if (vmEvent is MotionEvent motion)
            {
                return motion;
            }
        }

        return null;
    }
}
