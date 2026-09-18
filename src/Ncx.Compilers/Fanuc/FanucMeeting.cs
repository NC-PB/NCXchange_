namespace Ncx.Compilers.Fanuc;

/// <summary>
/// What the Fanuc compiler knows of the paths on which the control reaches one LABEL that a jump reaches: from the
/// block before it and from every JUMP to it (virtual machine 1). A jump written before the label tells it what it
/// brings; the label tells a jump written after it what the blocks after the label take from the control
/// (FanucPaths).
/// </summary>
internal sealed class FanucMeeting
{
    /// <summary>
    /// The modal values whose value of NCX differs by the path to the label, each true where the label leaves it to
    /// the control, which holds the value of each path (FanucBlock.Hold), and false where the control does not
    /// (FanucBlock.Lose); set when the label is written.
    /// </summary>
    public Dictionary<string, bool> Decided { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The modal values that a jump written before the label brings with a value that differs by the path to that jump.
    /// </summary>
    public HashSet<string> DependsByJump { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The modal values that a jump written before the label brings while the control does not hold the value of NCX.
    /// </summary>
    public HashSet<string> NotHeldByJump { get; } = new(StringComparer.Ordinal);
}
