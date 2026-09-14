namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The rounding RND of a corner next to an arc (controllers heidenhain.md 2; D58, language 4.3): the arc of radius R
/// tangent to the element before the corner and to the element after it, turning the way the corner turns. Its centre
/// lies at R from both elements on the side the corner turns to: on the line of a straight element moved across by R,
/// on the circle of an arc with its radius less R where the arc turns the same way as the rounding and more R where it
/// turns the other way. Of the points where the two meet, the rounding of the corner is the one nearest the corner
/// point that touches both elements within them. Between two lines HeidenhainCorners computes the rounding by the angle
/// of the corner.
/// </summary>
internal static class HeidenhainRounding
{
    // Below this a length or an angle is none.
    private const double Near = 1e-9;

    /// <summary>
    /// Fits the rounding of radius R between two elements, one of them an arc, at a corner that turns left or right:
    /// its centre and the points where it touches the element before and the element after; the reason it does not fit
    /// otherwise.
    /// </summary>
    /// <param name="before">The element before the corner.</param>
    /// <param name="after">The element after the corner.</param>
    /// <param name="radius">The radius R of the rounding.</param>
    /// <param name="left">True for a corner that turns left, where the rounding is counterclockwise.</param>
    /// <param name="center">The centre of the rounding.</param>
    /// <param name="begin">Where the rounding touches the element before the corner.</param>
    /// <param name="end">Where the rounding touches the element after the corner.</param>
    public static string? Fit(HeidenhainElement before, HeidenhainElement after, double radius, bool left,
        out HeidenhainVector center, out HeidenhainVector begin, out HeidenhainVector end)
    {
        double side = left ? 1 : -1;
        var centers = new List<HeidenhainVector>();
        if (!before.IsArc)
        {
            MeetLine(before, after, radius, side, centers);
        }
        else if (!after.IsArc)
        {
            MeetLine(after, before, radius, side, centers);
        }
        else
        {
            MeetCircles(before, after, radius, side, centers);
        }

        center = before.End;
        begin = before.End;
        end = after.Start;
        double nearest = double.MaxValue;
        foreach (HeidenhainVector candidate in centers)
        {
            HeidenhainVector onBefore = before.Touch(candidate);
            HeidenhainVector onAfter = after.Touch(candidate);
            double distance = candidate.Minus(before.End).Length;
            if (Within(before, before.ToEnd(onBefore)) && Within(after, after.FromStart(onAfter)) && distance < nearest)
            {
                nearest = distance;
                center = candidate;
                begin = onBefore;
                end = onAfter;
            }
        }

        return nearest < double.MaxValue
            ? null
            : "the rounding does not fit the elements of the corner: larger than an arc it rounds on the inside, or "
                + "longer than an element";
    }

    // The radius of the circle the centre of the rounding lies on next to an arc: the radius of the arc less R where
    // the arc turns the same way as the rounding, and more R where it turns the other way.
    private static double Offset(HeidenhainElement arc, double radius, double side)
    {
        return arc.Radius - (side * (arc.Counterclockwise ? 1 : -1) * radius);
    }

    // The centres on the line of a straight element moved by R to the side the corner turns to and on the circle of an
    // arc.
    private static void MeetLine(HeidenhainElement line, HeidenhainElement arc, double radius, double side,
        List<HeidenhainVector> centers)
    {
        double reach = Offset(arc, radius, side);
        if (reach <= Near)
        {
            return;
        }

        HeidenhainVector along = line.End.Minus(line.Start).Unit();
        HeidenhainVector through = line.Start.Plus(along.Left.Times(side * radius));
        HeidenhainVector fromCenter = through.Minus(arc.Center!.Value);
        double half = along.Dot(fromCenter);
        double discriminant = (half * half) - (fromCenter.Dot(fromCenter) - (reach * reach));
        if (discriminant < 0)
        {
            return;
        }

        double root = Math.Sqrt(discriminant);
        centers.Add(through.Plus(along.Times(-half + root)));
        centers.Add(through.Plus(along.Times(-half - root)));
    }

    // The centres on the circles of two arcs, each with its radius grown or shrunk by R.
    private static void MeetCircles(HeidenhainElement before, HeidenhainElement after, double radius, double side,
        List<HeidenhainVector> centers)
    {
        double first = Offset(before, radius, side);
        double second = Offset(after, radius, side);
        HeidenhainVector between = after.Center!.Value.Minus(before.Center!.Value);
        double distance = between.Length;
        if (first <= Near || second <= Near || distance <= Near || distance > first + second
            || distance < Math.Abs(first - second))
        {
            return;
        }

        double along = ((first * first) - (second * second) + (distance * distance)) / (2 * distance);
        double across = Math.Sqrt(Math.Max(0, (first * first) - (along * along)));
        HeidenhainVector unit = between.Times(1 / distance);
        HeidenhainVector foot = before.Center.Value.Plus(unit.Times(along));
        centers.Add(foot.Plus(unit.Left.Times(across)));
        centers.Add(foot.Minus(unit.Left.Times(across)));
    }

    // A rounding touches an element within it: on a line up to its whole length, on an arc short of its whole sweep, so
    // that the arc keeps a part and does not end where it starts (D122).
    private static bool Within(HeidenhainElement element, double measure)
    {
        return measure > Near && (element.IsArc ? measure < element.Sweep - Near : measure <= element.Length + Near);
    }
}
