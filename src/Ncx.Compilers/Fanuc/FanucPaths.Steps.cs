using System.Diagnostics.CodeAnalysis;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

// The look-ahead of the paths (architecture 8): what the steps of the STATIC walk tell of the paths to a label before
// the compiler has written them, since a label is written before a jump back to it and after a jump forward to it.
internal static partial class FanucPaths
{
    // How a block states a modal value itself: not at all, with a number, which the control holds on every path from
    // there on, or with an expression, whose value the control holds and the compiler cannot name (virtual machine 1).
    private enum Statement
    {
        None,
        Number,
        Expression,
    }

    // True where the value of NCX under a key differs by the path on which the control reaches a label (virtual
    // machine 1): the values of the walk differ over the block before the label and the jumps to it, or the value of
    // one of them depends on the path that led there, through a skipped block (D53), an expression, or another label
    // where it differs.
    private static bool DiffersAt(FanucBlock write, string label, string key)
    {
        bool cut = false;
        bool differs = DiffersAt(write, label, key, new HashSet<string>(StringComparer.Ordinal), ref cut);
        write.Labels.DiffersByPath[(label, key)] = differs;
        return differs;
    }

    // A label already being looked at adds nothing of its own on a path that leads back to it, so what is found for a
    // label on such a path is known only once that label is, unless it differs anyway.
    private static bool DiffersAt(FanucBlock write, string label, string key, HashSet<string> visiting, ref bool cut)
    {
        if (write.Labels.DiffersByPath.TryGetValue((label, key), out bool known))
        {
            return known;
        }

        if (!visiting.Add(label))
        {
            cut = true;
            return false;
        }

        bool ownCut = false;
        bool differs = ArrivalsDiffer(write, label, key, visiting, ref ownCut);
        visiting.Remove(label);
        if (differs || !ownCut)
        {
            write.Labels.DiffersByPath[(label, key)] = differs;
        }

        cut |= ownCut && !differs;
        return differs;
    }

    // The block before the label and every jump to it, in every walk of the section (virtual machine 1, 3.9).
    private static bool ArrivalsDiffer(FanucBlock write, string label, string key, HashSet<string> visiting,
        ref bool cut)
    {
        var states = new List<ChannelSnapshot>();
        var arrivals = new List<(BlockStep Step, bool WithIt)>();
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section != write.Step.Section)
            {
                continue;
            }

            if (step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                states.Add(step.Before);
                arrivals.Add((step, false));
            }

            if (step.Block.Find("JUMP")?.Value.ToCanonical() == label)
            {
                states.Add(step.After);
                arrivals.Add((step, true));
            }
        }

        if (!Agree(write, key, states))
        {
            return true;
        }

        foreach ((BlockStep step, bool withIt) in arrivals)
        {
            if (DependsOnThePathAt(write, step, withIt, key, visiting, ref cut))
            {
                return true;
            }
        }

        return false;
    }

    // True where the value of NCX under a key depends on the path the control came along, before a step or after it
    // (with it): back to the block that last stated it with a number, a skipped block after which it differs by the
    // switch (controller-mapping 1, SKIP; D53), a statement by an expression (virtual machine 1), or a label that a
    // jump reaches where it differs, make it so; the start of the program or walk does not.
    private static bool DependsOnThePathAt(FanucBlock write, BlockStep at, bool withIt, string key,
        HashSet<string> visiting, ref bool cut)
    {
        BlockStep later = at;
        for (int index = withIt ? at.Index : at.Index - 1; index >= 0; index--)
        {
            BlockStep step = write.Steps[index];
            Block block = step.Block;
            if (step.Section != at.Section)
            {
                continue;
            }

            if (block.Has("PROGRAM", null, "BEGIN") || block.Has("SUB", null, "BEGIN"))
            {
                return false;
            }

            // A jump in a skipped block jumps only where the block runs, so its own skip does not count for it.
            if (block.Skip && step.Index != at.Index)
            {
                if (!Agree(write, key, [step.Before, later.Before]))
                {
                    return true;
                }

                later = step;
                continue;
            }

            Statement statement = StatementOf(write, block, key);
            if (statement != Statement.None)
            {
                return statement == Statement.Expression;
            }

            // The label meets the paths before the block's own words (FanucFlow.WriteLabel).
            if (block.Find("LABEL")?.Value.ToCanonical() is string name && name != write.Labels.LoopLabel
                && write.Labels.IsJumpTarget(name))
            {
                return DiffersAt(write, name, key, visiting, ref cut);
            }

            later = step;
        }

        return false;
    }

    // The first block from the label to the jump back to it that takes the value under a key from the state before it
    // without stating it (FanucBlock.NeedsValueOfTheWalk); none where a block states it first. A skipped block that
    // states it states it on one path only (controller-mapping 1, SKIP).
    private static BlockStep? TakenAfterLabel(FanucBlock write, string key, BlockStep labelStep)
    {
        for (int index = labelStep.Index; index <= write.Step.Index; index++)
        {
            BlockStep step = write.Steps[index];
            if (step.Section != labelStep.Section)
            {
                continue;
            }

            if (Takes(write, step, key))
            {
                return step;
            }

            if (!step.Block.Skip && StatementOf(write, step.Block, key) != Statement.None)
            {
                return null;
            }
        }

        return null;
    }

    // The LABEL block a jump goes to in the walk of the jump: the last one before it or at it, where the jump goes
    // back, else the first one after it, up to the start and the end of the program or walk.
    private static BlockStep? LabelStepOf(FanucBlock write, string label)
    {
        BlockStep jump = write.Step;
        for (int index = jump.Index; index >= 0; index--)
        {
            BlockStep step = write.Steps[index];
            if (step.Section != jump.Section)
            {
                continue;
            }

            if (step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                return step;
            }

            if (step.Block.Has("PROGRAM", null, "BEGIN") || step.Block.Has("SUB", null, "BEGIN"))
            {
                break;
            }
        }

        for (int index = jump.Index + 1; index < write.Steps.Count; index++)
        {
            BlockStep step = write.Steps[index];
            if (step.Section != jump.Section)
            {
                continue;
            }

            if (step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                return step;
            }

            if (step.Block.Has("PROGRAM", null, "END") || step.Block.Has("SUB", null, "END"))
            {
                break;
            }
        }

        return null;
    }

    // How a block states the value under a key itself: F, FEED_MODE, WORKPLANE, OFFSET:RAD (language 4.3, 4.4), RPM
    // and CSS of the spindle (4.5, 4.11), CYCLE (4.7).
    private static Statement StatementOf(FanucBlock write, Block block, string key)
    {
        Word? word = key switch
        {
            Feed => block.Find("F"),
            FanucCodes.FeedMode => block.Find("FEED_MODE"),
            FanucCodes.Plane => block.Find("WORKPLANE"),
            Radius => block.Find("OFFSET", "RAD"),
            FanucCycles.Definition => block.Find("CYCLE"),
            _ when key.StartsWith(CssPrefix, StringComparison.Ordinal)
                => SpindleWord(write, block, "CSS", key.Substring(CssPrefix.Length)),
            _ => SpindleWord(write, block, "RPM", key.Substring(SpeedPrefix.Length)),
        };
        return word is null ? Statement.None
            : word.Value is ExprValue ? Statement.Expression
            : Statement.Number;
    }

    // True where a block takes the value under a key from the state before it without stating it, where the compiler
    // writes the value of the walk or asks the control for it (FanucBlock.NeedsValueOfTheWalk): LINE and ARC take F and
    // the feed mode (language 4.3), every motion and a cycle call the plane (4.2, 4.7), a block under G41 or G42 the D
    // register (4.4), a start and G97 of CSS=OFF the speed (4.5, 4.11), a start, an S and a G96 S of VC whether S is
    // the speed or the cutting speed (controllers fanuc.md 4), a CYCLE_CALL the cycle (4.7).
    private static bool Takes(FanucBlock write, BlockStep step, string key)
    {
        Block block = step.Block;
        string? verb = block.Verb?.Key;
        if (key.StartsWith(CssPrefix, StringComparison.Ordinal))
        {
            return TakesCss(write, step, key.Substring(CssPrefix.Length));
        }

        return key switch
        {
            Feed => verb is "LINE" or "ARC" && !block.Has("F"),
            FanucCodes.FeedMode => verb is "LINE" or "ARC" && !block.Has("FEED_MODE"),
            FanucCodes.Plane => verb is "RAPID" or "LINE" or "ARC" or "CYCLE_CALL" && !block.Has("WORKPLANE"),
            Radius => step.After.Motion.Comp != Compensation.Off && block.Find("OFFSET", "RAD") is null
                && step.After.LastHolder is string holder && step.After.Holders.TryGetValue(holder,
                    out HolderSnapshot? offsets) && offsets.OffsetRad != 0,
            FanucCycles.Definition => verb == "CYCLE_CALL" && !block.Has("CYCLE"),
            _ => TakesSpeed(write, step, key.Substring(SpeedPrefix.Length)),
        };
    }

    // A start of a running spindle and G97 of CSS=OFF without an RPM of the spindle take its speed (language 4.5,
    // 4.11).
    private static bool TakesSpeed(FanucBlock write, BlockStep step, string id)
    {
        Block block = step.Block;
        if (SpindleWord(write, block, "RPM", id) is not null || !Runs(step, id, out SpindleSnapshot? spindle)
            || spindle.Css)
        {
            return false;
        }

        return SpindleWord(write, block, "SPINDLE", id) is not null
            || SpindleWord(write, block, "CSS", id)?.Value.ToCanonical() == "OFF";
    }

    // A start, an S of the speed and the G96 S of VC take whether the control has G96 or G97 for the spindle
    // (controllers fanuc.md 4; language 4.11).
    private static bool TakesCss(FanucBlock write, BlockStep step, string id)
    {
        Block block = step.Block;
        if (SpindleWord(write, block, "CSS", id) is not null)
        {
            return false;
        }

        if (SpindleWord(write, block, "VC", id) is not null)
        {
            return true;
        }

        return Runs(step, id, out SpindleSnapshot? spindle)
            && (SpindleWord(write, block, "SPINDLE", id) is not null
                || (!spindle.Css && SpindleWord(write, block, "RPM", id) is not null));
    }

    // True where the spindle runs after the step.
    private static bool Runs(BlockStep step, string id, [NotNullWhen(true)] out SpindleSnapshot? spindle)
    {
        return step.After.Spindles.TryGetValue(id, out spindle) && spindle.Direction != SpindleDirection.Off;
    }

    // The word of a key whose address names the spindle, by its role, or without an address for the default spindle
    // (virtual machine 3.8 rule 2).
    private static Word? SpindleWord(FanucBlock write, Block block, string wordKey, string id)
    {
        foreach (Word word in block.Words)
        {
            string? spindle = word.Addr is null
                ? write.Machine.ResolveDefaultSpindle()?.Id
                : write.Machine.ResolveRole(word.Addr)?.Id;
            if (word.Key == wordKey && spindle == id)
            {
                return word;
            }
        }

        return null;
    }
}
