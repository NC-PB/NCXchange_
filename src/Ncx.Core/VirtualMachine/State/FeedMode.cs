namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The feed mode of FEED_MODE (language 4.3, virtual machine 2.2).
/// </summary>
public enum FeedMode
{
    /// <summary>
    /// FEED_MODE=PER_MIN: feed per minute (G94), the start value.
    /// </summary>
    PerMin,

    /// <summary>
    /// FEED_MODE=PER_REV: feed per spindle revolution (G95).
    /// </summary>
    PerRev,
}
