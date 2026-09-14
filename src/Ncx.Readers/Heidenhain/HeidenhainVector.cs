namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A point or a direction in the working plane, in double for the geometry of a chamfer or a rounding (D58;
/// code-guidelines 7, double only inside geometry): the coordinate of the first and of the second axis of the plane, X
/// and Y under a Z tool axis.
/// </summary>
/// <param name="First">The coordinate of the first axis of the plane.</param>
/// <param name="Second">The coordinate of the second axis of the plane.</param>
internal readonly record struct HeidenhainVector(double First, double Second)
{
    /// <summary>
    /// The length of the vector.
    /// </summary>
    public double Length => Math.Sqrt((First * First) + (Second * Second));

    /// <summary>
    /// The direction turned a quarter turn to the left, counterclockwise seen against the tool axis (language 4.3).
    /// </summary>
    public HeidenhainVector Left => new(-Second, First);

    /// <summary>
    /// The angle from the first axis of the plane in radians, counterclockwise positive.
    /// </summary>
    public double Angle => Math.Atan2(Second, First);

    /// <summary>
    /// A point of the reader as a vector.
    /// </summary>
    /// <param name="point">The point.</param>
    public static HeidenhainVector Of(HeidenhainPoint point)
    {
        return new HeidenhainVector((double)point.First, (double)point.Second);
    }

    public HeidenhainVector Plus(HeidenhainVector other)
    {
        return new HeidenhainVector(First + other.First, Second + other.Second);
    }

    public HeidenhainVector Minus(HeidenhainVector other)
    {
        return new HeidenhainVector(First - other.First, Second - other.Second);
    }

    public HeidenhainVector Times(double factor)
    {
        return new HeidenhainVector(First * factor, Second * factor);
    }

    /// <summary>
    /// The direction of the vector with the length 1.
    /// </summary>
    public HeidenhainVector Unit()
    {
        return Times(1 / Length);
    }

    public double Dot(HeidenhainVector other)
    {
        return (First * other.First) + (Second * other.Second);
    }

    /// <summary>
    /// Positive where the other direction turns to the left of this one, negative where it turns to the right.
    /// </summary>
    /// <param name="other">The other direction.</param>
    public double Cross(HeidenhainVector other)
    {
        return (First * other.Second) - (Second * other.First);
    }

    /// <summary>
    /// The point with the decimals the reader keeps for a computed number (wave-1 question #46).
    /// </summary>
    /// <param name="decimals">The decimals to keep.</param>
    public HeidenhainPoint Round(int decimals)
    {
        return new HeidenhainPoint(HeidenhainNumbers.Round(First, decimals), HeidenhainNumbers.Round(Second, decimals));
    }
}
