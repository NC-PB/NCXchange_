namespace Ncx.Core.VirtualMachine;

/// <summary>
/// Where the flow goes after a block (architecture 5.1, the last box: advance pc or follow flow): to the next block,
/// or into the subprogram of a CALL, which STATIC mode follows once the block's events are raised (virtual machine 1,
/// D99).
/// </summary>
/// <param name="Executed">False for a SKIP block that the run option skips (D53).</param>
/// <param name="FollowsCall">True for a block whose CALL the walk follows.</param>
internal sealed record BlockFlow(bool Executed, bool FollowsCall)
{
    /// <summary>
    /// A block the run option skips: nothing changed (D53).
    /// </summary>
    public static BlockFlow Skipped { get; } = new(Executed: false, FollowsCall: false);

    /// <summary>
    /// An executed block after which the flow goes on with the next block.
    /// </summary>
    public static BlockFlow Next { get; } = new(Executed: true, FollowsCall: false);

    /// <summary>
    /// An executed block whose CALL the walk follows.
    /// </summary>
    public static BlockFlow Call { get; } = new(Executed: true, FollowsCall: true);
}
