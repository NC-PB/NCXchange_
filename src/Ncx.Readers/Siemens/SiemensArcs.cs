using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The arcs of a SINUMERIK block (controllers siemens.md 3, 11 rule 2; controller-mapping 2; language 4.3; D84): G2 is
/// ARC=CW and G3 ARC=CCW with the end point and the center I J K, incremental from the start point, or I=AC() absolute
/// (the corpus writes every center so), or the radius CR= as R, or the opening angle AR= with the center as ANGLE or
/// with the end point converted to the center; TURN= adds full turns to a helix as ANGLE; CIP through an intermediate
/// point and CT tangential to the element before are converted to CENTER arcs with the source-side state.
/// </summary>
internal static class SiemensArcs
{
    // Below this the three points of a CIP lie on a line, and the end of a CT on its tangent.
    private const double Straight = 1e-9;

    /// <summary>
    /// Reads the arc words of a motion block whose plane and axis words the motion has read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The motion block with its axis words.</param>
    /// <param name="code">G2, G3, CIP or CT.</param>
    /// <param name="plane">The working plane.</param>
    /// <param name="start">The positions before the block moved.</param>
    public static void Read(SiemensBlock block, SiemensDraftBlock main, string code, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        if (block.Find(plane.ToolCenter) is not null || block.Find(plane.ToolCenter + "1") is not null)
        {
            block.Draft.KeepAsRaw($"{plane.ToolCenter} is no center word of the working plane; an arc outside the "
                + "plane is not read (controllers siemens.md 3; virtual machine 3.2)");
            return;
        }

        switch (code)
        {
            case "CIP":
                ReadThroughPoint(block, main, plane, start);
                break;
            case "CT":
                ReadTangential(block, main, plane, start);
                break;
            default:
                ReadCircle(block, main, code == "G3", plane, start);
                break;
        }
    }

    // G2 and G3 with the center, the radius or the opening angle (controllers siemens.md 3).
    private static void ReadCircle(SiemensBlock block, SiemensDraftBlock main, bool ccw, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        main.WithVerb("ARC", Direction(ccw));
        SourceWord? radius = block.Take("CR");
        SourceWord? opening = block.Take("AR");
        SourceWord? turns = block.Take("TURN");
        SourceWord? firstCenter = block.Take(plane.FirstCenter);
        SourceWord? secondCenter = block.Take(plane.SecondCenter);
        bool centerGiven = main.Has("CENTER", plane.First) || firstCenter is not null || secondCenter is not null;
        bool planeEnd = main.Has(plane.First, null) || main.Has(plane.Second, null)
            || main.Has("I" + plane.First, null) || main.Has("I" + plane.Second, null);
        SiemensPoint? from = plane.PointOf(start);
        SiemensPoint? to = plane.PointOf(block.State.Positions);
        if (radius is not null)
        {
            ReadRadius(block, main, radius, centerGiven || opening is not null || turns is not null, from, to, ccw);
            return;
        }

        if (!centerGiven && opening is null)
        {
            block.Draft.KeepAsRaw("an arc needs its center I J K, its radius CR or its opening angle AR (controllers "
                + "siemens.md 3)");
            return;
        }

        SiemensPoint? center = main.Has("CENTER", plane.First)
            ? CenterOf(main, plane)
            : centerGiven ? AddCenter(block, main, plane, from, firstCenter, secondCenter) : null;
        if (block.Draft.IsRaw)
        {
            return;
        }

        int extra = 0;
        if (turns is not null && !Turns(block, turns, out extra))
        {
            return;
        }

        if (opening is not null)
        {
            center = ReadOpening(block, main, plane, opening, centerGiven, planeEnd, from, to, ccw, extra) ? center
                ?? CenterOf(main, plane) : null;
        }
        else if (extra > 0)
        {
            AddTurns(block, main, plane, center, from, to, ccw, extra);
        }

        EndArc(block, plane, center, ccw);
    }

    // CR=5 is R=5, negative for the arc over 180 degrees (controller-mapping 2, R).
    private static void ReadRadius(SiemensBlock block, SiemensDraftBlock main, SourceWord radius, bool other,
        SiemensPoint? from, SiemensPoint? to, bool ccw)
    {
        if (other)
        {
            block.Draft.KeepAsRaw("an arc carries its center, its radius CR or its opening angle AR, one of them");
            return;
        }

        if (SiemensExpression.ValueOf(block, radius.Text, out string? problem) is not Value value)
        {
            block.Draft.KeepAsRaw($"CR={radius.Text} has no NCX value: {problem}");
            return;
        }

        main.Add("R", value);
        SiemensPoint? center = from is SiemensPoint a && to is SiemensPoint b && radius.Number is decimal r
            ? RadiusCenter(a, b, r, ccw)
            : null;
        var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
        EndArc(block, plane, center, ccw);
    }

    // I J K incremental from the start point, I=AC() absolute, I=IC() incremental; a center word left out is 0 from the
    // start point. The reader writes the absolute center where it knows the start point, and the incremental one where
    // it does not or where X is a diameter, whose I is a radius (controller-mapping 2, CENTER; D60).
    // TODO(question): wave-2 question #55, a center word left out; it is taken as 0, as the Fanuc reader takes it.
    private static SiemensPoint? AddCenter(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        SiemensPoint? from, SourceWord? firstWord, SourceWord? secondWord)
    {
        bool diameter = block.Facts.DiameterMode != "DIAMOF" && (plane.First == "X" || plane.Second == "X");
        Value? first = CenterValue(block, firstWord, out bool firstAbsolute);
        Value? second = CenterValue(block, secondWord, out bool secondAbsolute);
        if (first is null || second is null)
        {
            return null;
        }

        decimal? firstNumber = SiemensNumbers.NumberOf(first);
        decimal? secondNumber = SiemensNumbers.NumberOf(second);
        bool known = from is not null && !diameter && firstNumber is not null && secondNumber is not null;
        decimal? firstAt = firstAbsolute ? firstNumber : known ? from!.Value.First + firstNumber : null;
        decimal? secondAt = secondAbsolute ? secondNumber : known ? from!.Value.Second + secondNumber : null;
        main.Add("CENTER", firstAt is null ? "I" + plane.First : plane.First, firstAt is decimal x
            ? SiemensNumbers.Of(x) : first);
        main.Add("CENTER", secondAt is null ? "I" + plane.Second : plane.Second, secondAt is decimal y
            ? SiemensNumbers.Of(y) : second);
        return firstAt is decimal cx && secondAt is decimal cy && !diameter ? new SiemensPoint(cx, cy) : null;
    }

    private static Value? CenterValue(SiemensBlock block, SourceWord? word, out bool absolute)
    {
        absolute = false;
        if (word is null)
        {
            return new IntegerValue(0, "0");
        }

        string text = word.Text;
        if (SiemensMotion.Inner(text, out string form) is string inner && form is "AC" or "IC")
        {
            absolute = form == "AC";
            text = inner;
        }

        Value? value = SiemensExpression.ValueOf(block, text, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"the center {word.Address}={word.Text} has no NCX value: {problem}");
        }

        return value;
    }

    // AR=90 with the center and no end point is the sweep, ANGLE; with the end point and no center the reader computes
    // the center, on the side the direction and the size of the angle give (controllers siemens.md 3;
    // controller-mapping 2, ANGLE).
    private static bool ReadOpening(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, SourceWord opening,
        bool centerGiven, bool planeEnd, SiemensPoint? from, SiemensPoint? to, bool ccw, int extra)
    {
        if (opening.Number is not decimal angle || angle <= 0 || angle > 360)
        {
            block.Draft.KeepAsRaw($"AR={opening.Text} is no opening angle above 0 and up to 360 degrees");
            return false;
        }

        if (centerGiven && !planeEnd)
        {
            main.Add("ANGLE", SiemensNumbers.Of(angle + (360m * extra)));
            SweepEnd(block, main, plane, from, angle, ccw);
            return true;
        }

        if (centerGiven || !planeEnd || from is not SiemensPoint a || to is not SiemensPoint b || extra > 0
            || (a.First == b.First && a.Second == b.Second))
        {
            block.Draft.KeepAsRaw("AR stands with the center or with the end point from a known start point, and not "
                + "with TURN (controllers siemens.md 3)");
            return false;
        }

        // The center lies on the perpendicular bisector of the chord at h = (d / 2) / tan(a / 2) from its midpoint, on
        // the left of the chord for CCW; an angle above 180 degrees makes h negative, the other side.
        double dx = (double)(b.First - a.First);
        double dy = (double)(b.Second - a.Second);
        double d = Math.Sqrt((dx * dx) + (dy * dy));
        double h = d / 2 / Math.Tan((double)angle * Math.PI / 360);
        double side = ccw ? 1 : -1;
        int decimals = Decimals(block);
        decimal x = SiemensNumbers.Round(((double)(a.First + b.First) / 2) + (side * h * -dy / d), decimals);
        decimal y = SiemensNumbers.Round(((double)(a.Second + b.Second) / 2) + (side * h * dx / d), decimals);
        main.Add("CENTER", plane.First, SiemensNumbers.Of(x)).Add("CENTER", plane.Second, SiemensNumbers.Of(y));
        return true;
    }

    // TURN=n adds n full turns before the end point of a helix: the sweep of the arc plus n times 360 degrees, ANGLE,
    // without the end point in the plane (controllers siemens.md 3; language 4.3, ANGLE; D84).
    private static void AddTurns(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, SiemensPoint? center,
        SiemensPoint? from, SiemensPoint? to, bool ccw, int extra)
    {
        if (center is not SiemensPoint c || from is not SiemensPoint a || to is not SiemensPoint b)
        {
            block.Draft.KeepAsRaw("TURN needs the absolute center, the start and the end point, which the reader does "
                + "not know here (controllers siemens.md 3)");
            return;
        }

        double start = Math.Atan2((double)(a.Second - c.Second), (double)(a.First - c.First)) * 180 / Math.PI;
        double end = Math.Atan2((double)(b.Second - c.Second), (double)(b.First - c.First)) * 180 / Math.PI;
        double sweep = ccw ? end - start : start - end;
        sweep = ((sweep % 360) + 360) % 360;
        sweep = sweep < 1e-9 ? 360 : sweep;
        decimal angle = SiemensNumbers.Round(sweep + (360.0 * extra), Decimals(block));
        main.Remove(plane.First);
        main.Remove(plane.Second);
        main.Remove("I" + plane.First);
        main.Remove("I" + plane.Second);
        main.Add("ANGLE", SiemensNumbers.Of(angle));
    }

    // An arc by its sweep ends at the start point turned about the center by the sweep in its direction (language 4.3,
    // ANGLE; virtual machine 3.2); where the reader does not know the center or the start, the end is unknown.
    private static void SweepEnd(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, SiemensPoint? from,
        decimal sweep, bool ccw)
    {
        if (CenterOf(main, plane) is not SiemensPoint c || from is not SiemensPoint a)
        {
            block.State.ForgetPosition(plane.First);
            block.State.ForgetPosition(plane.Second);
            return;
        }

        double turn = (double)sweep * Math.PI / 180 * (ccw ? 1 : -1);
        double dx = (double)(a.First - c.First);
        double dy = (double)(a.Second - c.Second);
        int decimals = Decimals(block);
        block.State.SetPosition(plane.First, SiemensNumbers.Round((double)c.First + (dx * Math.Cos(turn))
            - (dy * Math.Sin(turn)), decimals));
        block.State.SetPosition(plane.Second, SiemensNumbers.Round((double)c.Second + (dx * Math.Sin(turn))
            + (dy * Math.Cos(turn)), decimals));
    }

    private static bool Turns(SiemensBlock block, SourceWord turns, out int extra)
    {
        extra = SiemensNumbers.WholeNumber(turns.Text) ?? -1;
        if (extra < 0)
        {
            block.Draft.KeepAsRaw($"TURN={turns.Text} counts no whole turns");
            return false;
        }

        return true;
    }

    // CIP X Y I1= J1= through the intermediate point: the circle through the start point, the intermediate point and
    // the end point, turning the way the three points turn (controllers siemens.md 3; controller-mapping 2, ANGLE).
    private static void ReadThroughPoint(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        SourceWord? firstWord = block.Take(plane.FirstCenter + "1");
        SourceWord? secondWord = block.Take(plane.SecondCenter + "1");
        SiemensPoint? from = plane.PointOf(start);
        SiemensPoint? to = plane.PointOf(block.State.Positions);
        decimal? midFirst = Intermediate(block, firstWord, from?.First);
        decimal? midSecond = Intermediate(block, secondWord, from?.Second);
        bool helix = main.Has(plane.Tool, null) || main.Has("I" + plane.Tool, null);
        if (from is not SiemensPoint a || to is not SiemensPoint c || midFirst is not decimal mx
            || midSecond is not decimal my || helix)
        {
            block.Draft.KeepAsRaw("CIP needs the start point, the intermediate point I1 J1 and the end point in the "
                + "plane as numbers the reader knows (controllers siemens.md 3)");
            return;
        }

        var b = new SiemensPoint(mx, my);
        double ax = (double)a.First;
        double ay = (double)a.Second;
        double bx = (double)b.First;
        double by = (double)b.Second;
        double cx = (double)c.First;
        double cy = (double)c.Second;
        double denominator = 2 * ((ax * (by - cy)) + (bx * (cy - ay)) + (cx * (ay - by)));
        if (Math.Abs(denominator) < Straight)
        {
            block.Draft.KeepAsRaw("the three points of the CIP lie on a line");
            return;
        }

        double a2 = (ax * ax) + (ay * ay);
        double b2 = (bx * bx) + (by * by);
        double c2 = (cx * cx) + (cy * cy);
        double ux = ((a2 * (by - cy)) + (b2 * (cy - ay)) + (c2 * (ay - by))) / denominator;
        double uy = ((a2 * (cx - bx)) + (b2 * (ax - cx)) + (c2 * (bx - ax))) / denominator;
        bool ccw = ((bx - ax) * (cy - by)) - ((by - ay) * (cx - bx)) > 0;
        int decimals = Decimals(block);
        var center = new SiemensPoint(SiemensNumbers.Round(ux, decimals), SiemensNumbers.Round(uy, decimals));
        main.WithVerb("ARC", Direction(ccw));
        main.Add("CENTER", plane.First, SiemensNumbers.Of(center.First))
            .Add("CENTER", plane.Second, SiemensNumbers.Of(center.Second));
        EndArc(block, plane, center, ccw);
    }

    // The intermediate point is absolute under G90 and incremental from the start point under G91, or as AC() and
    // IC() say (controllers siemens.md 3).
    private static decimal? Intermediate(SiemensBlock block, SourceWord? word, decimal? from)
    {
        if (word is null)
        {
            return null;
        }

        string text = word.Text;
        bool incremental = block.Facts.Incremental;
        if (SiemensMotion.Inner(text, out string form) is string inner && form is "AC" or "IC")
        {
            incremental = form == "IC";
            text = inner;
        }

        decimal? value = SiemensNumbers.NumberOf(SiemensNumbers.Parse(text));
        return incremental ? value + from : value;
    }

    // CT X Y continues the element before it tangentially: the circle through the start point, tangent there to the
    // direction t, and through the end point; its center lies on the normal n of t, start + s n with s = |d|^2 /
    // (2 n.d), on the left for s > 0, CCW (controllers siemens.md 3; controller-mapping 2, ANGLE).
    private static void ReadTangential(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        SiemensPoint? from = plane.PointOf(start);
        SiemensPoint? to = plane.PointOf(block.State.Positions);
        if (from is not SiemensPoint a || to is not SiemensPoint b || block.Facts.Tangent is not SiemensPoint t)
        {
            block.Draft.KeepAsRaw("CT continues the direction of the element before it from a known point, which the "
                + "reader does not know here (controllers siemens.md 3)");
            return;
        }

        double length = Math.Sqrt(Math.Pow((double)t.First, 2) + Math.Pow((double)t.Second, 2));
        double normalX = -(double)t.Second / length;
        double normalY = (double)t.First / length;
        double dx = (double)(b.First - a.First);
        double dy = (double)(b.Second - a.Second);
        double across = (normalX * dx) + (normalY * dy);
        if (Math.Abs(across) < Straight)
        {
            block.Draft.KeepAsRaw("the end of the CT lies on its tangent, so the arc is a straight line");
            return;
        }

        double scale = ((dx * dx) + (dy * dy)) / (2 * across);
        int decimals = Decimals(block);
        var center = new SiemensPoint(SiemensNumbers.Round((double)a.First + (scale * normalX), decimals),
            SiemensNumbers.Round((double)a.Second + (scale * normalY), decimals));
        bool ccw = scale > 0;
        main.WithVerb("ARC", Direction(ccw));
        main.Add("CENTER", plane.First, SiemensNumbers.Of(center.First))
            .Add("CENTER", plane.Second, SiemensNumbers.Of(center.Second));
        EndArc(block, plane, center, ccw);
    }

    // An arc ends in the direction perpendicular to its radius at the end point, turned with the arc, which a CT after
    // it continues (controllers siemens.md 3).
    private static void EndArc(SiemensBlock block, SiemensPlane plane, SiemensPoint? center, bool ccw)
    {
        SiemensPoint? end = plane.PointOf(block.State.Positions);
        if (center is not SiemensPoint c || end is not SiemensPoint e || (e.First == c.First && e.Second == c.Second))
        {
            block.Facts.Tangent = null;
            return;
        }

        decimal radialX = e.First - c.First;
        decimal radialY = e.Second - c.Second;
        block.Facts.Tangent = ccw ? new SiemensPoint(-radialY, radialX) : new SiemensPoint(radialY, -radialX);
    }

    // The absolute center a polar arc wrote, the pole.
    private static SiemensPoint? CenterOf(SiemensDraftBlock main, SiemensPlane plane)
    {
        decimal? first = SiemensNumbers.NumberOf(main.Find("CENTER", plane.First)?.Value);
        decimal? second = SiemensNumbers.NumberOf(main.Find("CENTER", plane.Second)?.Value);
        return first is decimal x && second is decimal y ? new SiemensPoint(x, y) : null;
    }

    // The center of the arc of radius r from start to end in its direction, as the virtual machine computes it
    // (virtual machine 3.2).
    private static SiemensPoint? RadiusCenter(SiemensPoint start, SiemensPoint end, decimal r, bool ccw)
    {
        double dx = (double)(end.First - start.First);
        double dy = (double)(end.Second - start.Second);
        double d = Math.Sqrt((dx * dx) + (dy * dy));
        if (d == 0)
        {
            return null;
        }

        double radius = (double)r;
        double h = Math.Sqrt(Math.Max(0, (radius * radius) - (d * d / 4)));
        double side = (ccw && radius > 0) || (!ccw && radius < 0) ? 1 : -1;
        double x = ((double)(start.First + end.First) / 2) + (side * h * -dy / d);
        double y = ((double)(start.Second + end.Second) / 2) + (side * h * dx / d);
        return new SiemensPoint(SiemensNumbers.Round(x, 6), SiemensNumbers.Round(y, 6));
    }

    private static IdentValue Direction(bool ccw)
    {
        return new IdentValue(ccw ? "CCW" : "CW");
    }

    // Computed coordinates keep the decimals of the source words of the block, at least three (wave-1 question #46).
    private static int Decimals(SiemensBlock block)
    {
        int decimals = SiemensNumbers.LeastDecimals;
        foreach (SourceWord word in block.Source.Words)
        {
            if (word.Number is not null)
            {
                decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(word.Text));
            }
        }

        return decimals;
    }
}
