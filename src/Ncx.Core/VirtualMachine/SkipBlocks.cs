using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The run option skip_blocks: which SKIP blocks the virtual machine skips. SKIP blocks are executed unless the option
/// says otherwise: it skips all of them, or those of the numbered block skip switches that are on (language 4.1,
/// virtual machine 2.2, 3.6, D53).
/// </summary>
public sealed record SkipBlocks
{
    /// <summary>
    /// skip_blocks none, the default: every SKIP block is executed (D53).
    /// </summary>
    public static SkipBlocks None { get; } = new();

    /// <summary>
    /// skip_blocks all: every SKIP block is skipped.
    /// </summary>
    public static SkipBlocks Every { get; } = new() { AllSwitches = true };

    /// <summary>
    /// True when every SKIP block is skipped, whatever its switch.
    /// </summary>
    public bool AllSwitches { get; init; }

    /// <summary>
    /// The numbered block skip switches that are on, skip_blocks = [1, 3]: a SKIP=n block with n among them is skipped.
    /// </summary>
    public IReadOnlyList<int> Switches { get; init; } = [];

    /// <summary>
    /// The block skip switches that are on, skip_blocks = [1, 3] (virtual machine 3.6).
    /// </summary>
    /// <param name="switches">The switch numbers, 1 to 9 (language 4.1).</param>
    public static SkipBlocks OnSwitches(IReadOnlyList<int> switches)
    {
        return new SkipBlocks { Switches = switches };
    }

    /// <summary>
    /// Tells whether the virtual machine skips a block under this option (D53).
    /// </summary>
    /// <param name="block">The block, with or without SKIP.</param>
    public bool Skips(Block block)
    {
        // A block without SKIP always runs; a SKIP block runs unless the option skips it (D53).
        if (!block.Skip)
        {
            return false;
        }

        if (AllSwitches)
        {
            return true;
        }

        // SKIP=n is skipped when switch n is on (virtual machine 3.6, skip_blocks = [1, 3]).
        // TODO(question): language 4.1 writes a bare SKIP for the unnumbered switch (Fanuc and Siemens /, Heidenhain /)
        // and SKIP=n for switch n, and virtual machine 3.6 names the list form "for numbered switches"; neither says
        // whether a switch list skips a bare SKIP (on Fanuc / and /1 are the same switch). A switch list skips the
        // numbered blocks only until D133 is answered; skip_blocks all skips the bare ones.
        return block.SkipNumber is int number && Switches.Contains(number);
    }
}
