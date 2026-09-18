using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// What a JUMP or a REPEAT brings to its label (language 4.9; virtual machine 1, 3.6): the STATIC walk records the jump
/// and does not follow it, so the text after the label is written on the way the text runs, and the jump arrives with
/// what the control and the program have at the jump. State is modal, as on every controller (language 2 rule 2), and
/// REPEAT runs the blocks from the label again with the state it has (language 4.9; virtual machine 3.6), so on every
/// way the blocks after the label run with the compensation and the feed that way brings until a block states another.
/// Klartext writes both at the motion (controllers heidenhain.md 2; heidenhain 8 rule 2), and after a label the first L
/// block and the first feed motion write them only where a block from the label on states them, where the control has
/// the values of the program on the way the text runs; where it has not, they write the values of that way. Each way of
/// a jump is followed to them and reported where Klartext would run it with other values than the program (CMP115,
/// CMP118); the tool axis of a WORKPLANE after the label, which only a TOOL CALL gives, is checked at the jump
/// (CMP110).
/// </summary>
internal static class HeidenhainArrivals
{
    // What the target state keeps under the compensation and the feed after a label where the control has the values of
    // the program on the way the text runs: the mark with the step of the label, ~12.
    private const string Mark = "~";

    /// <summary>
    /// Records at a label what the control has on the way the text runs into it, the compensation and the feed of the
    /// program or not, marks them in the target state, and follows the ways of the jumps forward that wait for it.
    /// </summary>
    public static void EnterLabel(HeidenhainBlock writing)
    {
        bool compensation = HeidenhainCompensation.EnterLabel(writing);
        bool feed = HeidenhainMotion.EnterLabel(writing);
        writing.Labels.Record(writing.Step, new HeidenhainLabelWay(compensation, feed));
        foreach (HeidenhainArrival arrival in writing.Labels.TakeWaiting(writing.Step))
        {
            Follow(writing, arrival, writing.Step.Index);
        }
    }

    /// <summary>
    /// Checks what the jump of the block brings to the blocks after its label: the tool axis at the jump
    /// (HeidenhainToolCall.CheckArrival, CMP110), and the compensation and the feed where both the jump and the label
    /// are written, now for a jump back and at the label for a jump forward (CMP115, CMP118).
    /// </summary>
    /// <param name="writing">The block being written, with the JUMP or REPEAT.</param>
    /// <param name="jump">The JUMP or REPEAT word, whose value names the label.</param>
    public static void Check(HeidenhainBlock writing, Word jump)
    {
        if (LabelOf(writing, jump.Value) is not int label)
        {
            return;
        }

        HeidenhainToolCall.CheckArrival(writing, jump, label);

        // The jump is taken at the end of its block (language 4.9), after its motion.
        var arrival = new HeidenhainArrival
        {
            Jump = writing.Block,
            Word = jump,
            ControlCompensation = HeidenhainCompensation.Active(writing),
            ProgramCompensation = HeidenhainCompensation.WordOf(writing.After.Motion.Comp),
            FeedInStep = HeidenhainMotion.InStep(writing, writing.Target.ActiveOf(HeidenhainMotion.FeedKey),
                writing.Step.Index),
            FeedStep = writing.Step.Index,
        };
        Arrive(writing, arrival, label);
    }

    /// <summary>
    /// Tells whether the control runs nothing after the step: PROGRAM=END ends the program, and SUB=END of a walk that
    /// no CALL entered returns to nothing (virtual machine 3.9).
    /// </summary>
    public static bool EndsTheRun(BlockStep step)
    {
        return step.Block.Has("PROGRAM", null, "END")
            || (step.Block.Has("SUB", null, "END") && step.Before.Flow.Calls.Count == 0);
    }

    /// <summary>
    /// What the target state keeps after the label of the step where the control has the value of the program: ~12.
    /// </summary>
    public static string MarkOf(BlockStep label)
    {
        return Mark + label.Index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The step of the label a value of the target state marks; null for any other value.
    /// </summary>
    public static int? MarkedLabel(string? active)
    {
        if (active is null || !active.StartsWith(Mark, StringComparison.Ordinal))
        {
            return null;
        }

        return int.TryParse(active.AsSpan(Mark.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int step)
            ? step
            : null;
    }

    /// <summary>
    /// Tells whether a block from a label up to a step states the value under a key, COMP or F: its word, or a
    /// @RESTORE of it, which applies it again as if the program had written it (virtual machine 3.10). The steps
    /// between are those of the walk in its order, the walks of the subprograms called there included.
    /// </summary>
    public static bool States(HeidenhainBlock writing, int label, int to, string key)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = label; index <= to; index++)
        {
            if (StatesIn(steps[index].Block, key))
            {
                return true;
            }
        }

        return false;
    }

    // The way reaches its label now where the label is written, and waits for it where the label stands further on.
    private static void Arrive(HeidenhainBlock writing, HeidenhainArrival arrival, int label)
    {
        BlockStep step = writing.LookAhead.Steps[label];
        if (writing.Labels.WayOf(step) is null)
        {
            writing.Labels.Wait(step, arrival);
            return;
        }

        Follow(writing, arrival, label);
    }

    // Follows a way from its label to the first L block and the first feed motion, and reports what Klartext runs there
    // on this way with another compensation or feed than the program (language 2 rule 2, 4.4, 4.9). A block that states
    // COMP or F sets it on every way, and the motion after it writes it. A label further on takes the way over, which
    // it reaches as the text runs. A walk of a subprogram entered on the way is written from an unknown target state,
    // so its first L block and feed motion write the values of its walk (virtual machine 3.9, D99), and so does the
    // caller after a label in it that its walk wrote nothing before.
    private static void Follow(HeidenhainBlock writing, HeidenhainArrival arrival, int label)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        HeidenhainLabelWay way = writing.Labels.WayOf(steps[label])
            ?? throw new InvalidOperationException("A way is followed from a label not written yet.");
        string? control = arrival.ControlCompensation;
        string program = arrival.ProgramCompensation;
        bool? feedInStep = arrival.FeedInStep;
        int feedStep = arrival.FeedStep;
        bool compensationStated = false;
        bool feedStated = false;
        bool unknown = false;
        int walks = 0;
        for (int index = label; index < steps.Count && (control is not null || feedInStep is not null); index++)
        {
            BlockStep step = steps[index];
            Block block = step.Block;
            if (EndsTheRun(step))
            {
                return;
            }

            if (block.Has("SUB", null, "BEGIN"))
            {
                walks++;
            }

            if (index > label && block.Has("LABEL"))
            {
                if (walks == 0)
                {
                    Arrive(writing, arrival with
                    {
                        ControlCompensation = control,
                        ProgramCompensation = program,
                        FeedInStep = feedInStep is bool kept ? kept && !feedStated : null,
                        FeedStep = feedStep,
                    }, index);
                    return;
                }

                unknown = true;
            }

            if (StatesIn(block, HeidenhainCompensation.WrittenKey))
            {
                compensationStated = true;
                program = HeidenhainCompensation.WordOf(step.After.Motion.Comp);
            }

            if (StatesIn(block, HeidenhainMotion.FeedKey))
            {
                feedStated = true;
                feedStep = index;
            }

            bool unmarked = walks > 0 || unknown;
            if (control is not null && HeidenhainCompensation.IsLBlock(block))
            {
                string? written = compensationStated || unmarked || !way.CompensationInStep
                    ? HeidenhainCompensation.WordOf(step.After.Motion.Comp)
                    : null;
                ReportCompensation(writing, arrival, step, control, program, written, unmarked);
                control = null;
            }
            else if (control is not null && HeidenhainCompensation.RunsWithoutLBlock(block) && control != program)
            {
                writing.Diagnostics.Error(arrival.Jump, DiagnosticCodes.HeidenhainCompensationWithoutLine,
                    $"{arrival.Word.ToCanonical()} reaches LBL {HeidenhainFlow.Label(arrival.Word.Value)} with the "
                    + $"radius compensation {control} on the control, and the {block.Verb?.Key} on line {block.Line} "
                    + $"after it runs with {program} in the program: Klartext switches the compensation at the end of "
                    + "an L block only, and none stands before that motion on this way (language 2 rule 2, 4.4, 4.9; "
                    + "controllers heidenhain.md 2).");
                control = null;
            }

            if (feedInStep is bool feedKept && HeidenhainMotion.IsFeedMotion(block))
            {
                bool writes = feedStated || unmarked || !way.FeedInStep;
                bool fed = writes ? HeidenhainMotion.SameFeed(writing, feedStep, index) : feedKept;
                ReportFeed(writing, arrival, step, writes, fed, unmarked);
                feedInStep = null;
            }

            if (block.Has("SUB", null, "END") && walks > 0)
            {
                walks--;
            }
        }
    }

    // The first L block on the way runs with the compensation it writes, or with the one the control has where it
    // writes none, and the program runs it with its own (language 2 rule 2, 4.4; controllers heidenhain.md 2).
    private static void ReportCompensation(HeidenhainBlock writing, HeidenhainArrival arrival, BlockStep step,
        string control, string program, string? written, bool walk)
    {
        if ((written ?? control) == program)
        {
            return;
        }

        string reaches = $"{arrival.Word.ToCanonical()} reaches LBL {HeidenhainFlow.Label(arrival.Word.Value)}";
        string line = step.Block.Line.ToString(CultureInfo.InvariantCulture);
        string message = written is null
            ? $"{reaches} with the radius compensation {control} on the control and {program} in the program, a COMP "
                + $"that no L block before the jump has written, and the L block on line {line} after the label, where "
                + "no block from the label on states COMP, writes none"
            : $"{reaches} with the radius compensation {program} of the program, and the L block on line {line} after "
                + $"the label writes {written}, " + (walk
                    ? "as the subprogram entered on the way writes it for the way the text runs (virtual machine 3.9, "
                        + "D99)"
                    : "the compensation of the way the text runs into the label, since no block from the label on "
                        + "states COMP");
        writing.Diagnostics.Error(arrival.Jump, DiagnosticCodes.HeidenhainLabelWaysDiffer,
            message + ": Klartext has one text for both ways, and nothing written runs both as the program does "
            + "(language 2 rule 2, 4.4, 4.9; controllers heidenhain.md 2).");
    }

    // The first feed motion on the way runs with the F it writes, or with the feed the control has where it writes
    // none, and the program runs it with its own (language 2 rule 2, 4.9; heidenhain 8 rule 2).
    private static void ReportFeed(HeidenhainBlock writing, HeidenhainArrival arrival, BlockStep step, bool writes,
        bool fed, bool walk)
    {
        if (fed)
        {
            return;
        }

        string reaches = $"{arrival.Word.ToCanonical()} reaches LBL {HeidenhainFlow.Label(arrival.Word.Value)}";
        string motion = $"the {step.Block.Verb?.Key} on line {step.Block.Line.ToString(CultureInfo.InvariantCulture)}";
        string message = writes
            ? $"{reaches} with another feed of the program than the way the text runs into the label, and {motion} "
                + "after the label writes the feed of that way, " + (walk
                    ? "as the subprogram entered on the way writes it (virtual machine 3.9, D99)"
                    : "since no block from the label on states F")
            : $"{reaches} with another feed on the control than in the program, an F that no motion before the jump "
                + $"has written, and {motion} after the label, where no block from the label on states F, writes none";
        writing.Diagnostics.Error(arrival.Jump, DiagnosticCodes.HeidenhainLabelWaysDiffer,
            message + ": Klartext has one text for both ways, and nothing written runs both as the program does "
            + "(language 2 rule 2, 4.9; controllers heidenhain.md 8 rule 2).");
    }

    // A block states the value under a key: its word, or a @RESTORE of that state key (virtual machine 3.10).
    private static bool StatesIn(Block block, string key)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key == key || (word.Key == "@RESTORE" && word.Value.ToCanonical() == key))
            {
                return true;
            }
        }

        return false;
    }

    // The step of the LABEL a jump names, in the walk of the jump: a LABEL is unique per program or subprogram and a
    // JUMP goes to a label of the current section, forward or backward (language 4.9; virtual machine 3.6), so the
    // label stands in the steps of the same section at the same call depth between its start and its end; null where
    // it does not.
    private static int? LabelOf(HeidenhainBlock writing, Value label)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        BlockStep jump = writing.Step;
        string name = label.ToCanonical();
        for (int index = jump.Index; index >= 0; index--)
        {
            BlockStep step = steps[index];
            if (InWalkOf(step, jump) && step.Block.Find("LABEL")?.Value.ToCanonical() == name)
            {
                return index;
            }

            if (InWalkOf(step, jump) && Bounds(step.Block, "BEGIN"))
            {
                break;
            }
        }

        for (int index = jump.Index + 1; index < steps.Count; index++)
        {
            BlockStep step = steps[index];
            if (InWalkOf(step, jump) && step.Block.Find("LABEL")?.Value.ToCanonical() == name)
            {
                return index;
            }

            if (InWalkOf(step, jump) && Bounds(step.Block, "END"))
            {
                break;
            }
        }

        return null;
    }

    // PROGRAM=BEGIN and SUB=BEGIN start a section, PROGRAM=END and SUB=END end it (language 4.13).
    private static bool Bounds(Block block, string bound)
    {
        return block.Has("PROGRAM", null, bound) || block.Has("SUB", null, bound);
    }

    // A step of the walk of the jump: the same section at the same call depth; the steps of the subprograms it calls
    // stand deeper, and another walk of the same subprogram is cut off by its SUB=BEGIN or SUB=END (virtual machine
    // 3.9).
    private static bool InWalkOf(BlockStep step, BlockStep jump)
    {
        return step.Section == jump.Section && step.Before.Flow.Calls.Count == jump.Before.Flow.Calls.Count;
    }
}
