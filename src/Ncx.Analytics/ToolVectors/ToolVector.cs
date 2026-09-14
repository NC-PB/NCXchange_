using System.Globalization;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.ToolVectors;

/// <summary>
/// A tool vector in double precision: the direction of the tool axis relative to the workpiece, from the words
/// TX TY TZ or from the rotary axes by the convention of implementation 14, P4-03 (virtual machine 2.2, 8).
/// </summary>
/// <param name="X">The component along X.</param>
/// <param name="Y">The component along Y.</param>
/// <param name="Z">The component along Z.</param>
internal readonly record struct ToolVector(double X, double Y, double Z)
{
    private const double RadiansPerDegree = Math.PI / 180d;

    // A component keeps three decimals in the report; a unit vector needs no more to be read.
    private const int ComponentDecimals = 3;

    /// <summary>
    /// The tool axis of a WORKPLANE, its normal: Z for XY, Y for ZX, X for YZ (language 4.2).
    /// </summary>
    public static ToolVector NormalOf(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => new ToolVector(0, 1, 0),
            Workplane.YZ => new ToolVector(1, 0, 0),
            _ => new ToolVector(0, 0, 1),
        };
    }

    /// <summary>
    /// The vector of the words TX TY TZ as the virtual machine stores them, as written (virtual machine 2.2, D81).
    /// </summary>
    public static ToolVector FromWords(IReadOnlyList<decimal> words)
    {
        return new ToolVector((double)words[0], (double)words[1], (double)words[2]);
    }

    /// <summary>
    /// The vector turned by an angle about a machine axis, X, Y or Z, by the right-hand rule.
    /// </summary>
    /// <param name="about">"X", "Y" or "Z".</param>
    /// <param name="degrees">The angle in degrees.</param>
    public ToolVector Turned(string about, double degrees)
    {
        double cos = Math.Cos(degrees * RadiansPerDegree);
        double sin = Math.Sin(degrees * RadiansPerDegree);
        return about switch
        {
            "X" => new ToolVector(X, (Y * cos) - (Z * sin), (Y * sin) + (Z * cos)),
            "Y" => new ToolVector((X * cos) + (Z * sin), Y, (Z * cos) - (X * sin)),
            _ => new ToolVector((X * cos) - (Y * sin), (X * sin) + (Y * cos), Z),
        };
    }

    /// <summary>
    /// The angle between this vector and another in degrees, from 0 to 180.
    /// </summary>
    public double DegreesTo(ToolVector other)
    {
        // From the sine and the cosine together, which keeps the small angles of a point list that the cosine alone
        // loses to rounding.
        double crossX = (Y * other.Z) - (Z * other.Y);
        double crossY = (Z * other.X) - (X * other.Z);
        double crossZ = (X * other.Y) - (Y * other.X);
        double sine = Math.Sqrt((crossX * crossX) + (crossY * crossY) + (crossZ * crossZ));
        double cosine = (X * other.X) + (Y * other.Y) + (Z * other.Z);
        return Math.Atan2(sine, cosine) / RadiansPerDegree;
    }

    /// <summary>
    /// The vector as the report writes it, the three components with three decimals: 0 0.5 0.866.
    /// </summary>
    public string ToText()
    {
        return Component(X) + " " + Component(Y) + " " + Component(Z);
    }

    // Adding 0 turns the -0 of a rounded -0.0001 into 0, which the report writes without a sign.
    private static string Component(double value)
    {
        return (Math.Round(value, ComponentDecimals) + 0d).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
