namespace Ncx.Compilers;

/// <summary>
/// One release of a mark as a channel of a job passed it (virtual machine 3.7, 7: SYNC_RELEASE): the mark, the round of
/// the job scheduler in which the channels were released together, the channels that took part, and the SYNC block in
/// the file of the channel. The channels that took part pass the same release, which is how the job compiler finds
/// the same mark in the program of another channel (D56).
/// </summary>
internal sealed record MarkRelease
{
    /// <summary>
    /// The mark m of SYNC=m.
    /// </summary>
    public required int Mark { get; init; }

    /// <summary>
    /// The round of the scheduler that released the channels (D39).
    /// </summary>
    public required int Round { get; init; }

    /// <summary>
    /// The channels that took part, WITH or every channel of the job, in ascending order (language 4.8).
    /// </summary>
    public required IReadOnlyList<int> Channels { get; init; }

    /// <summary>
    /// The index of the SYNC block in the blocks of the expanded file of the channel, a SYNC a rewriter generated among
    /// them; null for a SYNC that stands on no block of that file.
    /// </summary>
    public int? Block { get; init; }

    /// <summary>
    /// True when the SYNC block stands in a subprogram, whose blocks run at every CALL (virtual machine 3.9).
    /// </summary>
    public bool InSubprogram { get; init; }

    /// <summary>
    /// Tells whether another channel passed this same release: the same mark, round and channels.
    /// </summary>
    public bool IsSameRelease(MarkRelease other)
    {
        return Mark == other.Mark && Round == other.Round && Channels.SequenceEqual(other.Channels);
    }
}
