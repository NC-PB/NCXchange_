namespace Ncx.Config;

/// <summary>
/// One tool of the tool table: its kind, length and radius (machine-config 1, D10).
/// </summary>
public sealed record ToolData
{
    /// <summary>
    /// kind: the tool kind, ROTARY or TURNING, a key of [tool_change] kind_map that gives the value of {kind}
    /// (machine-config 3, D52); null when left out.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// length: the tool length; null when left out.
    /// </summary>
    public decimal? Length { get; init; }

    /// <summary>
    /// radius: the tool radius; null when left out.
    /// </summary>
    public decimal? Radius { get; init; }
}
