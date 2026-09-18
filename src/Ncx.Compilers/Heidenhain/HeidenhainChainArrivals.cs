using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// What a JUMP or a REPEAT brings to the chain words and SETPOS after its label (language 4.2, 4.9; virtual machine 1,
/// 3.4, 3.6; D31). The STATIC walk records the jump and does not follow it, so ORIGIN, SHIFT, ROTATE, MIRROR, TILT,
/// TILT_AXIS, their RESET forms and the cycle 7 of SETPOS after a label are written for the frame the text brings to
/// the label (HeidenhainChain, HeidenhainSetpos), and the jump arrives with the frame the program has at the jump:
/// REPEAT runs the blocks from the label again on the chain the pass before left. Klartext has one text for every way,
/// so the way of each jump is followed from its label with the chain the program has on it and the transforms those
/// lines leave on the control there, and a block that runs where the two differ, or whose lines depend on more than
/// the chain, is reported on the jump (CMP119).
/// </summary>
internal static class HeidenhainChainArrivals
{
    // What the jump brings where its chain or setpos shifts differ from those of the way the text runs into the label.
    private const string OtherFrame =
        "with another chain of transforms or setpos shift than the way the text runs into it";

    // The chain words of language 4.2 by the kind of entry they append or cut: SHIFT, TILT and TILT_AXIS as verbs and
    // with RESET, ROTATE with an angle or RESET, MIRROR with axes or OFF (virtual machine 2.1; the TODO(question) of
    // the virtual machine for D124 reads MIRROR=OFF as the reset form).
    private static readonly (string Key, string Reset, TransformKind Kind)[] s_words =
    [
        ("SHIFT", "RESET", TransformKind.Shift),
        ("ROTATE", "RESET", TransformKind.Rotate),
        ("MIRROR", "OFF", TransformKind.Mirror),
        ("TILT", "RESET", TransformKind.Tilt),
        ("TILT_AXIS", "RESET", TransformKind.TiltAxis),
    ];

    // The words that leave the order of the text (language 4.9).
    private static readonly string[] s_flowWords = ["JUMP", "REPEAT", "RETURN", "CALL"];

    /// <summary>
    /// Follows the way of a jump from its label with the chain the program has on it and the transforms the lines
    /// written for the way the text runs leave on the control, and reports the first block that runs where they differ
    /// or whose lines act otherwise on that way (CMP119).
    /// </summary>
    /// <param name="writing">The block being written: the jump, or the label a jump forward waited for.</param>
    /// <param name="arrival">The way of the jump, with the state of the program at the jump.</param>
    /// <param name="way">What the label found on the way the text runs into it.</param>
    /// <param name="label">The step of the label in the walk order of the look-ahead.</param>
    public static void Check(HeidenhainBlock writing, HeidenhainArrival arrival, HeidenhainLabelWay way, int label)
    {
        if (arrival.Program is not ChannelSnapshot program)
        {
            return;
        }

        // The control has at the jump what the program has there, which the lines before the jump wrote. The cycle 7
        // of SETPOS stands at a place in the chain only while a setpos shift stands (HeidenhainSetpos).
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        ChannelSnapshot text = steps[label].Before;
        var followed = new HeidenhainChainWay
        {
            AtTheJump = program,
            Program = [.. program.Frame.Chain],
            Control = [.. program.Frame.Chain],
            SetposSame = HeidenhainSetpos.SameShifts(program, text)
                && (HeidenhainSetpos.StandingAxes(program).Count == 0 || arrival.SetposPlace == way.SetposPlace),
        };
        foreach (string axis in program.Motion.Position.Keys.Union(text.Motion.Position.Keys))
        {
            program.Motion.Position.TryGetValue(axis, out AxisPosition atTheJump);
            text.Motion.Position.TryGetValue(axis, out AxisPosition atTheLabel);
            if (atTheJump != atTheLabel)
            {
                followed.Displaced.Add(axis);
            }
        }

        // The way runs the blocks from the label in the order of the text, through later labels and into the walks of
        // the subprograms it calls, until the program ends (virtual machine 3.6, 3.9), or until both ways have the
        // same frame and every axis at the same place, from where every block acts alike on both.
        for (int index = label; index < steps.Count; index++)
        {
            BlockStep step = steps[index];
            if (HeidenhainArrivals.EndsTheRun(step) || Alike(followed, step))
            {
                return;
            }

            if ((Problem(followed, step) ?? Run(followed, step)) is string problem)
            {
                writing.Diagnostics.Error(arrival.Jump, DiagnosticCodes.HeidenhainLabelChainsDiffer,
                    $"{arrival.Word.ToCanonical()} reaches LBL {HeidenhainFlow.Label(arrival.Word.Value)} "
                    + problem + ": every way runs the blocks after the label with the frame it brings, and Klartext "
                    + "has one text for all of them (language 4.2, 4.9; virtual machine 3.4; controllers heidenhain.md "
                    + "3; D31).");
                return;
            }
        }
    }

    // Both ways have the same chain and setpos shifts, the control the transforms of the program, and every axis
    // stands at the same place on both.
    private static bool Alike(HeidenhainChainWay way, BlockStep step)
    {
        return way.Displaced.Count == 0 && SameFrame(way, step);
    }

    // Both ways have the same chain and setpos shifts before the step, and the control the transforms of the program.
    private static bool SameFrame(HeidenhainChainWay way, BlockStep step)
    {
        return way.ControlKnown && way.SetposSame && SameEntries(way.Program, step.Before.Frame.Chain)
            && SameEntries(way.Control, way.Program);
    }

    // The lines of the block that depend on more than the chain of the control: SETPOS, whose cycle 7 is the setpos
    // shift declared from the position and frame of the way the text runs (virtual machine 3.4; HeidenhainSetpos),
    // and, where the setpos shifts of the two ways differ, the cycle 7 of SETPOS that ORIGIN cancels, that a RESET may
    // move (HeidenhainSetpos.CheckRemoved) and that a SHIFT replaces (machine-config 3). The reason where the block
    // acts otherwise on the way of the jump; null where it does not.
    private static string? Problem(HeidenhainChainWay way, BlockStep step)
    {
        Block block = step.Block;
        string at = At(block);
        if (block.Verb?.Key == "SETPOS")
        {
            bool frame = SameFrame(way, step);
            string? displaced = HeidenhainAxes.Of(block)
                .FirstOrDefault(word => !word.Incremental && way.Displaced.Contains(word.Axis))?.Axis;
            if (!frame || displaced is not null)
            {
                string brings = frame
                    ? $"with {displaced} at another position than the way the text runs into it"
                    : OtherFrame;
                return $"{brings}, and the SETPOS {at} is written as the cycle 7 of the setpos shift the program "
                    + "declares on that way, not of the one it declares on the way of the jump";
            }
        }

        if (way.SetposSame)
        {
            return null;
        }

        ChainChange change = ChainWriter.Between(step.Before.Frame, step.After.Frame);
        if (block.Find("ORIGIN") is Word origin)
        {
            return $"{OtherFrame}, and the {origin.ToCanonical()} {at} cancels before cycle 247 the cycle 7 of the "
                + "setpos shifts of that way, not of those the program clears on the way of the jump";
        }

        if (change.Removed.Count > 0 || Removed(way.Program, block).Count > 0)
        {
            string reset = ResetOf(block)?.ToCanonical() ?? "RESET";
            return $"{OtherFrame}, and the {reset} {at} removes transforms that the cycle 7 of a SETPOS may follow, at "
                + "another place on that way than on the way of the jump";
        }

        bool shift = change.Appended.Any(entry => entry.Kind == TransformKind.Shift);
        if (shift && HeidenhainSetpos.StandingAxes(way.AtTheJump).Count > 0
            && HeidenhainSetpos.StandingAxes(step.After).Count == 0)
        {
            return "with the setpos shift of a SETPOS that the way the text runs into it has not, and the SHIFT "
                + $"{at} is written as a cycle 7 that replaces the cycle 7 of that SETPOS on the control, where the "
                + "program applies both (machine-config 3)";
        }

        return null;
    }

    // Runs the block on the way: the program cuts and appends its chain, the control takes the lines written for the
    // way the text runs, the cancels of what the program removes there and the cycles of what it appends there
    // (HeidenhainChain.Write, D31), and an absolute motion puts its axes at the same place on both ways. The reason
    // where a block runs while the control has other transforms than the program; null where none does.
    private static string? Run(HeidenhainChainWay way, BlockStep step)
    {
        Block block = step.Block;
        ChainChange change = ChainWriter.Between(step.Before.Frame, step.After.Frame);

        // ORIGIN empties the chain of the program (language 4.2), and the compiler cancels before cycle 247 what the
        // program removes on the way the text runs (HeidenhainChain.Write).
        // TODO(question): D253: whether cycle 247 ends an active cycle 7, 8 or 10 or tilted plane on the control is
        // open; a transform the jump brings that the cancels of the text leave stays active after cycle 247 here.
        if (block.Has("ORIGIN"))
        {
            way.Program.Clear();
        }

        List<TransformEntry> removed = Removed(way.Program, block);
        way.Program.RemoveRange(way.Program.Count - removed.Count, removed.Count);
        foreach (TransformEntry entry in change.Removed)
        {
            way.Cancel(entry);
        }

        bool given = true;
        foreach (TransformEntry entry in change.Appended)
        {
            way.Program.Add(entry);
            if (Written(step, entry))
            {
                given &= way.Write(entry);
            }
        }

        // The block that leaves the control with other transforms than the program is named, and the first block that
        // runs with them reports it; a later chain word may give the control the program's transforms again.
        string at = At(block);
        if (!given)
        {
            way.Differs = $"the {ChainWordOf(block)} {at} is written as a cycle that replaces a transform of its kind "
                + "the jump brings, which others follow on the control, and where it acts then is not given (D253)";
        }
        else if (way.ControlKnown && SameEntries(way.Control, way.Program))
        {
            way.Differs = null;
        }
        else
        {
            string origin = block.Has("ORIGIN") ? " (whether cycle 247 ends them is D253)" : "";
            way.Differs ??= $"the {ChainWordOf(block)} {at} is written for that way and leaves the control with other "
                + $"transforms than the program has on the way of the jump{origin}";
        }

        if (way.Differs is string differs && RunsWithTheFrame(step))
        {
            return $"with another chain of transforms than the way the text runs into it; {differs}, and the "
                + $"{RunWordOf(block)} on line {block.Line.ToString(CultureInfo.InvariantCulture)} runs with them";
        }

        if (block.Verb?.Key is "RAPID" or "LINE" or "ARC" or "HOME" or "CYCLE_CALL")
        {
            foreach (HeidenhainAxisWord axis in HeidenhainAxes.Of(block))
            {
                if (!axis.Incremental)
                {
                    way.Displaced.Remove(axis.Axis);
                }
            }
        }

        return null;
    }

    // The block runs with the transforms of the control: a motion in the workpiece frame, a cycle call or RETRACT along
    // the tool axis (language 4.2, 4.3; HOME and FRAME=MACHINE are M91 moves to machine coordinates, controllers
    // heidenhain.md 2 and 8 rule 5), or it may leave the order of the text, a JUMP, REPEAT, RETURN or a CALL of another
    // file, and the blocks it goes to run with them (virtual machine 3.6).
    private static bool RunsWithTheFrame(BlockStep step)
    {
        Block block = step.Block;
        bool workpiece = block.Verb?.Key is "RAPID" or "LINE" or "ARC" or "CYCLE_CALL"
            && !block.Has("FRAME", null, "MACHINE");
        bool external = block.Find("CALL")?.Value is StringValue name
            && !step.After.Flow.Subs.ContainsKey(name.Content);
        return workpiece || block.Verb?.Key == "RETRACT" || block.Has("JUMP") || block.Has("REPEAT")
            || block.Has("RETURN") || external;
    }

    // The compiler writes the cycle of an appended entry unless one of its kind, or for a SHIFT the cycle 7 of a
    // SETPOS, stands before it on the way the text runs (HeidenhainChain.Append, CMP106).
    private static bool Written(BlockStep step, TransformEntry entry)
    {
        foreach (TransformEntry earlier in step.After.Frame.Chain)
        {
            if (ReferenceEquals(earlier, entry))
            {
                break;
            }

            if (HeidenhainChain.FamilyOf(earlier.Kind) == HeidenhainChain.FamilyOf(entry.Kind))
            {
                return false;
            }
        }

        return entry.Kind != TransformKind.Shift || HeidenhainSetpos.StandingAxes(step.After).Count == 0;
    }

    // What the RESET forms of the block cut from a chain, the last entry first: each, in the order of the block, cuts
    // the chain at the last entry of its kind and everything after it (language 4.2; virtual machine 2.1).
    private static List<TransformEntry> Removed(List<TransformEntry> chain, Block block)
    {
        int cut = chain.Count;
        foreach (Word word in block.Words)
        {
            foreach ((string key, string reset, TransformKind kind) in s_words)
            {
                if (word.Key != key || word.Addr is not null || word.Value.ToCanonical() != reset || cut == 0)
                {
                    continue;
                }

                int last = chain.FindLastIndex(cut - 1, cut, entry => entry.Kind == kind);
                cut = last >= 0 ? last : cut;
            }
        }

        var removed = new List<TransformEntry>();
        for (int index = chain.Count - 1; index >= cut; index--)
        {
            removed.Add(chain[index]);
        }

        return removed;
    }

    // Where the block stands, as the message names it.
    private static string At(Block block)
    {
        return $"on line {block.Line.ToString(CultureInfo.InvariantCulture)} after the label";
    }

    // The chain word of the block, ORIGIN=1, SHIFT, ROTATE=RESET, as the message names it.
    private static string ChainWordOf(Block block)
    {
        if (block.Find("ORIGIN") is Word origin)
        {
            return origin.ToCanonical();
        }

        foreach ((string key, string _, TransformKind _) in s_words)
        {
            if (block.Find(key) is Word word)
            {
                return word.ToCanonical();
            }
        }

        return "block";
    }

    // The RESET form of the block, as the message names it.
    private static Word? ResetOf(Block block)
    {
        foreach ((string key, string reset, TransformKind _) in s_words)
        {
            if (block.Has(key, null, reset))
            {
                return block.Find(key);
            }
        }

        return null;
    }

    // The word the block runs with, RAPID, CYCLE_CALL, REPEAT=1, as the message names it.
    private static string RunWordOf(Block block)
    {
        if (block.Verb is Word verb)
        {
            return verb.Key;
        }

        foreach (string key in s_flowWords)
        {
            if (block.Find(key) is Word word)
            {
                return word.ToCanonical();
            }
        }

        return "block";
    }

    // Two lists of entries are the same where each entry is the same, one by one (SameEntry).
    private static bool SameEntries(List<TransformEntry> one, IReadOnlyList<TransformEntry> other)
    {
        if (one.Count != other.Count)
        {
            return false;
        }

        for (int index = 0; index < one.Count; index++)
        {
            if (!SameEntry(one[index], other[index]))
            {
                return false;
            }
        }

        return true;
    }

    // Two entries transform the frame alike: the same entry, or the same kind with the same known values, a shift of
    // 0 on an axis being none (language 4.2: omitted axes are 0). A value from an expression is UNKNOWN in STATIC mode
    // (virtual machine 1) and the same as no other.
    private static bool SameEntry(TransformEntry one, TransformEntry other)
    {
        if (ReferenceEquals(one, other))
        {
            return true;
        }

        return one.Kind == other.Kind && one.Angle is decimal && one.Angle == other.Angle
            && one.Workplane == other.Workplane && one.Move == other.Move && one.Rot == other.Rot
            && one.Mirrored.SequenceEqual(other.Mirrored)
            && SameValues(one.Shift, other.Shift) && SameValues(other.Shift, one.Shift)
            && SameValues(one.Angles, other.Angles) && SameValues(other.Angles, one.Angles);
    }

    // Each name of the first map has a known value that the second has as well, 0 where the second has none.
    private static bool SameValues(IReadOnlyDictionary<string, decimal?> one,
        IReadOnlyDictionary<string, decimal?> other)
    {
        foreach (KeyValuePair<string, decimal?> value in one)
        {
            decimal? same = other.TryGetValue(value.Key, out decimal? found) ? found : 0m;
            if (value.Value is not decimal known || same != known)
            {
                return false;
            }
        }

        return true;
    }
}
