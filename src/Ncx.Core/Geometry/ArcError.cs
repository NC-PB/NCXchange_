namespace Ncx.Core.Geometry;

/// <summary>
/// Why an arc does not resolve: the geometric ones among the arc ERRORs of virtual machine 5, each of which the virtual
/// machine reports with its VM code. The rules about the words of an ARC block (CENTER, R or ANGLE present, both plane
/// axes of CENTER, ANGLE with R or with plane end-point words) are checked on the words before the geometry.
/// </summary>
internal enum ArcError
{
    /// <summary>
    /// CENTER form: |start - center| and |end - center| differ by more than the arc tolerance (virtual machine 3.2, 5
    /// "inconsistent center"; D36).
    /// </summary>
    InconsistentCenter,

    /// <summary>
    /// R form: d > 2|R| plus the arc tolerance, the chord is longer than the diameter (virtual machine 3.2, 5 "radius
    /// too small").
    /// </summary>
    RadiusTooSmall,

    /// <summary>
    /// R form: start = end; a full circle needs CENTER (language 4.3; virtual machine 3.2, 5 "full circle with R").
    /// </summary>
    FullCircleWithRadius,

    /// <summary>
    /// R form: R is a number, not 0 (language 4.3).
    /// </summary>
    RadiusZero,

    /// <summary>
    /// ANGLE form: the sweep is a number of degrees greater than 0 (language 4.3; virtual machine 5 "ANGLE ... not
    /// greater than 0").
    /// </summary>
    AngleNotGreaterThanZero,
}
