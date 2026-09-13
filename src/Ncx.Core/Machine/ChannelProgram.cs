namespace Ncx.Core.Machine;

/// <summary>
/// One [[channel]] of a job manifest: the file whose program runs on the channel (machine-config 8, D48).
/// </summary>
public sealed record ChannelProgram
{
    /// <summary>
    /// id: the channel number, 1.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// file: the NCX file, "shaft.ncx".
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// program: the NAME of the PROGRAM section that runs; null for the first program of the file (D48).
    /// </summary>
    public string? Program { get; init; }
}
