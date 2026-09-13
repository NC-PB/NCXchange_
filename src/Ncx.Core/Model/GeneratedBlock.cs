namespace Ncx.Core.Model;

/// <summary>
/// The origin a generated block carries (virtual machine 3.10, language 4.15): the block of the file it was generated
/// for, the expansion rule or the rewriter that made it and why, so that a diagnostic, trace and annotate can name
/// them (D98).
/// </summary>
public sealed record GeneratedBlock
{
    /// <summary>
    /// The block of the file the block was generated for, the block that triggered the rule; its line is the
    /// OriginLine of the generated block (language 4.15, D98).
    /// </summary>
    public required Block Origin { get; init; }

    /// <summary>
    /// The rule or the rewriter that made the block: "[coolant] THROUGH", "[tool_change]", "cycle PECK",
    /// "[machine] limits", or the name of a rewriter, "CoolantClutchRule" (machine-config 5a, code-guidelines 11).
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Why: the key of the rule the text comes from, "restore", "requires", "pre", "post"; the reason a rewriter gives;
    /// or the limit a word was clamped to (machine-config 5a, D64).
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Before, in place of or after its origin.
    /// </summary>
    public required GeneratedPlacement Placement { get; init; }
}
