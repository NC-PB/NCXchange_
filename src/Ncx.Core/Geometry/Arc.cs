namespace Ncx.Core.Geometry;

/// <summary>
/// A resolved arc in plane coordinates (see <see cref="Plane"/>): start, end, center and sweep, what the virtual
/// machine keeps of an ARC so that the compiler can write the CENTER form, the R form, or full turns plus a rest
/// (virtual machine 3.2, D84).
/// </summary>
internal sealed record Arc
{
    public required ArcDirection Direction { get; init; }

    /// <summary>
    /// The start: the current position.
    /// </summary>
    public required Vec3 Start { get; init; }

    /// <summary>
    /// The end: the start with the axis words applied, or the start rotated by ANGLE; its Z differs from the start's
    /// when a tool-axis word makes a helix.
    /// </summary>
    public required Vec3 End { get; init; }

    /// <summary>
    /// The center, given (CENTER, ANGLE) or computed (R). It is a point of the plane and carries the tool-axis
    /// coordinate of the start.
    /// </summary>
    public required Vec3 Center { get; init; }

    /// <summary>
    /// The radius at the start, |start - center| in the plane.
    /// </summary>
    public required double Radius { get; init; }

    /// <summary>
    /// The sweep in degrees in the direction of the verb: 360 for a full circle, more than 360 for an ANGLE of several
    /// turns.
    /// </summary>
    public required double Sweep { get; init; }

    /// <summary>
    /// The full turns within the sweep, for controllers that take at most one turn per block (D84).
    /// </summary>
    public int FullTurns => Angle.FullTurns(Sweep);

    /// <summary>
    /// The sweep beyond the full turns, in degrees (D84).
    /// </summary>
    public double Rest => Angle.Rest(Sweep);

    /// <summary>
    /// A tool-axis word makes a helix (virtual machine 3.2).
    /// </summary>
    public bool IsHelix => End.Z != Start.Z;
}
