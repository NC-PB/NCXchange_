using Ncx.Core.Geometry;

namespace Ncx.Core.Tests.Geometry;

/// <summary>
/// Vec3 is the double of the geometry, the one type with operators (code-guidelines 3.4, 10.2); numbers enter it from
/// the model's decimals and leave it as rounded decimals (architecture 4.2, phase 1 risks).
/// </summary>
public sealed class Vec3Tests
{
    [Fact]
    public void Operators_TwoVectors_AddAndSubtractPerComponent()
    {
        var a = new Vec3(1, 2, 3);
        var b = new Vec3(10, 20, 30);

        Assert.Equal(new Vec3(11, 22, 33), a + b);
        Assert.Equal(new Vec3(9, 18, 27), b - a);
        Assert.Equal(new Vec3(-1, -2, -3), -a);
    }

    [Fact]
    public void Operators_VectorAndNumber_ScalePerComponent()
    {
        var a = new Vec3(1, -2, 4);

        Assert.Equal(new Vec3(2, -4, 8), a * 2);
        Assert.Equal(new Vec3(2, -4, 8), 2 * a);
        Assert.Equal(new Vec3(0.5, -1, 2), a / 2);
    }

    [Fact]
    public void Length_ThreeFourTwelve_IsThirteen()
    {
        Assert.Equal(13, new Vec3(3, 4, 12).Length);
    }

    // The model's decimal coordinates are converted once, component by component.
    [Fact]
    public void FromDecimals_ModelCoordinates_ConvertEachComponent()
    {
        Vec3 point = Vec3.FromDecimals(50.534m, 69.993m, -5.4m);

        Assert.Equal(new Vec3(50.534, 69.993, -5.4), point);
    }

    // A coordinate computed in double reaches the position store only as a decimal rounded to the given decimals
    // (phase 1 risks).
    [Fact]
    public void RoundToDecimal_ComputedCoordinate_RoundsToTheGivenDecimals()
    {
        Assert.Equal(69.026m, Vec3.RoundToDecimal(69.025870876999, 3));
        Assert.Equal(56.166m, Vec3.RoundToDecimal(56.165730887068, 3));
        Assert.Equal(7m, Vec3.RoundToDecimal(6.9999999999997, 3));
    }

    // Half away from zero, as NCX rounds everywhere it rounds (language 4.12, ROUND).
    [Theory]
    [InlineData(0.0005, 3, "0.001")]
    [InlineData(-0.0005, 3, "-0.001")]
    [InlineData(2.5, 0, "3")]
    [InlineData(-2.5, 0, "-3")]
    public void RoundToDecimal_HalfwayValue_RoundsAwayFromZero(double coordinate, int decimals, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Vec3.RoundToDecimal(coordinate, decimals));
    }
}
