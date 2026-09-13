namespace Ncx.Core.Machine;

/// <summary>
/// [retract]: the templates of the RETRACT verb (machine-config 5, D83). An empty template means a computed
/// machine-frame move when the kinematics is known, else an ERROR at compile time.
/// </summary>
public sealed record RetractTable
{
    /// <summary>
    /// MAX: written for a bare RETRACT, to the axis limit, "M140 MB MAX"; null when left out.
    /// </summary>
    public string? Max { get; init; }

    /// <summary>
    /// BY: written for RETRACT=50, "M140 MB{distance}"; null when left out.
    /// </summary>
    public string? By { get; init; }
}
