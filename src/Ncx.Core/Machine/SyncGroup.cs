namespace Ncx.Core.Machine;

/// <summary>
/// One entry of [sync] groups: a set of channels that wait with marks of their own range, { channels = [3, 4], range =
/// [800, 849] } (machine-config 5).
/// </summary>
public sealed record SyncGroup
{
    /// <summary>
    /// channels: the channels of the group, [3, 4].
    /// </summary>
    public IReadOnlyList<int> Channels { get; init; } = [];

    /// <summary>
    /// range: the marks of the group; null when left out.
    /// </summary>
    public MarkRange? Range { get; init; }
}
