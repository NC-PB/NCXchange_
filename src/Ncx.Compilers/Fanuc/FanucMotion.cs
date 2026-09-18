using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The motion of a Fanuc block (controllers fanuc.md 3, 4, 10 rule 1; controller-mapping 2; language 4.3): G0 and G1
/// on change, G2 and G3 with the end point and I J K or R, sweeps beyond 360 degrees split into turns, G90 or G91 by
/// the words of the block, the plane, the feed mode, the units and the compensation where they change, F where the
/// control's feed differs, G53 for FRAME=MACHINE, G4 for DWELL.
/// </summary>
internal static class FanucMotion
{
    // The verbs that interpolate in the working plane and are written with the plane (language 4.3).
    private static readonly string[] s_planeMotions = ["RAPID", "LINE", "ARC", "CYCLE_CALL"];

    /// <summary>
    /// The modal words of the block as their codes where the control has another active: UNITS as G20/G21, FEED_MODE
    /// as G94/G95 (G98/G99 in system A), WORKPLANE as G17 to G19, COMP as G40 to G42 (controllers fanuc.md 3, 10 rule
    /// 1; controller-mapping 1, 2).
    /// </summary>
    public static void WriteModalWords(FanucBlock write)
    {
        Block block = write.Block;
        ChannelSnapshot after = write.After;
        if (block.Has("UNITS"))
        {
            write.Written("UNITS");
            if (FanucCodes.UnitsCode(after.Frame.Units, write.System) is string units)
            {
                Change(write, FanucCodes.Units, units);
            }
        }

        if (block.Has("FEED_MODE"))
        {
            write.Written("FEED_MODE");
            Change(write, FanucCodes.FeedMode, FanucCodes.FeedModeCode(after.Motion.FeedMode, write.System));
        }

        // The plane stands in the motion block itself, and under plane_with_first_motion only there (the TODO(question)
        // of OutputFormat).
        if (block.Has("WORKPLANE"))
        {
            write.Written("WORKPLANE");
            bool moves = block.Verb is Word verb && s_planeMotions.Contains(verb.Key);
            if (!moves && write.Machine.Format?.PlaneWithFirstMotion != true)
            {
                WritePlane(write);
            }
        }

        // G41 and G42 apply from the motion of the same block, G40 ends the compensation (controllers fanuc.md 4).
        if (block.Has("COMP"))
        {
            write.Written("COMP");
            Change(write, FanucCodes.Comp, FanucCodes.CompCode(after.Motion.Comp));
        }
    }

    /// <summary>
    /// The code of the working plane where the control has another active (language 4.2, WORKPLANE).
    /// </summary>
    public static void WritePlane(FanucBlock write)
    {
        Change(write, FanucCodes.Plane, FanucCodes.PlaneCode(write.After.Frame.Workplane));
    }

    /// <summary>
    /// A modal code of a group where the control has another active, a modal word written only on change (phase 3,
    /// P3-03; controllers fanuc.md 3, 10 rule 1).
    /// </summary>
    public static void Change(FanucBlock write, string group, string code)
    {
        if (write.Target.Changes(group, code))
        {
            write.Main.Code(FanucCodes.RankOf(group), code);
        }
    }

    /// <summary>
    /// Writes RAPID, LINE and ARC (language 4.3; controllers fanuc.md 4, 10 rule 1).
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        string? verb = block.Verb?.Key;
        if (verb is not ("RAPID" or "LINE" or "ARC"))
        {
            return;
        }

        write.Written(verb);
        bool machineFrame = block.Has("FRAME", null, "MACHINE");
        if (machineFrame)
        {
            // TODO(question): D242: the rate of G53 is open; a G53 block moves at rapid whatever group 01 holds, as
            // D242 recommends, so LINE and ARC in machine coordinates have no Fanuc form, until D242 is answered.
            write.Written("FRAME");
            if (verb != "RAPID")
            {
                write.Error(DiagnosticCodes.FanucFeedMotionInMachineFrame,
                    $"{verb} with FRAME=MACHINE has no Fanuc form: G53 moves at rapid (controllers fanuc.md 4; D242).");
                write.WrittenAll();
                return;
            }
        }

        FanucCycles.CancelBeforeMotion(write);
        WriteMotionCode(write, verb, block.Verb!.Value);
        if (machineFrame)
        {
            write.Main.Code(FanucCodes.OneShot, "G53");
        }

        WritePlane(write);
        List<FanucAxisWord> axes = FanucAxes.Of(block);
        if (verb == "ARC" && block.Has("ANGLE"))
        {
            FanucArcs.WriteTurns(write, axes);
        }
        else if (!AddAxisWords(write, axes, machineFrame))
        {
            return;
        }
        else if (verb == "ARC")
        {
            FanucArcs.Add(write);
        }

        // G53 ends at a machine position, which the length offset does not move (controllers fanuc.md 4), so an offset
        // that waits for the tool axis waits for the next move in the workpiece frame (the TODO(question) of
        // OutputFormat).
        AddToolVector(write);
        FanucToolWords.AddOffsets(write, HasToolAxisWord(write, axes) && !machineFrame);
        AddFeed(write, verb);
    }

    /// <summary>
    /// True when the block moves the tool axis in the workpiece frame, where the length offset applies: a RAPID, LINE
    /// or ARC with a word of the tool axis of its plane outside G53, or a CYCLE_CALL, which drills along it (language
    /// 4.2, WORKPLANE; 4.3; 4.7).
    /// </summary>
    public static bool MovesToolAxis(FanucBlock write)
    {
        Block block = write.Block;
        return block.Verb?.Key switch
        {
            "CYCLE_CALL" => true,
            "RAPID" or "LINE" or "ARC" => !block.Has("FRAME", null, "MACHINE")
                && HasToolAxisWord(write, FanucAxes.Of(block)),
            _ => false,
        };
    }

    /// <summary>
    /// True when the block is a SETPOS with a word of the tool axis of its plane: the control declares that position
    /// with the length offset it has active, as the virtual machine declares it with OFFSET:LEN active (language 4.2,
    /// SETPOS and WORKPLANE; 4.4, OFFSET:LEN is modal).
    /// </summary>
    public static bool DeclaresToolAxis(FanucBlock write)
    {
        return write.Block.Verb?.Key == "SETPOS" && HasToolAxisWord(write, FanucAxes.Of(write.Block));
    }

    /// <summary>
    /// The axis words of a motion as the control takes them, all absolute under G90 or all incremental under G91, or in
    /// G-code system A with the incremental letters (controllers fanuc.md 3, 10 rule 1; controller-mapping 2, IX); a
    /// block that mixes the two is written absolute from the point the virtual machine reached. False after an ERROR.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="axes">The axis words of the block.</param>
    /// <param name="machineFrame">True under G53, which takes absolute words only.</param>
    // TODO(question): fanuc 10 rule 1 writes absolute values, or G91 blocks where the NCX block had incremental words,
    // "a machine setting" that machine-config does not name; the compiler writes G91 blocks, which keep an incremental
    // subprogram right at every call (language 4.13), until that is answered.
    public static bool AddAxisWords(FanucBlock write, List<FanucAxisWord> axes, bool machineFrame)
    {
        if (axes.Count == 0)
        {
            return true;
        }

        bool systemA = write.System == GcodeSystem.A;
        bool anyAbsolute = axes.Exists(axis => !axis.Incremental);
        bool anyIncremental = axes.Exists(axis => axis.Incremental);
        bool asAbsolute = !systemA && anyIncremental && (anyAbsolute || machineFrame);
        if (!systemA)
        {
            Change(write, FanucCodes.Distance, anyIncremental && !asAbsolute ? "G91" : "G90");
        }

        foreach (FanucAxisWord axis in axes)
        {
            write.Written(axis.Word);
            string? word = asAbsolute && axis.Incremental
                ? AbsoluteWord(write, axis)
                : Word(write, axis);
            if (word is null)
            {
                return false;
            }

            write.Main.Word(word);
        }

        return true;
    }

    /// <summary>
    /// One axis word as written: the letter of the axis and the value of the program, a diameter where the machine
    /// writes one (D60); null after an ERROR.
    /// </summary>
    public static string? Word(FanucBlock write, FanucAxisWord axis)
    {
        string? letter = FanucAxes.LetterOf(write, axis.Axis, axis.Incremental);
        if (letter is null)
        {
            write.Error(DiagnosticCodes.FanucIncrementalWordNotWritable,
                $"{axis.Word.ToCanonical()} is incremental, and in G-code system A the axis {axis.Axis} has no "
                + "incremental letter of [[axis]] (controllers fanuc.md 3; machine-config 4).");
            return null;
        }

        string? value = FanucExpressions.WordValue(write, DecimalsAddress(write, axis.Axis), axis.Word.Value,
            FanucAxes.WordFactor(write, axis.Axis));
        return value is null ? null : letter + value;
    }

    /// <summary>
    /// The address whose decimals of [format] a coordinate of an axis takes: the axis, else its letter without the
    /// digits of a machine axis (machine-config 2).
    /// </summary>
    public static string DecimalsAddress(FanucBlock write, string axis)
    {
        return write.Numbers.DecimalsOf(axis) is null && axis.Length > 1 ? axis.Substring(0, 1) : axis;
    }

    /// <summary>
    /// G4 with P in milliseconds for DWELL, which NCX gives in seconds (controllers fanuc.md 4; controller-mapping 1,
    /// DWELL; language 4.1).
    /// </summary>
    // TODO(question): D159: the unit of G4 P is milliseconds or seconds by a parameter that no key of the machine file
    // carries; P is written in milliseconds, as fanuc 4 writes it and the reader reads it, until D159 is answered.
    public static void WriteDwell(FanucBlock write)
    {
        if (write.Block.Find("DWELL") is not Word dwell)
        {
            return;
        }

        write.Written(dwell);
        if (FanucExpressions.WordValue(write, "P", dwell.Value, 1000m) is string milliseconds)
        {
            write.Write("G4 P" + milliseconds);
        }
    }

    // G0 and G1 are written only when the verb changes (controllers fanuc.md 10 rule 1), and the first motion after a
    // tool change writes its code under motion_code_after_tool_change (the TODO(question) of OutputFormat).
    // TODO(question): fanuc 10 rule 1 names G0 and G1 and not G2 and G3; an arc writes G2 or G3 on every block, as the
    // sources write it, which is right whatever the control has active, until that is answered.
    private static void WriteMotionCode(FanucBlock write, string verb, Value value)
    {
        string code = verb switch
        {
            "RAPID" => "G0",
            "LINE" => "G1",
            _ => value.ToCanonical() == "CCW" ? "G3" : "G2",
        };
        if (verb == "ARC" || write.Target.ActiveOf(FanucCodes.Motion) != code)
        {
            write.Main.Code(FanucCodes.RankOf(FanucCodes.Motion), code);
        }

        write.Target.Set(FanucCodes.Motion, code);
    }

    /// <summary>
    /// An incremental word of a block that also has absolute words, or of a G53 block, or of a canned cycle, written as
    /// the absolute point the virtual machine reached (virtual machine 3.1); null after an ERROR.
    /// </summary>
    public static string? AbsoluteWord(FanucBlock write, FanucAxisWord axis)
    {
        if (!write.After.Motion.Position.TryGetValue(axis.Axis, out AxisPosition position) || !position.Known)
        {
            write.Error(DiagnosticCodes.FanucMixedWordsUnknownTarget,
                $"{axis.Word.ToCanonical()} stands in a block with absolute words or under G53, and the point it "
                + "reaches is not known, so the block cannot be written in G90 (controllers fanuc.md 3, 4).");
            return null;
        }

        string letter = FanucAxes.LetterOf(write, axis.Axis, incremental: false) ?? axis.Axis;
        return letter + write.FormatComputed(DecimalsAddress(write, axis.Axis),
            position.Value * FanucAxes.StateFactor(write, axis.Axis));
    }

    // F where the feed of the control differs from the feed of NCX; a RAPID writes none. The feed mode stands with the
    // feed where the control has another (controllers fanuc.md 2, 3).
    // TODO(question): D218: where the F of a Fanuc cycle block stands is open; the compiler counts it as the control's
    // feed and writes F only where the NCX feed differs, as D218 recommends, until D218 is answered.
    private static void AddFeed(FanucBlock write, string verb)
    {
        Word? feedWord = write.Block.Find("F");
        if (feedWord is not null)
        {
            write.Written(feedWord);
        }

        if (verb == "RAPID")
        {
            return;
        }

        Change(write, FanucCodes.FeedMode, FanucCodes.FeedModeCode(write.After.Motion.FeedMode, write.System));
        string? feed = feedWord is not null
            ? FanucExpressions.WordValue(write, "F", feedWord.Value, 1m)
            : write.After.Motion.Feed is decimal value ? write.Format("F", value) : null;
        if (feed is not null && write.Target.Changes("F", feed))
        {
            write.Main.Word("F" + feed);
        }
    }

    // Under G43.5 the tool vector of a line is I J K (controller-mapping 2, tool vectors; D81; controllers fanuc.md 4:
    // "G43.5 (type 2, with the tool vector as I J K)"); the components are written as the program writes them, a unit
    // vector with no decimals of [format], each a real number with its point, K1. (fanuc 10 rule 5). The surface
    // normal NX NY NZ has no Fanuc form and stays unwritten (CMP308).
    private static void AddToolVector(FanucBlock write)
    {
        string[] keys = ["TX", "TY", "TZ"];
        string[] letters = ["I", "J", "K"];
        for (int index = 0; index < keys.Length; index++)
        {
            if (write.Block.Find(keys[index]) is Word component)
            {
                write.Written(component);
                if (FanucExpressions.WordValue(write, keys[index], component.Value, 1m) is string value)
                {
                    write.Main.Word(letters[index] + value);
                }
            }
        }
    }

    /// <summary>
    /// True when a LINE with a tool vector follows before TCPM=OFF or the end of the program, so that TCPM=ON is G43.5
    /// and not G43.4 (controllers fanuc.md 4; controller-mapping 1, TCPM; D81).
    /// </summary>
    public static bool VectorsFollow(FanucBlock write)
    {
        for (int index = write.Step.Index + 1; index < write.Steps.Count; index++)
        {
            Block block = write.Steps[index].Block;
            if (block.Has("TX"))
            {
                return true;
            }

            if (block.Has("TCPM") || block.Has("PROGRAM", null, "END"))
            {
                return false;
            }
        }

        return false;
    }

    // A word of the tool axis of the plane among the axis words of the block (language 4.2, WORKPLANE).
    private static bool HasToolAxisWord(FanucBlock write, List<FanucAxisWord> axes)
    {
        string toolAxis = FanucCycles.ToolAxisOf(write.After.Frame.Workplane);
        return axes.Exists(axis => axis.Axis == toolAxis);
    }
}
