namespace ThroughCoolantSourceRule;

/// <summary>
/// Reads the three blocks M5, M51, M3 S1500 back as the one word COOLANT:THROUGH=ON. They
/// are what a machine whose coolant clutch engages only while the spindle stands writes for
/// that word, and a program read back without this rule would stop and start its spindle
/// twice when it is compiled again (D66, machine-config 5a).
/// </summary>
public sealed class ThroughCoolantSourceRule : ISourceRule
{
    // The three M codes of the sequence. Change them to match your machine file.
    private const decimal SpindleStop = 5m;
    private const decimal ThroughCoolantOn = 51m;
    private const decimal SpindleStart = 3m;

    // TODO(question): D231, D232. The reader offers a rule only the blocks its tables leave
    // undecided, and the tables name M5, M51 and M3 (D231); a claim covers the one block
    // offered, and the rule sees no block after it (D232). So Read cannot fold the sequence
    // yet: it hands Fold no following blocks, and Fold folds nothing without them. Once a
    // rule is offered every block and sees the blocks after it, as D231 and D232 recommend,
    // Read hands Fold those blocks and claims the three it folded.
    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        return Fold(block, [], builder) > 0;
    }

    /// <summary>
    /// Folds the sequence that starts at a block: a block with M5, the next with M51, the one
    /// after with M3 and its speed, each with nothing else but a block number.
    /// </summary>
    /// <param name="block">The block the sequence starts with, M5.</param>
    /// <param name="following">The blocks after it, in their order.</param>
    /// <param name="builder">Where the NCX block goes.</param>
    /// <returns>How many source blocks were folded: 3, or 0 when they are no such sequence.</returns>
    public static int Fold(SourceBlock block, IReadOnlyList<SourceBlock> following, NcxBuilder builder)
    {
        if (following.Count < 2
            || !HoldsOnly(block, SpindleStop, speedAllowed: false)
            || !HoldsOnly(following[0], ThroughCoolantOn, speedAllowed: false)
            || !HoldsOnly(following[1], SpindleStart, speedAllowed: true))
        {
            return 0;
        }

        // One word for the three blocks. The spindle needs no word of its own: the machine
        // file or the plugin that expands COOLANT:THROUGH=ON again restores the direction and
        // the speed the spindle had before the stop (virtual machine 3.10).
        builder.Begin(block.Line).Word("COOLANT", "THROUGH", new IdentValue("ON")).End();
        return 3;
    }

    // A block that holds this M code and nothing else but its block number N, and the speed
    // S where it may. A block with more, a move among it, is no part of the sequence, since
    // folding it would lose the rest.
    private static bool HoldsOnly(SourceBlock block, decimal mCode, bool speedAllowed)
    {
        if (block.BlockSkip)
        {
            return false;
        }

        bool holdsCode = false;
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && word.Number == mCode)
            {
                holdsCode = true;
            }
            else if (word.Address != "N" && !(speedAllowed && word.Address == "S"))
            {
                return false;
            }
        }

        return holdsCode;
    }
}
