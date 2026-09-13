namespace Ncx.Core.Machine;

/// <summary>
/// The four optional keys of a function state, the tool change or a catalog cycle, from which the expander makes
/// generated NCX blocks around the triggering block (machine-config 5a, virtual machine 3.10).
/// </summary>
public sealed record ExpansionRule
{
    /// <summary>
    /// pre: NCX blocks inserted before the block, "HOME Z", "RAPID {position:tool_change} FRAME=MACHINE" (D100).
    /// </summary>
    public IReadOnlyList<string> Pre { get; init; } = [];

    /// <summary>
    /// post: NCX blocks inserted after the block.
    /// </summary>
    public IReadOnlyList<string> Post { get; init; } = [];

    /// <summary>
    /// requires: state conditions that must hold while the function is written, SPINDLE = "OFF".
    /// </summary>
    public IReadOnlyDictionary<string, string> Requires { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// restore: the state variables put back after the block through @RESTORE, "SPINDLE".
    /// </summary>
    public IReadOnlyList<string> Restore { get; init; } = [];
}
