namespace Ncx.Core.Machine;

/// <summary>
/// One [[axis]]: its names, kind and owner, the limits and reference points in machine coordinates (the G53 / M91
/// frame), and the dynamics for the runtime estimate (machine-config 4, D100).
/// </summary>
public sealed record AxisDef
{
    /// <summary>
    /// id: the machine's own name of the axis, X1, C2.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ncx: the name NCX programs use for the axis, X, Z2 (D30, D93).
    /// </summary>
    public required string NcxName { get; init; }

    /// <summary>
    /// letter: the address written in the output, X, "C2" (D30); null when left out.
    /// </summary>
    public string? Letter { get; init; }

    /// <summary>
    /// incremental_letter: the incremental form on Fanuc lathes, U for X; null when left out.
    /// </summary>
    public string? IncrementalLetter { get; init; }

    /// <summary>
    /// kind: linear or rotary.
    /// </summary>
    public required AxisKind Kind { get; init; }

    /// <summary>
    /// owner: the resource id the axis belongs to, S1 for the C axis of the main spindle; null when left out.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// limits: the lower travel limit in machine coordinates; for a modulo axis the start of the display range (D100);
    /// null when left out.
    /// </summary>
    public decimal? Min { get; init; }

    /// <summary>
    /// limits: the upper travel limit in machine coordinates; for a modulo axis the end of the display range (D100);
    /// null when left out.
    /// </summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// rapid: the rapid speed in mm/min or deg/min, for the runtime estimate (D64); null when left out.
    /// </summary>
    public decimal? Rapid { get; init; }

    /// <summary>
    /// max_feed: the feed this axis can follow in mm/min or deg/min; the expander warns or clamps (D64); null when
    /// left out.
    /// </summary>
    public decimal? MaxFeed { get; init; }

    /// <summary>
    /// acceleration: in mm/s² or deg/s², for the trapezoidal profile of the runtime estimate (D64); null when left
    /// out.
    /// </summary>
    public decimal? Acceleration { get; init; }

    /// <summary>
    /// programming: diameter, radius or switchable, for the X axis of a lathe (D60); null when left out.
    /// </summary>
    public Programming? Programming { get; init; }

    /// <summary>
    /// home: reference point 1 in machine coordinates, for HOME (D100); null for an axis without a reference point.
    /// </summary>
    public decimal? Home { get; init; }

    /// <summary>
    /// home2: reference point 2 in machine coordinates, for HOME under POINT=2 (D100); null when left out.
    /// </summary>
    public decimal? Home2 { get; init; }

    /// <summary>
    /// clamp: the clamping templates of a rotary axis, ON = "M10", OFF = "M11"; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Clamp { get; init; } = new Dictionary<string, string>();
}
