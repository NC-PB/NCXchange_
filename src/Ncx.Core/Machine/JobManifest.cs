namespace Ncx.Core.Machine;

/// <summary>
/// A job manifest, &lt;name&gt;.ncxjob.toml: the machine, the program of each channel, and the spindles and axes the
/// channels share (machine-config 8, D15, D20, D48). It lives with the machine model, because the job scheduler reads
/// it (D107).
/// </summary>
public sealed record JobManifest
{
    /// <summary>
    /// [job] name: the name of the job, "SHAFT"; null when left out.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// [job] machine: the machine file of the job, "nakamura-ntjx.toml" (F24).
    /// </summary>
    public required string Machine { get; init; }

    /// <summary>
    /// [[channel]]: the program of each channel, in file order.
    /// </summary>
    public IReadOnlyList<ChannelProgram> Channels { get; init; } = [];

    /// <summary>
    /// [shared] spindles: the resource ids commanded from more than one channel; the scheduler checks the conflicts
    /// (D20).
    /// </summary>
    public IReadOnlyList<string> SharedSpindles { get; init; } = [];

    /// <summary>
    /// [shared] axes: the axes commanded from more than one channel (D20, F24).
    /// </summary>
    public IReadOnlyList<string> SharedAxes { get; init; } = [];
}
