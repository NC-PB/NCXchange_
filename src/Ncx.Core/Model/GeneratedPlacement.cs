namespace Ncx.Core.Model;

/// <summary>
/// Where a generated block stands against the block of the file it was generated for (language 4.15, architecture
/// 5.5).
/// </summary>
public enum GeneratedPlacement
{
    /// <summary>
    /// Before the block: the @SAVE of a restore list, the words that establish a requires, a pre block, a block a
    /// rewriter inserts before it.
    /// </summary>
    Before,

    /// <summary>
    /// In place of the block: its words as a rewriter or the limits = "clamp" policy rewrote them (D64). ncx format
    /// writes the block of the file it stands for.
    /// </summary>
    InPlace,

    /// <summary>
    /// After the block: a post block, the @RESTORE of a restore list, a block a rewriter inserts after it.
    /// </summary>
    After,
}
