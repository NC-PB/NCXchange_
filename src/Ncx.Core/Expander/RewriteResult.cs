namespace Ncx.Core.Expander;

/// <summary>
/// The answer of a program rewriter for one block (architecture 9, code-guidelines 11, D106): Unchanged, Replace with
/// the new text of the block, or Surround with the texts of generated blocks before and after it. Every text is one NCX
/// block, which the expander parses with the option of generated text, so that it may carry @SAVE and @RESTORE
/// (code-guidelines 10.3, D95); the reason is what trace shows with the name of the rewriter (virtual machine 3.10).
/// </summary>
public sealed record RewriteResult
{
    private RewriteResult(RewriteKind kind, string? replacement, IReadOnlyList<string> before,
        IReadOnlyList<string> after, string reason)
    {
        Kind = kind;
        Replacement = replacement;
        Before = before;
        After = after;
        Reason = reason;
    }

    /// <summary>
    /// The block stays as it is.
    /// </summary>
    public static RewriteResult Unchanged { get; } = new(RewriteKind.Unchanged, null, [], [], "");

    /// <summary>
    /// Unchanged, Replace or Surround.
    /// </summary>
    public RewriteKind Kind { get; }

    /// <summary>
    /// For Replace, the NCX text of the block that stands in place of the block; null otherwise.
    /// </summary>
    public string? Replacement { get; }

    /// <summary>
    /// For Surround, the NCX texts of the blocks inserted before the block, one block each, in their order.
    /// </summary>
    public IReadOnlyList<string> Before { get; }

    /// <summary>
    /// For Surround, the NCX texts of the blocks inserted after the block, one block each, in their order.
    /// </summary>
    public IReadOnlyList<string> After { get; }

    /// <summary>
    /// Why the rewriter changed the program: "the coolant clutch needs a standing spindle".
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Rewrites the words of the block: this NCX text stands in its place, "RPM:MAIN=1200".
    /// </summary>
    /// <param name="block">The NCX text of the new block.</param>
    /// <param name="reason">Why, as trace shows it.</param>
    public static RewriteResult Replace(string block, string reason)
    {
        return new RewriteResult(RewriteKind.Replace, block, [], [], reason);
    }

    /// <summary>
    /// Inserts generated blocks before and after the block, which stays: before: ["@SAVE=SPINDLE:MAIN",
    /// "SPINDLE:MAIN=OFF"], after: ["@RESTORE=SPINDLE:MAIN"].
    /// </summary>
    /// <param name="before">The NCX texts of the blocks before it, one block each.</param>
    /// <param name="after">The NCX texts of the blocks after it, one block each.</param>
    /// <param name="reason">Why, as trace shows it.</param>
    public static RewriteResult Surround(IReadOnlyList<string> before, IReadOnlyList<string> after, string reason)
    {
        return new RewriteResult(RewriteKind.Surround, null, before, after, reason);
    }
}
