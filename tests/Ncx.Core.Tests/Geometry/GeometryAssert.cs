using Ncx.Core.Geometry;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// Compares computed geometry. Geometry is double (architecture 4.2), so a point is compared within a precision far
/// below the arc tolerance of D36: a test fails on a wrong formula, never on the last bit of a double.
/// </summary>
internal static class GeometryAssert
{
    public const double Precision = 1e-9;

    // The arc tolerance of D36 for a program in MM.
    public const decimal ArcToleranceMm = 0.01m;

    public static void Point(double expectedX, double expectedY, Vec3 actual)
    {
        Assert.Equal(expectedX, actual.X, Precision);
        Assert.Equal(expectedY, actual.Y, Precision);
    }

    public static void Point(double expectedX, double expectedY, double expectedZ, Vec3 actual)
    {
        Point(expectedX, expectedY, actual);
        Assert.Equal(expectedZ, actual.Z, Precision);
    }

    // A resolved arc; for a failed one the first assertion shows the error.
    public static Arc Resolved(ArcResult result)
    {
        Assert.Null(result.Error);
        Assert.NotNull(result.Arc);
        return result.Arc;
    }
}
