namespace Ncx.Core.Machine;

/// <summary>
/// [format] block_numbers = { enabled = true, start = 10, step = 10 }: whether and how the compiler numbers the
/// blocks it writes (machine-config 2).
/// </summary>
public sealed record BlockNumbering
{
    /// <summary>
    /// enabled: whether blocks are numbered; false when left out.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// start: the first block number, 10 (Heidenhain: 0); null when left out.
    /// </summary>
    public int? Start { get; init; }

    /// <summary>
    /// step: the increment, 10 (Heidenhain: 1); null when left out.
    /// </summary>
    public int? Step { get; init; }
}
