using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The arcs of a Klartext program (controllers heidenhain.md 2, 7 rules 1 and 5; controller-mapping 2; language 4.3;
/// D84): CC sets the pole, C draws the arc about it and becomes one ARC with the absolute CENTER, CR is the ARC with R,
/// CT the tangential arc converted to a CENTER arc, CP the polar arc about the pole converted to Cartesian, and CP IPA
/// beyond 360 degrees the ARC with ANGLE. DR+ is CCW, DR- is CW.
/// </summary>
internal static class HeidenhainArcs
{
    // Below this the end of a CT lies on its tangent, and the arc is a straight line.
    private const double Straight = 1e-9;

    /// <summary>
    /// Reads CC X+50 Y+50: the pole, absolute; CC IX IY incremental from the current position; CC alone takes the
    /// current position (controllers heidenhain.md 2). CC writes no NCX block; the C and CP after it carry the centre.
    /// </summary>
    /// <param name="block">The block being read, whose first word is CC.</param>
    public static void ReadPole(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        HeidenhainState state = block.Heidenhain;
        if (!state.PlaneAxes(out string first, out string second, out _))
        {
            block.MarkAllRead();
            state.Pole = null;
            return;
        }

        // Up to the element after a chamfer or a rounding the current position is the corner point
        // (HeidenhainCorners.CurrentPosition).
        List<SourceWord> words = HeidenhainMotion.AxisWords(block, [first, second]);
        state.Pole = PoleOf(words, first, HeidenhainCorners.CurrentPosition(block), out int decimals);
        state.PoleDecimals = decimals;
    }

    /// <summary>
    /// The pole of CC X+50 Y+50 from its words in the plane: absolute, CC IX IY incremental from the current position,
    /// CC alone the current position (controllers heidenhain.md 2); null where it is not known.
    /// </summary>
    /// <param name="words">The axis words of the CC in the plane.</param>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="current">The current position; null where it is not known.</param>
    /// <param name="decimals">The decimals of the source words the pole is taken from, at least three.</param>
    public static HeidenhainPoint? PoleOf(IReadOnlyList<SourceWord> words, string first, HeidenhainPoint? current,
        out int decimals)
    {
        decimals = HeidenhainNumbers.LeastDecimals;
        decimal? x = words.Count == 0 ? current?.First : null;
        decimal? y = words.Count == 0 ? current?.Second : null;
        foreach (SourceWord word in words)
        {
            bool isFirst = HeidenhainMotion.AxisOf(word) == first;
            decimal? here = isFirst ? current?.First : current?.Second;
            decimal? from = word.Address.StartsWith('I') ? here : 0m;
            decimal? value = word.Number is decimal number && from is decimal origin ? origin + number : null;
            decimals = Math.Max(decimals, HeidenhainNumbers.DecimalsOf(word));
            x = isFirst ? value : x;
            y = isFirst ? y : value;
        }

        // TODO(question): wave-2 question #86, heidenhain 2 does not say what a CC with one axis of the plane leaves
        // for the other, the coordinate of the earlier pole or of the current position; the pole is then not known, and
        // the arcs about it stay RAW.
        return x is decimal poleX && y is decimal poleY ? new HeidenhainPoint(poleX, poleY) : null;
    }

    /// <summary>
    /// Reads C X+70 Y+50 DR+, the arc about the pole to the end point: ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50
    /// (heidenhain 7 rule 5); a tool-axis word makes a helix.
    /// </summary>
    /// <param name="block">The block being read, whose first word is C.</param>
    public static void ReadCenterArc(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        HeidenhainState state = block.Heidenhain;
        if (!Begin(block, out string first, out string second, out string tool, out string? direction))
        {
            return;
        }

        if (state.Pole is not HeidenhainPoint pole)
        {
            block.Draft.KeepAsRaw("C needs the pole of a CC (controllers heidenhain.md 2)");
            return;
        }

        List<SourceWord> axes = HeidenhainMotion.AxisWords(block, [first, second, tool]);
        HeidenhainDraftBlock main = block.Draft.Main.WithVerb("ARC", new IdentValue(direction!));
        if (!HeidenhainMotion.AddAxes(block, main, axes))
        {
            return;
        }

        // The arc after an expanded chamfer or rounding ends where the source's does (D58).
        HeidenhainCorners.CompleteEnd(block, main);
        main.Add("CENTER", first, HeidenhainNumbers.Of(pole.First))
            .Add("CENTER", second, HeidenhainNumbers.Of(pole.Second));
        EndArc(block, pole, direction == "CCW");
    }

    /// <summary>
    /// Reads CR X+2 Y+7 R+5 DR-, the arc by its radius: ARC=CW X=2 Y=7 R=5; a positive radius is an arc of at most 180
    /// degrees, a negative one of more, as in NCX (controllers heidenhain.md 2; language 4.3).
    /// </summary>
    /// <param name="block">The block being read, whose first word is CR.</param>
    public static void ReadRadiusArc(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        SourceWord? radius = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "R" && word.Text.Length > 0 && word.Number != 0m)
            {
                radius = word;
                block.MarkRead(word);
                break;
            }
        }

        HeidenhainState state = block.Heidenhain;
        HeidenhainPoint? start = state.Position();
        if (!Begin(block, out string first, out string second, out string tool, out string? direction))
        {
            return;
        }

        if (radius is null)
        {
            block.Draft.KeepAsRaw("CR needs its radius R (controllers heidenhain.md 2)");
            return;
        }

        List<SourceWord> axes = HeidenhainMotion.AxisWords(block, [first, second, tool]);
        HeidenhainDraftBlock main = block.Draft.Main.WithVerb("ARC", new IdentValue(direction!));
        if (!HeidenhainMotion.AddAxes(block, main, axes)
            || HeidenhainMotion.ValueOf(block, radius) is not Value written)
        {
            return;
        }

        // The arc after an expanded rounding ends where the source's does and keeps its centre, with R positive where
        // the rounding leaves a half turn or less of it (D58; language 4.3, R).
        HeidenhainCorners.CompleteEnd(block, main);
        Value value = HeidenhainCorners.RadiusAfterCorner(block, written);
        main.Add("R", value);
        HeidenhainPoint? end = state.Position();
        bool ccw = direction == "CCW";
        HeidenhainPoint? center = start is not null && end is not null && HeidenhainNumbers.NumberOf(value) is decimal r
            ? RadiusCenter(start, end, r, ccw)
            : null;
        EndArc(block, center, ccw);
    }

    /// <summary>
    /// Reads CT X Y, the arc that continues the contour element before it tangentially, converted to an ARC about its
    /// centre, computed from the start point, the direction the element before ended in and the end point (controllers
    /// heidenhain.md 2; phase 3, P3-05; controller-mapping 2, ANGLE row: CT converted to a CENTER arc).
    /// </summary>
    /// <param name="block">The block being read, whose first word is CT.</param>
    public static void ReadTangentArc(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        HeidenhainState state = block.Heidenhain;
        HeidenhainPoint? start = state.Position();
        HeidenhainPoint? tangent = state.Tangent;
        HeidenhainMotion.ReadFeed(block, canBeRapid: false);
        HeidenhainMotion.ReadCompensation(block);
        if (!state.PlaneAxes(out string first, out string second, out string tool) || start is null
            || tangent is null)
        {
            block.Draft.KeepAsRaw("CT continues the direction of the contour element before it from a known point, "
                + "which the reader does not know here (controllers heidenhain.md 2)");
            return;
        }

        List<SourceWord> axes = HeidenhainMotion.AxisWords(block, [first, second, tool]);
        var main = new HeidenhainDraftBlock();
        if (!HeidenhainMotion.AddAxes(block, main, axes) || state.Position() is not HeidenhainPoint end)
        {
            block.Draft.KeepAsRaw("CT needs an end point the reader knows");
            return;
        }

        // The circle through the start point, tangent there to the direction t, and through the end point: its centre
        // lies on the normal n of t, start + s n with s = |d|^2 / (2 n.d), d = end - start; on the left for s > 0, CCW.
        double length = Math.Sqrt(Math.Pow((double)tangent.First, 2) + Math.Pow((double)tangent.Second, 2));
        double normalX = -(double)tangent.Second / length;
        double normalY = (double)tangent.First / length;
        double distanceX = (double)(end.First - start.First);
        double distanceY = (double)(end.Second - start.Second);
        double across = (normalX * distanceX) + (normalY * distanceY);
        if (Math.Abs(across) < Straight)
        {
            block.Draft.KeepAsRaw("the end of the CT lies on its tangent, so the arc is a straight line");
            return;
        }

        double scale = ((distanceX * distanceX) + (distanceY * distanceY)) / (2 * across);
        int decimals = Decimals(axes);
        var center = new HeidenhainPoint(
            HeidenhainNumbers.Round((double)start.First + (scale * normalX), decimals),
            HeidenhainNumbers.Round((double)start.Second + (scale * normalY), decimals));
        bool ccw = scale > 0;
        HeidenhainDraftBlock arc = block.Draft.Main.WithVerb("ARC", new IdentValue(ccw ? "CCW" : "CW"));
        arc.Words.AddRange(main.Words);
        arc.Add("CENTER", first, HeidenhainNumbers.Of(center.First))
            .Add("CENTER", second, HeidenhainNumbers.Of(center.Second));
        EndArc(block, center, ccw);
    }

    /// <summary>
    /// Reads CP PA+90 DR+ and CP IPA+737.956 IZ-5.4 DR+, the arc about the pole with the radius of the current
    /// position: converted to Cartesian, and beyond 360 degrees of IPA the ARC with ANGLE, the helix of TopSolid
    /// (controllers heidenhain.md 2, 7 rule 5; language 4.3, ANGLE; D84).
    /// </summary>
    /// <param name="block">The block being read, whose first word is CP.</param>
    public static void ReadPolarArc(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        HeidenhainState state = block.Heidenhain;
        HeidenhainPoint? start = state.Position();
        if (!Begin(block, out string first, out string second, out string tool, out string? direction))
        {
            return;
        }

        SourceWord? angle = block.Take("PA") ?? block.Take("IPA");
        if (state.Pole is not HeidenhainPoint pole || start is null || angle?.Number is not decimal sweep)
        {
            block.Draft.KeepAsRaw("CP needs the pole of a CC, the current position and its angle PA or IPA as a number "
                + "(controllers heidenhain.md 2)");
            return;
        }

        bool ccw = direction == "CCW";
        List<SourceWord> axes = HeidenhainMotion.AxisWords(block, [tool]);
        HeidenhainDraftBlock main = block.Draft.Main.WithVerb("ARC", new IdentValue(direction!));
        bool incremental = angle.Address == "IPA";
        if (incremental && sweep != 0 && (sweep > 0) != ccw)
        {
            block.Draft.KeepAsRaw("the sign of IPA and the direction DR of the CP disagree");
            return;
        }

        // Beyond a full turn the sweep is the ANGLE of the arc in the direction of its verb, unsigned (language 4.3;
        // D84); otherwise the end point is converted to Cartesian (heidenhain 7 rule 5).
        if (incremental && Math.Abs(sweep) > 360m)
        {
            main.Add("ANGLE", HeidenhainNumbers.Of(HeidenhainCorners.SweepAfterCorner(block, Math.Abs(sweep))));
        }

        if (!PolarPoint(block, null, angle, out decimal x, out decimal y)
            || !HeidenhainMotion.AddAxes(block, main, axes))
        {
            return;
        }

        if (!main.Has("ANGLE", null))
        {
            main.Add(first, HeidenhainNumbers.Of(x)).Add(second, HeidenhainNumbers.Of(y));
        }

        main.Add("CENTER", first, HeidenhainNumbers.Of(pole.First))
            .Add("CENTER", second, HeidenhainNumbers.Of(pole.Second));
        block.State.SetPosition(first, x);
        block.State.SetPosition(second, y);
        EndArc(block, pole, ccw);
    }

    /// <summary>
    /// The Cartesian point of a polar coordinate about the pole: PR or IPR the radius, PA or IPA the angle from the
    /// first axis of the plane, a missing one and the incremental forms taken from the current position (controllers
    /// heidenhain.md 2, 7 rule 5); false, with the block kept RAW, when the reader cannot compute it.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="radius">The PR or IPR word; null to keep the radius of the current position.</param>
    /// <param name="angle">The PA or IPA word; null to keep the angle of the current position.</param>
    /// <param name="x">The coordinate of the first axis of the plane.</param>
    /// <param name="y">The coordinate of the second axis of the plane.</param>
    public static bool PolarPoint(HeidenhainBlock block, SourceWord? radius, SourceWord? angle, out decimal x,
        out decimal y)
    {
        // Up to the element after a chamfer or a rounding the current position is the corner point
        // (HeidenhainCorners.CurrentPosition).
        HeidenhainState state = block.Heidenhain;
        if (PolarPoint(state.Pole!, state.PoleDecimals, HeidenhainCorners.CurrentPosition(block), radius, angle, out x,
            out y))
        {
            return true;
        }

        block.Draft.KeepAsRaw("a polar coordinate is converted to Cartesian from numbers and, for IPR, IPA or a "
            + "missing PR or PA, from the current position (controllers heidenhain.md 7 rule 5)");
        return false;
    }

    /// <summary>
    /// The Cartesian point of a polar coordinate about a pole, as PolarPoint of a block computes it; false where a word
    /// is no number or the current position it needs is not known.
    /// </summary>
    /// <param name="pole">The pole.</param>
    /// <param name="poleDecimals">The decimals of the source words the pole was taken from.</param>
    /// <param name="current">The current position; null where it is not known.</param>
    /// <param name="radius">The PR or IPR word; null to keep the radius of the current position.</param>
    /// <param name="angle">The PA or IPA word; null to keep the angle of the current position.</param>
    /// <param name="x">The coordinate of the first axis of the plane.</param>
    /// <param name="y">The coordinate of the second axis of the plane.</param>
    public static bool PolarPoint(HeidenhainPoint pole, int poleDecimals, HeidenhainPoint? current, SourceWord? radius,
        SourceWord? angle, out decimal x, out decimal y)
    {
        x = 0;
        y = 0;
        bool needsCurrent = radius is null || angle is null || radius.Address == "IPR" || angle.Address == "IPA";
        if ((radius is not null && radius.Number is null) || (angle is not null && angle.Number is null)
            || (needsCurrent && current is null))
        {
            return false;
        }

        double currentRadius = current is null ? 0 : Distance(pole, current);
        double currentAngle = current is null
            ? 0
            : Math.Atan2((double)(current.Second - pole.Second), (double)(current.First - pole.First)) * 180 / Math.PI;
        double r = radius is null ? currentRadius : (double)radius.Number!.Value;
        r += radius?.Address == "IPR" ? currentRadius : 0;
        double a = angle is null ? currentAngle : (double)angle.Number!.Value;
        a += angle?.Address == "IPA" ? currentAngle : 0;
        var words = new List<SourceWord>();
        foreach (SourceWord? word in new[] { radius, angle })
        {
            if (word is not null)
            {
                words.Add(word);
            }
        }

        int decimals = Math.Max(poleDecimals, Decimals(words));
        x = HeidenhainNumbers.Round((double)pole.First + (r * Math.Cos(a * Math.PI / 180)), decimals);
        y = HeidenhainNumbers.Round((double)pole.Second + (r * Math.Sin(a * Math.PI / 180)), decimals);
        return true;
    }

    // The words every arc reads the same way: its direction DR+ or DR-, the feed, the compensation, and a known plane.
    private static bool Begin(HeidenhainBlock block, out string first, out string second, out string tool,
        out string? direction)
    {
        SourceWord? rotation = block.Take("DR");
        direction = rotation?.Text switch
        {
            "+" => "CCW",
            "-" => "CW",
            _ => null,
        };
        HeidenhainMotion.ReadFeed(block, canBeRapid: false);
        HeidenhainMotion.ReadCompensation(block);
        bool plane = block.Heidenhain.PlaneAxes(out first, out second, out tool);
        if (direction is null || !plane)
        {
            block.Draft.KeepAsRaw("an arc needs its direction DR+ or DR- in a known working plane (controllers "
                + "heidenhain.md 2)");
        }

        return !block.Draft.IsRaw;
    }

    // An arc ends in the direction perpendicular to its radius at the end point, turned with the arc, which a CT after
    // it continues (controllers heidenhain.md 2).
    private static void EndArc(HeidenhainBlock block, HeidenhainPoint? center, bool ccw)
    {
        HeidenhainPoint? end = block.Heidenhain.Position();
        if (center is null || end is null)
        {
            block.Heidenhain.Tangent = null;
            return;
        }

        decimal radialX = end.First - center.First;
        decimal radialY = end.Second - center.Second;
        HeidenhainPoint along = ccw ? new HeidenhainPoint(-radialY, radialX) : new HeidenhainPoint(radialY, -radialX);
        block.Heidenhain.Tangent = radialX == 0 && radialY == 0 ? null : along;
    }

    /// <summary>
    /// The centre of the arc of radius r from start to end in its direction, as the virtual machine computes it
    /// (virtual machine 3.2): the midpoint plus h along the left normal, on the left for CCW with r > 0 and CW with
    /// r &lt; 0; null where the arc ends where it starts.
    /// </summary>
    /// <param name="start">Where the arc starts.</param>
    /// <param name="end">Where the arc ends.</param>
    /// <param name="r">The radius, negative for more than a half turn (language 4.3, R).</param>
    /// <param name="ccw">True for an arc counterclockwise.</param>
    public static HeidenhainPoint? RadiusCenter(HeidenhainPoint start, HeidenhainPoint end, decimal r, bool ccw)
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
        return new HeidenhainPoint(HeidenhainNumbers.Round(x, 6), HeidenhainNumbers.Round(y, 6));
    }

    private static double Distance(HeidenhainPoint first, HeidenhainPoint second)
    {
        double dx = (double)(second.First - first.First);
        double dy = (double)(second.Second - first.Second);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // Computed coordinates keep the decimals of the source words they come from, at least three (wave-1 question #46).
    private static int Decimals(IReadOnlyList<SourceWord> words)
    {
        int decimals = HeidenhainNumbers.LeastDecimals;
        foreach (SourceWord word in words)
        {
            decimals = Math.Max(decimals, HeidenhainNumbers.DecimalsOf(word));
        }

        return decimals;
    }
}
