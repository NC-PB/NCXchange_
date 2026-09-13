namespace Ncx.Core.Machine;

/// <summary>
/// [cycles]: the cycle catalog of the controller family that the machine uses (machine-config 6).
/// </summary>
public sealed record CyclesConfig
{
    /// <summary>
    /// catalog: the catalog file, "heidenhain-cycles.toml", shipped with NCXchange and user-extendable; null when left
    /// out.
    /// </summary>
    public string? CatalogFile { get; init; }
}
