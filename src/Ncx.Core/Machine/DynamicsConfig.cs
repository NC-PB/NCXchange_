namespace Ncx.Core.Machine;

/// <summary>
/// [dynamics]: the control behaviour for the runtime estimate (machine-config 4, D64).
/// </summary>
public sealed record DynamicsConfig
{
    /// <summary>
    /// block_time: the seconds the control needs per block; a short segment cannot be faster; null when left out.
    /// </summary>
    public decimal? BlockTime { get; init; }

    /// <summary>
    /// path_mode: continuous or exact stop; null when left out.
    /// </summary>
    public PathMode? PathMode { get; init; }

    /// <summary>
    /// corner_speed: the mm/min carried through a corner in continuous mode; null when left out.
    /// </summary>
    public decimal? CornerSpeed { get; init; }
}
