namespace Ncx.Readers.Siemens;

/// <summary>
/// The polar coordinates of a SINUMERIK program (controllers siemens.md 3; controller-mapping 2, ANGLE; D84): G110,
/// G111 and G112 define the pole relative to the last position, to the workpiece zero or to the last pole, with the
/// axes of the plane or with AP and RP; a motion with AP= RP= ends at the point the angle and the radius give about the
/// pole, converted to Cartesian, and an arc with them turns about the pole. The pole stays until the program end.
/// </summary>
internal static class SiemensPolar
{
    /// <summary>
    /// Reads a block that defines the pole; it writes no block of its own, since the motions after it carry its point.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void ReadPole(SiemensBlock block)
    {
        string? code = null;
        foreach (string candidate in new[] { "G110", "G111", "G112" })
        {
            if (block.TakeCode(candidate))
            {
                code = candidate;
            }
        }

        if (code is null || block.Draft.IsRaw)
        {
            return;
        }

        SiemensFacts facts = block.Facts;
        var plane = SiemensPlane.Of(facts.WorkingPlane);
        SiemensPoint? reference = code switch
        {
            "G110" => plane.PointOf(block.State.Positions),
            "G111" => new SiemensPoint(0, 0),
            _ => facts.IsUnknown(SiemensFacts.Pole) ? null : facts.PolePoint,
        };
        SiemensPoint? offset = Offset(block, plane);
        if (reference is not SiemensPoint from || offset is not SiemensPoint by)
        {
            block.Draft.KeepAsRaw($"{code} defines its pole from a point the reader does not know, or with values that "
                + "are no numbers (controllers siemens.md 3)");
            facts.MakeUnknown(SiemensFacts.Pole);
            return;
        }

        facts.PolePoint = new SiemensPoint(from.First + by.First, from.Second + by.Second);
        facts.Unknown.Remove(SiemensFacts.Pole);
    }

    /// <summary>
    /// Reads AP and RP of a motion block into the Cartesian end point in the plane, and the pole as the center of an
    /// arc; false, with the block kept RAW, where the reader cannot convert them.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The motion block.</param>
    /// <param name="plane">The working plane.</param>
    /// <param name="axes">The axis words of the block.</param>
    public static bool Read(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, List<SourceWord> axes)
    {
        SiemensFacts facts = block.Facts;
        SourceWord? angleWord = block.Take("AP");
        SourceWord? radiusWord = block.Take("RP");
        foreach (SourceWord word in axes)
        {
            if (SiemensAxes.AxisOf(block, word) is string axis && (axis == plane.First || axis == plane.Second))
            {
                block.Draft.KeepAsRaw("a block gives its end point in the plane with AP and RP or with the axes, "
                    + "not both");
                return false;
            }
        }

        SiemensPoint? current = plane.PointOf(block.State.Positions);
        if (facts.IsUnknown(SiemensFacts.Pole) || facts.PolePoint is not SiemensPoint pole)
        {
            block.Draft.KeepAsRaw("AP and RP need the pole of G110, G111 or G112, which the reader does not know here");
            return false;
        }

        double? currentRadius = current is SiemensPoint here ? Distance(pole, here) : null;
        double? currentAngle = current is SiemensPoint point
            ? Math.Atan2((double)(point.Second - pole.Second), (double)(point.First - pole.First)) * 180 / Math.PI
            : null;
        double? angle = Coordinate(block, angleWord, currentAngle);
        double? radius = Coordinate(block, radiusWord, currentRadius);
        if (angle is not double a || radius is not double r)
        {
            block.Draft.KeepAsRaw("AP and RP are converted from numbers and, where one is left out or incremental, "
                + "from "
                + "the current position (controllers siemens.md 3)");
            return false;
        }

        int decimals = Decimals(angleWord, radiusWord);
        decimal x = SiemensNumbers.Round((double)pole.First + (r * Math.Cos(a * Math.PI / 180)), decimals);
        decimal y = SiemensNumbers.Round((double)pole.Second + (r * Math.Sin(a * Math.PI / 180)), decimals);
        main.Add(plane.First, SiemensNumbers.Of(x)).Add(plane.Second, SiemensNumbers.Of(y));
        if (facts.MotionCode is "G2" or "G3")
        {
            main.Add("CENTER", plane.First, SiemensNumbers.Of(pole.First))
                .Add("CENTER", plane.Second, SiemensNumbers.Of(pole.Second));
        }

        block.State.SetPosition(plane.First, x);
        block.State.SetPosition(plane.Second, y);
        return true;
    }

    // The offset of the pole from its reference: the axes of the plane, or AP and RP; a pole word left out is 0.
    private static SiemensPoint? Offset(SiemensBlock block, SiemensPlane plane)
    {
        SourceWord? angle = block.Take("AP");
        SourceWord? radius = block.Take("RP");
        if (angle is not null || radius is not null)
        {
            if (angle?.Number is not decimal a || radius?.Number is not decimal r)
            {
                return null;
            }

            int decimals = Decimals(angle, radius);
            return new SiemensPoint(
                SiemensNumbers.Round((double)r * Math.Cos((double)a * Math.PI / 180), decimals),
                SiemensNumbers.Round((double)r * Math.Sin((double)a * Math.PI / 180), decimals));
        }

        decimal first = 0;
        decimal second = 0;
        foreach (SourceWord word in block.Unread())
        {
            string? axis = SiemensAxes.AxisOf(block, word);
            if (axis != plane.First && axis != plane.Second)
            {
                continue;
            }

            block.MarkRead(word);
            if (word.Number is not decimal value)
            {
                return null;
            }

            first = axis == plane.First ? value : first;
            second = axis == plane.Second ? value : second;
        }

        return new SiemensPoint(first, second);
    }

    // AP or RP absolute, AP=IC() added to the current value, left out the current value; under G91 without AC or IC
    // the reader does not read it.
    private static double? Coordinate(SiemensBlock block, SourceWord? word, double? current)
    {
        if (word is null)
        {
            return current;
        }

        string text = word.Text;
        bool incremental = false;
        bool explicitForm = false;
        if (SiemensMotion.Inner(text, out string form) is string inner && form is "AC" or "IC")
        {
            incremental = form == "IC";
            explicitForm = true;
            text = inner;
        }

        if (!explicitForm && block.Facts.Incremental)
        {
            return null;
        }

        decimal? value = SiemensNumbers.NumberOf(SiemensNumbers.Parse(text));
        if (value is null)
        {
            return null;
        }

        return incremental ? current + (double)value : (double)value;
    }

    private static double Distance(SiemensPoint first, SiemensPoint second)
    {
        double dx = (double)(second.First - first.First);
        double dy = (double)(second.Second - first.Second);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // Computed coordinates keep the decimals of the source words, at least three (wave-1 question #46).
    private static int Decimals(SourceWord? angle, SourceWord? radius)
    {
        int decimals = SiemensNumbers.LeastDecimals;
        foreach (SourceWord? word in new[] { angle, radius })
        {
            if (word is not null)
            {
                decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(word.Text));
            }
        }

        return decimals;
    }
}
