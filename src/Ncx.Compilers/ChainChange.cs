using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// What one block did to the frame chain (language 4.2, D31): the entries it removed and the entries it appended.
/// </summary>
public sealed record ChainChange
{
    /// <summary>
    /// The entries the block removed, the last entry first: the chain is unwound from the end only (language 4.2), by
    /// a RESET that removes its entry and everything after it, or by ORIGIN, which starts an empty chain.
    /// </summary>
    public required IReadOnlyList<TransformEntry> Removed { get; init; }

    /// <summary>
    /// The entries the block appended, in program order (D31).
    /// </summary>
    public required IReadOnlyList<TransformEntry> Appended { get; init; }

    /// <summary>
    /// True when the block left the chain as it was.
    /// </summary>
    public bool IsEmpty => Removed.Count == 0 && Appended.Count == 0;
}
