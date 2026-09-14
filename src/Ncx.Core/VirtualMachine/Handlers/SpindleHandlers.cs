using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The spindle words of language 4.5 and the lathe words CSS, VC and RPM_MAX of 4.11, per spindle resource: the rows of
/// virtual machine 2.4 on the spindle step 2 resolved (3.8 rules 1, 2 and 5). The rules 4 and 5 that depend on the mode
/// and the synchronization are checked after all state words of the block (ResourceResolver.CheckSpindleRules).
/// </summary>
internal static class SpindleHandlers
{
    /// <summary>
    /// Registers the spindle words; PHASE belongs to the SPINDLE_SYNC of its block (language 4.5).
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["SPINDLE"] = ApplySpindle;
        handlers["RPM"] = ApplyRpm;
        handlers["SPINDLE_MODE"] = ApplySpindleMode;
        handlers["ORIENT"] = ApplyOrient;
        handlers["SPINDLE_SYNC"] = ApplySpindleSync;
        handlers["CSS"] = ApplyCss;
        handlers["VC"] = ApplyVc;
        handlers["RPM_MAX"] = ApplyRpmMax;
    }

    // SPINDLE=CW, CCW or OFF (language 4.5, virtual machine 2.4).
    private static void ApplySpindle(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is not SpindleState spindle)
        {
            return;
        }

        spindle.Direction = BlockContext.IdentOf(word) switch
        {
            "CW" => SpindleDirection.Clockwise,
            "CCW" => SpindleDirection.Counterclockwise,
            _ => SpindleDirection.Off,
        };
    }

    // RPM=n, per resource (language 4.5, virtual machine 2.4); UNKNOWN from an expression in STATIC mode (1).
    private static void ApplyRpm(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is not SpindleState spindle)
        {
            return;
        }

        if (context.TryNumber(word, StateKeyOf(word, context), out decimal rpm))
        {
            spindle.Rpm = rpm;
        }
    }

    // SPINDLE_MODE:r=SPINDLE or AXIS: the work spindle as rotating spindle or as positioning C axis (language 4.5,
    // virtual machine 2.4, 3.8 rule 4).
    private static void ApplySpindleMode(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is SpindleState spindle)
        {
            spindle.Mode = BlockContext.IdentOf(word) == "AXIS" ? SpindleMode.Axis : SpindleMode.Spindle;
        }
    }

    // ORIENT=deg: oriented spindle stop; the spindle is stopped afterwards (language 4.5, virtual machine 2.4).
    private static void ApplyOrient(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is not SpindleState spindle)
        {
            return;
        }

        if (context.TryNumber(word, StateKeyOf(word, context), out decimal angle))
        {
            spindle.Orientation = angle;
        }

        spindle.Direction = SpindleDirection.Off;
    }

    // SPINDLE_SYNC=a,b: synchronous spindles, the second follows the first, with the angular offset of PHASE in the
    // same block for a phase-synchronous run, speed only without it; SPINDLE_SYNC=OFF ends it (language 4.5, virtual
    // machine 2.4, 3.8 rule 5).
    private static void ApplySpindleSync(Word word, BlockContext context)
    {
        // TODO(question): virtual machine 2.4 gives every spindle a syncPartner and syncPhase without saying on which
        // of the two they stand; they stand on the following spindle, naming the leading one, so that rule 5 knows
        // which spindle b is, until D171 is answered.
        foreach (KeyValuePair<string, SpindleState> spindle in context.State.Spindles)
        {
            spindle.Value.SyncPartner = null;
            spindle.Value.SyncPhase = null;
            context.State.Unknown.Remove(BlockContext.StateKey("PHASE", spindle.Key));
        }

        if (BlockContext.IdentOf(word) == "OFF" || context.SyncSpindles.Count != 2)
        {
            return;
        }

        string leading = context.SyncSpindles[0];
        string following = context.SyncSpindles[1];
        SpindleState follower = context.State.Spindles[following];
        follower.SyncPartner = leading;
        if (context.Block.Find("PHASE") is Word phase)
        {
            string phaseKey = BlockContext.StateKey("PHASE", following);
            follower.SyncPhase = context.TryNumber(phase, phaseKey, out decimal offset) ? offset : 0m;
        }
    }

    // CSS=ON or OFF: constant surface speed; under it the spindle follows VC (language 4.11, virtual machine 2.4).
    private static void ApplyCss(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is SpindleState spindle)
        {
            spindle.Css = BlockContext.IdentOf(word) == "ON";
        }
    }

    // VC=n: the cutting speed for CSS (language 4.11, virtual machine 2.4).
    private static void ApplyVc(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is SpindleState spindle
            && context.TryNumber(word, StateKeyOf(word, context), out decimal speed))
        {
            spindle.Vc = speed;
        }
    }

    // RPM_MAX=n: the speed limit under CSS (language 4.11, virtual machine 2.4).
    private static void ApplyRpmMax(Word word, BlockContext context)
    {
        if (SpindleOf(word, context) is SpindleState spindle
            && context.TryNumber(word, StateKeyOf(word, context), out decimal limit))
        {
            spindle.RpmMax = limit;
        }
    }

    // The spindle step 2 resolved for the word; null when it could not be resolved.
    private static SpindleState? SpindleOf(Word word, BlockContext context)
    {
        return context.ResourceOf.TryGetValue(word, out string? spindleId) ? context.State.Spindles[spindleId] : null;
    }

    // RPM:S1, the state key of the variable a word sets on its spindle.
    private static string StateKeyOf(Word word, BlockContext context)
    {
        return BlockContext.StateKey(word.Key, context.ResourceOf[word]);
    }
}
