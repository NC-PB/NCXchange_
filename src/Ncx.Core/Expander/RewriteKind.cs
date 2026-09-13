namespace Ncx.Core.Expander;

/// <summary>
/// The three answers of a program rewriter (architecture 9, code-guidelines 11).
/// </summary>
public enum RewriteKind
{
    /// <summary>
    /// The block stays as it is.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The words of the block are rewritten: one block of NCX text stands in its place.
    /// </summary>
    Replace,

    /// <summary>
    /// Generated blocks of NCX text stand before and after the block.
    /// </summary>
    Surround,
}
