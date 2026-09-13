namespace Ncx.Core.Machine;

/// <summary>
/// One [[resource]]: a spindle, tool holder or table that roles and defaults resolve to (machine-config 4, virtual
/// machine 3.8).
/// </summary>
public sealed record ResourceDef
{
    /// <summary>
    /// id: the machine's own name of the resource, S1, H1.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// type: work spindle, tool spindle, tool holder or table.
    /// </summary>
    public required ResourceType Type { get; init; }

    /// <summary>
    /// axis: the id of the C axis this spindle becomes in AXIS mode, C1; null when left out.
    /// </summary>
    public string? Axis { get; init; }

    /// <summary>
    /// spindle: the id of the tool spindle of a holder, S3; null when left out.
    /// </summary>
    public string? Spindle { get; init; }

    /// <summary>
    /// magazine: the holder has a magazine that prepares tools; false when left out.
    /// </summary>
    public bool Magazine { get; init; }

    /// <summary>
    /// channel: the channel that commands this resource on a machine with several, turret 2 on channel 2 (F23); null
    /// when left out.
    /// </summary>
    public int? Channel { get; init; }

    /// <summary>
    /// True for a work spindle and a tool spindle, the resources that SPINDLE and RPM address.
    /// </summary>
    public bool IsSpindle => Type is ResourceType.WorkSpindle or ResourceType.ToolSpindle;
}
