namespace Ncx.Compilers;

/// <summary>
/// What the compile of one channel program knows of its job (implementation 16, P6-02): the channel it writes and the
/// channels of the job. The job compiler gives it to the compiler of the family with the channel program, which it has
/// expanded and whose channel-bound words it has placed; a compile of one file has none.
/// </summary>
public sealed record JobView
{
    /// <summary>
    /// The channel the program runs on, the id of its [[channel]] in the job manifest (machine-config 8).
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The channels of the job in ascending order, which a SYNC without WITH waits for (language 4.8).
    /// </summary>
    public required IReadOnlyList<int> Channels { get; init; }
}
