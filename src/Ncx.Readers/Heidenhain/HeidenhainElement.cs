namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A contour element on one side of a chamfer or a rounding, in the working plane (controllers heidenhain.md 2; D58):
/// a line from its start to its end, or an arc about its centre in its direction. The end of the element before the
/// corner and the start of the element after it are the corner point.
/// </summary>
internal sealed record HeidenhainElement
{
    /// <summary>
    /// Where the element starts.
    /// </summary>
    public required HeidenhainVector Start { get; init; }

    /// <summary>
    /// Where the element ends.
    /// </summary>
    public required HeidenhainVector End { get; init; }

    /// <summary>
    /// The centre of an arc; null for a line.
    /// </summary>
    public HeidenhainVector? Center { get; init; }

    /// <summary>
    /// True for an arc counterclockwise, ARC=CCW, DR+ (language 4.3).
    /// </summary>
    public bool Counterclockwise { get; init; }

    /// <summary>
    /// The radius of an arc, from its centre to the corner point.
    /// </summary>
    public double Radius { get; init; }

    /// <summary>
    /// The sweep of an arc in radians in its direction, more than a turn for the ANGLE of D84; 0 for a line.
    /// </summary>
    public double Sweep { get; init; }

    /// <summary>
    /// The R of an arc written by its radius as the source writes it, negative for more than a half turn (language 4.3,
    /// R); null for another element.
    /// </summary>
    public decimal? WrittenRadius { get; init; }

    /// <summary>
    /// True for an arc written by its sweep, the ANGLE of CP IPA beyond 360 degrees (D84).
    /// </summary>
    public bool WritesAngle { get; init; }

    /// <summary>
    /// True where the element names its end point by incremental words.
    /// </summary>
    public bool Incremental { get; init; }

    /// <summary>
    /// The decimals of the numbers the points of the element have.
    /// </summary>
    public int Decimals { get; init; }

    /// <summary>
    /// True for an arc.
    /// </summary>
    public bool IsArc => Center is not null;

    /// <summary>
    /// The length of a line.
    /// </summary>
    public double Length => End.Minus(Start).Length;

    /// <summary>
    /// The angle an arc about a centre sweeps from one point to another in its direction, from 0 up to a full turn.
    /// </summary>
    /// <param name="center">The centre.</param>
    /// <param name="from">The point the arc sweeps from.</param>
    /// <param name="to">The point the arc sweeps to.</param>
    /// <param name="counterclockwise">True for an arc counterclockwise.</param>
    public static double SweepBetween(HeidenhainVector center, HeidenhainVector from, HeidenhainVector to,
        bool counterclockwise)
    {
        double sweep = to.Minus(center).Angle - from.Minus(center).Angle;
        if (!counterclockwise)
        {
            sweep = -sweep;
        }

        while (sweep < 0)
        {
            sweep += 2 * Math.PI;
        }

        while (sweep >= 2 * Math.PI)
        {
            sweep -= 2 * Math.PI;
        }

        return sweep;
    }

    /// <summary>
    /// The direction the element runs in at a point of it: a line along itself, an arc across its radius, turned with
    /// the arc (language 4.3, CW and CCW).
    /// </summary>
    /// <param name="point">A point of the element.</param>
    public HeidenhainVector DirectionAt(HeidenhainVector point)
    {
        if (Center is not HeidenhainVector center)
        {
            return End.Minus(Start).Unit();
        }

        HeidenhainVector across = point.Minus(center).Unit().Left;
        return Counterclockwise ? across : across.Times(-1);
    }

    /// <summary>
    /// The point of the element nearest to a point: the foot of the perpendicular on a line, the point of the circle
    /// toward it on an arc; where a rounding about that point touches the element.
    /// </summary>
    /// <param name="point">The point, the centre of a rounding.</param>
    public HeidenhainVector Touch(HeidenhainVector point)
    {
        if (Center is not HeidenhainVector center)
        {
            HeidenhainVector along = End.Minus(Start).Unit();
            return Start.Plus(along.Times(point.Minus(Start).Dot(along)));
        }

        return center.Plus(point.Minus(center).Unit().Times(Radius));
    }

    /// <summary>
    /// How far a point of the element lies from its start along it: the length along a line, the angle an arc sweeps.
    /// </summary>
    /// <param name="point">A point of the element.</param>
    public double FromStart(HeidenhainVector point)
    {
        return Center is HeidenhainVector center
            ? SweepBetween(center, Start, point, Counterclockwise)
            : point.Minus(Start).Dot(End.Minus(Start).Unit());
    }

    /// <summary>
    /// How far a point of the element lies from its end along it: the length along a line, the angle an arc sweeps.
    /// </summary>
    /// <param name="point">A point of the element.</param>
    public double ToEnd(HeidenhainVector point)
    {
        return Center is HeidenhainVector center
            ? SweepBetween(center, point, End, Counterclockwise)
            : End.Minus(point).Dot(End.Minus(Start).Unit());
    }
}
