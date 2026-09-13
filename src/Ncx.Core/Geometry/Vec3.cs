namespace Ncx.Core.Geometry;

/// <summary>
/// A point or a direction in double precision, the one type of the code with operators (code-guidelines 3.4, 10.2).
/// Geometry is computed in double while every stored number stays a decimal with its text: a coordinate enters here
/// once from the model's decimals and goes back to a position only as a rounded decimal (architecture 4.2, D62; phase 1
/// risks). For an arc the three components are plane coordinates (see <see cref="Plane"/>).
/// </summary>
/// <param name="X">The first component; for an arc, along the first plane axis.</param>
/// <param name="Y">The second component; for an arc, along the second plane axis.</param>
/// <param name="Z">The third component; for an arc, along the tool axis.</param>
internal readonly record struct Vec3(double X, double Y, double Z)
{
    /// <summary>
    /// The euclidean length.
    /// </summary>
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public static Vec3 operator +(Vec3 left, Vec3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vec3 operator -(Vec3 left, Vec3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vec3 operator -(Vec3 vector) => new(-vector.X, -vector.Y, -vector.Z);

    public static Vec3 operator *(Vec3 vector, double factor) => new(vector.X * factor, vector.Y * factor,
        vector.Z * factor);

    public static Vec3 operator *(double factor, Vec3 vector) => vector * factor;

    public static Vec3 operator /(Vec3 vector, double divisor) => new(vector.X / divisor, vector.Y / divisor,
        vector.Z / divisor);

    // The model's decimal coordinates enter the geometry here, converted once (architecture 4.2, phase 1 risks).
    public static Vec3 FromDecimals(decimal x, decimal y, decimal z)
    {
        return new Vec3((double)x, (double)y, (double)z);
    }

    // A coordinate computed in double reaches the position store only as a decimal rounded to the units' decimals,
    // never as a double (phase 1 risks, D62). Half away from zero is the one rounding NCX defines (language 4.12,
    // ROUND).
    // TODO(question): no document says how many decimals "the units' decimals" are (D62 is the math library, and the
    // [format] decimals of machine-config 2 belong to the compiler, per address and machine), so the caller passes
    // them.
    public static decimal RoundToDecimal(double coordinate, int decimals)
    {
        return Math.Round((decimal)coordinate, decimals, MidpointRounding.AwayFromZero);
    }
}
