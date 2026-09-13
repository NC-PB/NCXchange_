namespace Ncx.Core.Machine;

/// <summary>
/// [sync]: how the channels of a machine wait for each other: the wait template, the path form, the marks, and the
/// Siemens channel commands (machine-config 5, language 4.8).
/// </summary>
public sealed record SyncConfig
{
    /// <summary>
    /// wait: the wait template, "M{mark}", "M{mark} P{paths}", "WAITM({mark},{channels})"; null when left out.
    /// </summary>
    public string? Wait { get; init; }

    /// <summary>
    /// paths: how {paths} is written, as a list (P12) or a bitmask (P3); null when left out.
    /// </summary>
    public SyncPaths? Paths { get; init; }

    /// <summary>
    /// mark_range: the marks the compiler may use, [100, 199]; null when left out.
    /// </summary>
    public MarkRange? MarkRange { get; init; }

    /// <summary>
    /// start_mark: the mark written as the first block of every channel program, 199; null when left out.
    /// </summary>
    public int? StartMark { get; init; }

    /// <summary>
    /// groups: further wait groups between other channels with their own marks (Nakamura WTW); empty when left out.
    /// </summary>
    public IReadOnlyList<SyncGroup> Groups { get; init; } = [];

    /// <summary>
    /// start_channel: the Siemens template that starts a channel, "START({channel})"; null when left out.
    /// </summary>
    public string? StartChannel { get; init; }

    /// <summary>
    /// wait_channel: the Siemens template that waits for a channel to end, "WAITE({channel})"; null when left out.
    /// </summary>
    public string? WaitChannel { get; init; }
}
