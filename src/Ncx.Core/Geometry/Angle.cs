namespace Ncx.Core.Geometry;

/// <summary>
/// Angles in degrees, as NCX writes every angle (language 4.12), and the turns of an arc: the turn in the direction of
/// the verb (language 4.3, virtual machine 3.2) and a sweep split into full turns and a rest (D84).
/// </summary>
internal static class Angle
{
    /// <summary>
    /// One full turn in degrees.
    /// </summary>
    public const double FullTurn = 360;

    public static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    public static double ToDegrees(double radians)
    {
        return radians * 180 / Math.PI;
    }

    // The direction of a vector in plane coordinates, measured from the first plane axis toward the second, which is
    // the counterclockwise sense of every working plane (virtual machine 3.2); between -180 and 180 degrees.
    public static double Of(Vec3 planeVector)
    {
        return ToDegrees(Math.Atan2(planeVector.Y, planeVector.X));
    }

    // How far an arc turns from one direction to another in the direction of its verb: CCW counts from the first plane
    // axis toward the second, CW the other way (language 4.3, virtual machine 3.2). Between 0 for the same direction
    // and one full turn for a direction just behind.
    public static double Turn(double fromDegrees, double toDegrees, ArcDirection direction)
    {
        double turn = direction == ArcDirection.Counterclockwise ? toDegrees - fromDegrees : fromDegrees - toDegrees;
        turn %= FullTurn;
        if (turn < 0)
        {
            turn += FullTurn;
        }

        return turn;
    }

    // A sweep of more than 360 degrees is more than one turn; the compiler writes full turns plus a rest for
    // controllers that take at most one turn per block (virtual machine 3.2, D84).
    public static int FullTurns(double sweep)
    {
        return (int)Math.Floor(sweep / FullTurn);
    }

    public static double Rest(double sweep)
    {
        return sweep - (FullTurns(sweep) * FullTurn);
    }
}
