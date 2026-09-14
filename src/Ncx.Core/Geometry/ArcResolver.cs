namespace Ncx.Core.Geometry;

/// <summary>
/// Resolves an ARC in its working plane (virtual machine 3.2): the CENTER form with the tolerance check, the R form
/// with the center formula, the ANGLE form of D84. Pure geometry: points come in plane coordinates (see
/// <see cref="Plane"/>), converted once from the model's decimals (<see cref="Vec3.FromDecimals"/>); the radius, the
/// sweep and the arc tolerance come in as the decimals of the word and the configuration and are converted here, once
/// (D36; phase 1 risks). The plane geometry uses X and Y only; a Z that differs between start and end is the travel
/// of a helix. A problem is an <see cref="ArcResult"/> with its <see cref="ArcError"/>, never an exception.
/// </summary>
internal static class ArcResolver
{
    /// <summary>
    /// The CENTER form: from start to end around the center (virtual machine 3.2).
    /// </summary>
    /// <param name="direction">The verb, ARC=CW or ARC=CCW.</param>
    /// <param name="start">The current position.</param>
    /// <param name="end">The start with the axis words of the block applied.</param>
    /// <param name="center">
    /// The center, absolute: CENTER:X as written, CENTER:IX added to the start by the caller; its Z is not used.
    /// </param>
    /// <param name="arcTolerance">The arc tolerance of the configuration in the active units (D36).</param>
    public static ArcResult ResolveCenterForm(ArcDirection direction, Vec3 start, Vec3 end, Vec3 center,
        decimal arcTolerance)
    {
        Vec3 centerInPlane = new(center.X, center.Y, start.Z);

        // The VM validates |start - center| against |end - center| with the tolerance from the machine configuration
        // (virtual machine 3.2, D36).
        double startRadius = DistanceInPlane(start, centerInPlane);
        double endRadius = DistanceInPlane(end, centerInPlane);
        if (Math.Abs(startRadius - endRadius) > (double)arcTolerance)
        {
            return ArcResult.Failed(ArcError.InconsistentCenter);
        }

        // Start = end is a full circle (virtual machine 3.2), a full turn of a helix when a tool-axis word moves the
        // end; otherwise the arc turns from the start to the end in the direction of the verb.
        // TODO(question): start = end is read as equal plane coordinates. Whether an end within the arc tolerance of
        // the start is a full circle, and what an arc of radius 0 (a center on the start) is, the documents do not say;
        // such arcs resolve to what their coordinates give (a sweep near 0 or near 360, a radius near 0) (D122).
        double sweep = Angle.FullTurn;
        if (!SameInPlane(start, end))
        {
            sweep = Angle.Turn(Angle.Of(start - centerInPlane), Angle.Of(end - centerInPlane), direction);
        }

        return Resolved(direction, start, end, centerInPlane, sweep);
    }

    /// <summary>
    /// The R form: from start to end with a signed radius (virtual machine 3.2).
    /// </summary>
    /// <param name="direction">The verb, ARC=CW or ARC=CCW.</param>
    /// <param name="start">The current position.</param>
    /// <param name="end">The start with the axis words of the block applied.</param>
    /// <param name="radius">R as written: positive for 180 degrees or less, negative for more (language 4.3).</param>
    /// <param name="arcTolerance">The arc tolerance of the configuration in the active units (D36).</param>
    public static ArcResult ResolveRadiusForm(ArcDirection direction, Vec3 start, Vec3 end, decimal radius,
        decimal arcTolerance)
    {
        // R is a number, not 0 (language 4.3).
        if (radius == 0)
        {
            return ArcResult.Failed(ArcError.RadiusZero);
        }

        // Start = end with R: ERROR; full circles need CENTER (virtual machine 3.2, language 4.3).
        if (SameInPlane(start, end))
        {
            return ArcResult.Failed(ArcError.FullCircleWithRadius);
        }

        // With d = |end - start|, d > 2|R| plus tolerance is an ERROR (virtual machine 3.2).
        double signedRadius = (double)radius;
        Vec3 chord = InPlane(end - start);
        double chordLength = chord.Length;
        if (chordLength > (2 * Math.Abs(signedRadius)) + (double)arcTolerance)
        {
            return ArcResult.Failed(ArcError.RadiusTooSmall);
        }

        // h = sqrt(R² - (d/2)²), M = midpoint, u = (end - start)/d, n = left normal of u (virtual machine 3.2). A chord
        // longer than 2|R| but within the tolerance leaves nothing under the root: h is 0, the center the midpoint.
        double halfChord = chordLength / 2;
        double centerDistance = Math.Sqrt(Math.Max(0, (signedRadius * signedRadius) - (halfChord * halfChord)));
        Vec3 midpoint = start + (chord / 2);
        Vec3 leftNormal = LeftNormal(chord / chordLength);

        // Center = M + s·h·n with s = +1 for (CCW, R > 0) and (CW, R < 0), s = -1 otherwise (virtual machine 3.2).
        bool counterclockwise = direction == ArcDirection.Counterclockwise;
        bool centerOnTheLeft = (counterclockwise && signedRadius > 0) || (!counterclockwise && signedRadius < 0);
        double side = centerOnTheLeft ? 1 : -1;
        Vec3 center = midpoint + (side * centerDistance * leftNormal);

        // The VM keeps the computed center, so the compiler can write either form (virtual machine 3.2).
        double sweep = Angle.Turn(Angle.Of(start - center), Angle.Of(end - center), direction);
        return Resolved(direction, start, end, center, sweep);
    }

    /// <summary>
    /// The ANGLE form: the start rotated about the center by the sweep in the direction of the verb (virtual machine
    /// 3.2, D84).
    /// </summary>
    /// <param name="direction">The verb, ARC=CW or ARC=CCW.</param>
    /// <param name="start">The current position.</param>
    /// <param name="toolAxisEnd">
    /// The Z of the end: the start's without a tool-axis word, the helix end with one.
    /// </param>
    /// <param name="center">The center, absolute, as for the CENTER form; its Z is not used.</param>
    /// <param name="angle">ANGLE as written, in degrees: unsigned, more than 360 for several turns
    /// (language 4.3).</param>
    public static ArcResult ResolveAngleForm(ArcDirection direction, Vec3 start, double toolAxisEnd, Vec3 center,
        decimal angle)
    {
        // ANGLE is a number of degrees greater than 0 (language 4.3, virtual machine 5).
        if (angle <= 0)
        {
            return ArcResult.Failed(ArcError.AngleNotGreaterThanZero);
        }

        // The end point in the plane is the start point rotated about the center by ANGLE degrees in the direction of
        // the verb; every full turn brings it back to the start, so the rest alone places it. A tool-axis word
        // distributes its travel over the whole sweep, a helix of ANGLE/360 turns that ends at its tool-axis
        // coordinate (virtual machine 3.2, D84).
        Vec3 centerInPlane = new(center.X, center.Y, start.Z);
        double sweep = (double)angle;
        double rest = Angle.Rest(sweep);
        double turn = direction == ArcDirection.Counterclockwise ? rest : -rest;
        Vec3 radial = Rotate(InPlane(start - centerInPlane), turn);
        Vec3 end = new(centerInPlane.X + radial.X, centerInPlane.Y + radial.Y, toolAxisEnd);

        // The VM stores start, center, sweep and end, so the compiler can write full turns plus a rest for controllers
        // that take at most one turn per block, or the sweep form for those that have it (virtual machine 3.2).
        return Resolved(direction, start, end, centerInPlane, sweep);
    }

    private static ArcResult Resolved(ArcDirection direction, Vec3 start, Vec3 end, Vec3 center, double sweep)
    {
        return ArcResult.Resolved(new Arc
        {
            Direction = direction,
            Start = start,
            End = end,
            Center = center,
            Radius = DistanceInPlane(start, center),
            Sweep = sweep,
        });
    }

    // X and Y are the plane axes, Z the tool axis (Plane): a vector in the plane has no Z.
    private static Vec3 InPlane(Vec3 vector)
    {
        return new Vec3(vector.X, vector.Y, 0);
    }

    private static double DistanceInPlane(Vec3 point, Vec3 center)
    {
        return InPlane(point - center).Length;
    }

    private static bool SameInPlane(Vec3 first, Vec3 second)
    {
        return first.X == second.X && first.Y == second.Y;
    }

    // The left normal of a direction in the plane: the direction turned a quarter counterclockwise, from the first
    // plane axis toward the second (virtual machine 3.2).
    private static Vec3 LeftNormal(Vec3 direction)
    {
        return new Vec3(-direction.Y, direction.X, 0);
    }

    // A vector in the plane turned by an angle in degrees, counterclockwise for a positive angle.
    private static Vec3 Rotate(Vec3 planeVector, double degrees)
    {
        double radians = Angle.ToRadians(degrees);
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new Vec3(
            (planeVector.X * cos) - (planeVector.Y * sin),
            (planeVector.X * sin) + (planeVector.Y * cos),
            0);
    }
}
