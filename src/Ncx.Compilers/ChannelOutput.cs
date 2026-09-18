using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The output file of one channel of a job, as the compiler of the family named it, before the job compiler names it
/// after the channel (implementation 16, P6-02).
/// </summary>
internal sealed record ChannelOutput
{
    /// <summary>
    /// The channel, the id of its [[channel]] (machine-config 8).
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The program of the channel, with its NAME and NUMBER (language 4.1).
    /// </summary>
    public required Section Program { get; init; }

    /// <summary>
    /// The file as the compiler of the family wrote it.
    /// </summary>
    public required CompiledFile File { get; init; }
}
