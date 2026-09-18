using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The blocks where paths of the control meet, of which the STATIC walk takes one (virtual machine 1): a LABEL that a
/// jump reaches, which the control reaches from the block before it and from every JUMP to it, and the block after a
/// skipped one, which the control reaches with and without the skipped block (controller-mapping 1, SKIP; D53). There
/// the modal values that a block may take from the state before it without stating them, F, the feed mode, the plane,
/// the D register, the speed and the G96 or G97 of each spindle (language 2 rule 2, 4.3, 4.4, 4.5, 4.11), and the
/// active cycle (4.7), are compared over the paths: a value of NCX that is the same on every path stands again at its
/// next use, and one that differs is left to the control, which keeps the value of each path where the compiler wrote
/// every change of it (FanucBlock.Hold), and is CMP309 at the first block that takes it without stating it where the
/// control does not (FanucBlock.Lose).
/// </summary>
internal static partial class FanucPaths
{
    // The key of the feed in the target state, and of the D register (FanucMotion, FanucToolWords).
    private const string Feed = "F";
    private const string Radius = "D";

    // The key of the speed of a spindle in the target state is S: with the resource id, the key of its G96 or G97 CSS:
    // with the resource id (FanucToolWords).
    private const string SpeedPrefix = FanucToolWords.SpeedKey;
    private const string CssPrefix = FanucToolWords.CssKey;

    /// <summary>
    /// Before the N line of a LABEL that a jump reaches: every modal value whose value of NCX differs by the path to
    /// the label, or depends on the path the control came along (FanucBlock.Hold, Lose), is written where the control
    /// does not hold it and a line of its own can hold it, F100., G94, G17, so that the control holds the value of the
    /// path from the block before; the value of every other path is written before its jump (CheckJump).
    /// </summary>
    /// <param name="write">The LABEL block.</param>
    /// <param name="label">The label as NCX writes it.</param>
    /// <returns>The keys whose value differs by path, each true where the control holds it (FanucBlock.Hold) and
    /// false where it does not (FanucBlock.Lose).</returns>
    public static Dictionary<string, bool> MeetAtLabel(FanucBlock write, string label)
    {
        FanucMeeting meeting = write.Labels.MeetingAt(write.Step);
        foreach (string key in MeetingKeysOf(write))
        {
            // A value the control holds per path before the label differs by path as much as one whose values of the
            // walk differ (D53): the block before the label or a jump may come from a skipped block.
            bool differs = DiffersAt(write, label, key) || write.DependsOnThePath(key)
                || meeting.DependsByJump.Contains(key);
            if (!differs)
            {
                continue;
            }

            if (!HoldsAt(write, key, write.Before))
            {
                WriteAlone(write, key, write.Before, write.Writer);
            }

            // The control holds the value of every path where it holds the one of the block before the label here, and
            // the one of every jump; a jump written after the label is checked where it is written (CheckJump), unless
            // no line can hold its value at all.
            meeting.Decided[key] = HoldsAt(write, key, write.Before) && !meeting.NotHeldByJump.Contains(key)
                && JumpsCanHold(write, label, key);
        }

        return meeting.Decided;
    }

    /// <summary>
    /// Before the GOTO of a JUMP to a label: a modal value whose value of NCX differs by the path to the label is
    /// written where the control does not hold it and a line of its own can hold it, F100., G94, G17, S2000. Where no
    /// line can, the D register, which Fanuc writes with G41 and G42 (controllers fanuc.md 4), the S of a spindle whose
    /// M code is not the one the control saw last (fanuc 5) or which runs under G96, the blocks after the label take
    /// that value from the control: the first of them that takes it without stating it is CMP309, and none else
    /// (MeetAtLabel, FanucBlock.NeedsValueOfTheWalk).
    /// </summary>
    /// <param name="write">The JUMP block.</param>
    /// <param name="label">The label the jump goes to, as NCX writes it.</param>
    public static void CheckJump(FanucBlock write, string label)
    {
        BlockStep? labelStep = LabelStepOf(write, label);
        foreach (string key in MeetingKeysOf(write))
        {
            if (labelStep is not null && labelStep.Index <= write.Step.Index)
            {
                CheckJumpBack(write, label, key, labelStep);
            }
            else
            {
                CheckJumpForward(write, label, key, labelStep);
            }
        }
    }

    /// <summary>
    /// Before a skipped block that changes the feed, the feed mode or the plane, or that calls a subprogram: the value
    /// of NCX before it is written unskipped where the control does not hold it, so that the control holds it on the
    /// path that skips the block (controller-mapping 1, SKIP).
    /// </summary>
    public static void BeforeSkippedBlock(FanucBlock write)
    {
        foreach (string key in new[] { Feed, FanucCodes.FeedMode, FanucCodes.Plane })
        {
            FanucModalValue before = ValueOf(write, key, write.Before);
            bool changes = write.Block.Has("CALL") || before != ValueOf(write, key, write.After);
            if (changes && !Holds(write.Target.ActiveOf(key), before))
            {
                WriteAlone(write, key, write.Before, write.Writer);
            }
        }
    }

    /// <summary>
    /// At the first block after a skipped one in its program or walk: a modal value of NCX that is the same whether the
    /// switch skipped the block or not, and that the control held for one path before, stands again at its next use
    /// where the control may hold another; one that differs, or that the control holds per path on either side, is
    /// left to the control where it holds the value of both paths, and is CMP309 at its next use where it does not
    /// (controller-mapping 1, SKIP; D53).
    /// </summary>
    /// <param name="write">The first block after the skipped one.</param>
    /// <param name="before">What the target state held before the skipped block, the state of the path that skips
    /// it.</param>
    /// <param name="skippedBefore">The state of NCX before the skipped block.</param>
    public static void AfterSkippedBlock(FanucBlock write, Dictionary<string, string> before,
        ChannelSnapshot skippedBefore)
    {
        foreach (string key in KeysOf(write))
        {
            FanucModalValue skipped = ValueOf(write, key, skippedBefore);
            FanucModalValue run = ValueOf(write, key, write.Before);
            string? skippedControl = before.GetValueOrDefault(key);
            string? runControl = write.Target.ActiveOf(key);
            bool depends = FanucBlock.IsPathValue(skippedControl) || FanucBlock.IsPathValue(runControl);
            if (!depends && skipped.IsKnown && skipped == run)
            {
                if (skippedControl != runControl)
                {
                    write.MakeUnknown(key);
                }

                continue;
            }

            if (HoldsAt(write, key, skippedBefore, skippedControl) && HoldsAt(write, key, write.Before, runControl))
            {
                write.Hold(key);
            }
            else
            {
                write.Lose(key);
            }
        }

        // A skipped CYCLE block leaves the control no definition of either path, nor does one of a path before it
        // (MeetAtLabel).
        bool cycleDepends = FanucBlock.IsPathValue(before.GetValueOrDefault(FanucCycles.Definition))
            || write.DependsOnThePath(FanucCycles.Definition);
        if (cycleDepends || !Agree(write, FanucCycles.Definition, [skippedBefore, write.Before]))
        {
            write.Lose(FanucCycles.Definition);
        }
    }

    /// <summary>
    /// True for a key of the target state that AfterSkippedBlock and MeetAtLabel compare over the paths.
    /// </summary>
    public static bool IsModalValue(FanucBlock write, string key)
    {
        return KeysOf(write).Contains(key);
    }

    // A jump back to a label written before it: the label has left each value that differs by path to the control
    // (MeetAtLabel), so the control must hold here the value of NCX of this jump; where it cannot, the first block
    // after the label that takes the value without stating it is CMP309 (virtual machine 1; D53). A value the label
    // left to the blocks after it, the same on every path it saw, is taken from the walk there, which is wrong for
    // this jump where its value depends on the path the control came along.
    private static void CheckJumpBack(FanucBlock write, string label, string key, BlockStep labelStep)
    {
        FanucMeeting meeting = write.Labels.MeetingAt(labelStep);
        if (!meeting.Decided.TryGetValue(key, out bool heldAtLabel))
        {
            if (write.DependsOnThePath(key))
            {
                ReportTakenAfterLabel(write, label, key, labelStep);
            }

            return;
        }

        // A value the control does not hold after the label is CMP309 at every block that takes it already.
        if (!heldAtLabel)
        {
            return;
        }

        if (!HoldsAt(write, key, write.After))
        {
            WriteAlone(write, key, write.After, write.Write);
        }

        if (!HoldsAt(write, key, write.After))
        {
            ReportTakenAfterLabel(write, label, key, labelStep);
        }
    }

    // A jump to a label written after it: a value that differs by the path to the label is written where the control
    // does not hold it and a line of its own can hold it, and the label learns what the jump brings (MeetAtLabel).
    private static void CheckJumpForward(FanucBlock write, string label, string key, BlockStep? labelStep)
    {
        bool depends = write.DependsOnThePath(key);
        if ((depends || DiffersAt(write, label, key)) && !HoldsAt(write, key, write.After))
        {
            WriteAlone(write, key, write.After, write.Write);
        }

        if (labelStep is null)
        {
            return;
        }

        FanucMeeting meeting = write.Labels.MeetingAt(labelStep);
        if (depends)
        {
            meeting.DependsByJump.Add(key);
        }

        if (!HoldsAt(write, key, write.After))
        {
            meeting.NotHeldByJump.Add(key);
        }
    }

    // CMP309 at a jump back whose value the control does not hold, where a block after the label takes the value
    // without stating it before any block states it (virtual machine 1; language 4.3); none where no block takes it.
    private static void ReportTakenAfterLabel(FanucBlock write, string label, string key, BlockStep labelStep)
    {
        if (TakenAfterLabel(write, key, labelStep) is not BlockStep taker)
        {
            return;
        }

        string line = taker.Block.Line.ToString(CultureInfo.InvariantCulture);
        write.Error(DiagnosticCodes.FanucModalValueNotHeld,
            $"JUMP={label} reaches the label with {WordOf(write, key)} of a value that differs by path, which the "
            + $"control does not hold here, and the block of line {line} after the label takes it from the state "
            + "before it, so that block cannot be written right on every path (virtual machine 1; language 4.3).");
    }

    // The keys of the modal values a block may take from the state before it: F, the feed mode and the plane (language
    // 4.2, 4.3), the D register that G41 and G42 take (4.4), the speed of every spindle that M3 takes (4.5), and
    // whether an S of the spindle is its speed or its cutting speed, G97 or G96 (4.11).
    private static List<string> KeysOf(FanucBlock write)
    {
        var keys = new List<string> { Feed, FanucCodes.FeedMode, FanucCodes.Plane, Radius };
        foreach (ResourceDef resource in write.Machine.Resources)
        {
            if (resource.IsSpindle)
            {
                keys.Add(SpeedPrefix + resource.Id);
                keys.Add(CssPrefix + resource.Id);
            }
        }

        return keys;
    }

    // The keys compared where a jump reaches a label: the modal values and the active cycle, which a CYCLE_CALL takes
    // (language 4.7).
    private static List<string> MeetingKeysOf(FanucBlock write)
    {
        List<string> keys = KeysOf(write);
        keys.Add(FanucCycles.Definition);
        return keys;
    }

    // The value of NCX under a key in a state of the STATIC walk, as the compiler writes it, rounded to the decimals of
    // its address without the WARNING of the block that writes it (machine-config 2).
    private static FanucModalValue ValueOf(FanucBlock write, string key, ChannelSnapshot state)
    {
        if (key == Feed)
        {
            return state.Unknown.Contains(Feed) ? FanucModalValue.NotKnown
                : state.Motion.Feed is decimal feed ? FanucModalValue.Of(write.FormatComputed(Feed, feed))
                : FanucModalValue.None;
        }

        if (key == FanucCodes.FeedMode)
        {
            return FanucModalValue.Of(FanucCodes.FeedModeCode(state.Motion.FeedMode, write.System));
        }

        if (key == FanucCodes.Plane)
        {
            return FanucModalValue.Of(FanucCodes.PlaneCode(state.Frame.Workplane));
        }

        if (key == Radius)
        {
            return state.LastHolder is string holderId
                && state.Holders.TryGetValue(holderId, out HolderSnapshot? holder) && holder.OffsetRad != 0
                ? FanucModalValue.Of(holder.OffsetRad.ToString(CultureInfo.InvariantCulture))
                : FanucModalValue.None;
        }

        if (key == FanucCycles.Definition)
        {
            return FanucCycles.DefinitionOf(state.Cycle) is string definition
                ? FanucModalValue.Of(definition)
                : FanucModalValue.None;
        }

        if (key.StartsWith(CssPrefix, StringComparison.Ordinal))
        {
            return state.Spindles.TryGetValue(key.Substring(CssPrefix.Length), out SpindleSnapshot? mode)
                ? FanucModalValue.Of(mode.Css ? "G96" : "G97")
                : FanucModalValue.None;
        }

        string id = key.Substring(SpeedPrefix.Length);
        if (state.Unknown.Contains("RPM:" + id))
        {
            return FanucModalValue.NotKnown;
        }

        return state.Spindles.TryGetValue(id, out SpindleSnapshot? spindle) && spindle.Rpm != 0
            && FanucToolWords.SpeedTextOf(write, id, spindle) is string speed
            ? FanucModalValue.Of(speed)
            : FanucModalValue.None;
    }

    // True where the value of NCX under a key is the same known value in every state.
    private static bool Agree(FanucBlock write, string key, List<ChannelSnapshot> states)
    {
        FanucModalValue? first = null;
        foreach (ChannelSnapshot state in states)
        {
            FanucModalValue value = ValueOf(write, key, state);
            if (!value.IsKnown || (first is FanucModalValue seen && seen != value))
            {
                return false;
            }

            first = value;
        }

        return true;
    }

    // True where the control can hold a value of NCX of a path at all, so that a block after the paths meet may take
    // it from the control. Fanuc defines a cycle with its first call, so the control holds no cycle of a path (language
    // 4.7; controllers fanuc.md 6). Under G96 the S of the control is the cutting speed, and a spindle in AXIS mode is
    // a C axis, so the control holds no speed of such a spindle, and an S alone would change the cutting speed
    // (language 4.11: RPM is ignored while CSS is on; controllers fanuc.md 4). An S written, or a start, is right on a
    // path under G96 or under G97 only, so a G96 or G97 that differs by path cannot be held either.
    private static bool CanHold(string key, ChannelSnapshot state)
    {
        if (key == FanucCycles.Definition || key.StartsWith(CssPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return !key.StartsWith(SpeedPrefix, StringComparison.Ordinal)
            || !state.Spindles.TryGetValue(key.Substring(SpeedPrefix.Length), out SpindleSnapshot? spindle)
            || (!spindle.Css && spindle.Mode != SpindleMode.Axis);
    }

    // True where the control holds, on the path the target state is written for, the value of NCX of a state.
    private static bool HoldsAt(FanucBlock write, string key, ChannelSnapshot state)
    {
        return HoldsAt(write, key, state, write.Target.ActiveOf(key));
    }

    private static bool HoldsAt(FanucBlock write, string key, ChannelSnapshot state, string? active)
    {
        return CanHold(key, state) && Holds(active, ValueOf(write, key, state));
    }

    // True where every jump to the label arrives with a value the control can hold (CanHold).
    private static bool JumpsCanHold(FanucBlock write, string label, string key)
    {
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section == write.Step.Section && step.Block.Find("JUMP")?.Value.ToCanonical() == label
                && !CanHold(key, step.After))
            {
                return false;
            }
        }

        return true;
    }

    // True where the control holds a value of NCX: nothing to hold, the value itself, or the value of its path
    // (FanucBlock.Hold), which is all it can hold of a value from an expression.
    private static bool Holds(string? active, FanucModalValue value)
    {
        if (value.IsKnown && value.Text is null)
        {
            return true;
        }

        return FanucBlock.IsHeld(active) || (value.Text is string text && active == text);
    }

    // The value in a line of its own, where a line of its own holds it on Fanuc: F100. (controllers fanuc.md 2, 4; the
    // F word is modal, language 4.3), a modal G code alone (fanuc 3), S2000 where the S belongs to its spindle without
    // an M code, on a machine with one spindle or after that spindle's own M code (fanuc 5, 10 rule 2: never S alone
    // after another spindle's M code), and where it is the speed: never under G96 of NCX or of the control, nor in AXIS
    // mode (CanHold). The D register stands with G41 or G42 (fanuc 4), so it is not written alone. A value that
    // depends on the path the control came along is the value of the walk on one path only, so it is never written
    // (virtual machine 1; D53).
    private static void WriteAlone(FanucBlock write, string key, ChannelSnapshot state, Action<string> writer)
    {
        FanucModalValue value = ValueOf(write, key, state);
        if (value.Text is not string text || write.DependsOnThePath(key) || !CanHold(key, state))
        {
            return;
        }

        bool alone = key is Feed or FanucCodes.FeedMode or FanucCodes.Plane
            || (key.StartsWith(SpeedPrefix, StringComparison.Ordinal)
                && SpeedStandsAlone(write, key.Substring(SpeedPrefix.Length)));
        if (!alone)
        {
            return;
        }

        writer(key == Feed ? Feed + text : text);
        write.Target.Set(key, text);
    }

    // An S alone is the speed of the spindle where it belongs to it (FanucToolWords.SpeedStandsAlone) and the control
    // has G97 for it, not the G96 it wrote last or may have on another path (controllers fanuc.md 4).
    private static bool SpeedStandsAlone(FanucBlock write, string id)
    {
        string cssKey = CssPrefix + id;
        return FanucToolWords.SpeedStandsAlone(write, id) && write.Target.ActiveOf(cssKey) != "G96"
            && !write.DependsOnThePath(cssKey);
    }

    // The NCX word of a key, for a message.
    private static string WordOf(FanucBlock write, string key)
    {
        if (key.StartsWith(CssPrefix, StringComparison.Ordinal))
        {
            return "CSS of the spindle " + SpindleNameOf(write, key.Substring(CssPrefix.Length));
        }

        return key switch
        {
            Feed => "F",
            FanucCodes.FeedMode => "FEED_MODE",
            FanucCodes.Plane => "WORKPLANE",
            Radius => "OFFSET:RAD",
            FanucCycles.Definition => "CYCLE",
            _ => "RPM of the spindle " + SpindleNameOf(write, key.Substring(SpeedPrefix.Length)),
        };
    }

    // The role that names a spindle, else its resource id (language 4.10).
    private static string SpindleNameOf(FanucBlock write, string id)
    {
        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == id)
            {
                return role.Key;
            }
        }

        return id;
    }
}
