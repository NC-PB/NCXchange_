using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Handlers;

/// <summary>
/// The state words of the motion table of language 4.3, feed and feed mode: rows of virtual machine 2.2. The motion
/// verbs and their axis words are step 5 (P1-03).
/// </summary>
internal static class MotionHandlers
{
    /// <summary>
    /// Registers the state words of the motion table.
    /// </summary>
    public static void Register(Dictionary<string, Action<Word, BlockContext>> handlers)
    {
        handlers["F"] = ApplyFeed;
        handlers["FEED_MODE"] = ApplyFeedMode;
    }

    // F=value: the feed in the active feed mode (language 4.3, virtual machine 2.2). A feed from an expression is
    // UNKNOWN in STATIC mode (virtual machine 1): a feed is set, its value is not known.
    private static void ApplyFeed(Word word, BlockContext context)
    {
        MotionState motion = context.State.Motion;
        motion.Feed = context.TryNumber(word, "F", out decimal feed) ? feed : motion.Feed ?? 0m;
    }

    // FEED_MODE=PER_MIN or PER_REV (language 4.3, virtual machine 2.2).
    private static void ApplyFeedMode(Word word, BlockContext context)
    {
        context.State.Motion.FeedMode = BlockContext.IdentOf(word) == "PER_REV" ? FeedMode.PerRev : FeedMode.PerMin;
    }
}
