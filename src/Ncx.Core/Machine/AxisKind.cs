namespace Ncx.Core.Machine;

/// <summary>
/// [[axis]] kind (machine-config 4).
/// </summary>
public enum AxisKind
{
    /// <summary>
    /// kind = "linear": moves in mm.
    /// </summary>
    Linear,

    /// <summary>
    /// kind = "rotary": turns in degrees.
    /// </summary>
    Rotary,
}
