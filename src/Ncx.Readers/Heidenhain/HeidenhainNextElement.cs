namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The contour element after a chamfer or a rounding, read ahead from its source words before the reader reads its
/// block, so that the element before the corner can end where the corner begins (controllers heidenhain.md 2; D58): L
/// to its end point, LP to its polar point about the pole, C about the pole, CR by its radius, CP about the pole, each
/// in the working plane with numbers, and the CC blocks on both sides of the corner that set the pole. An incremental
/// word, a CC alone or by IX and IY, and a polar coordinate that takes the current position count from the corner
/// point, as the reader then reads them (HeidenhainCorners.CurrentPosition and FromCornerEnd; wave-2 question #90).
/// </summary>
internal static class HeidenhainNextElement
{
    private const string Skipped = "a block of the corner carries a block skip";

    // The axis addresses of a motion block (controllers heidenhain.md 2).
    private static readonly string[] s_axes = ["X", "Y", "Z", "A", "B", "C", "U", "V", "W"];

    /// <summary>
    /// Reads the element after a corner ahead: the CHF or RND block and the CC blocks from the block before the corner
    /// up to the element, then the element; null, with the reason, where the reader does not expand the corner.
    /// </summary>
    /// <param name="block">The block before the corner, read.</param>
    /// <param name="corner">The CHF or RND block.</param>
    /// <param name="point">The corner point, where the element before the corner ends.</param>
    /// <param name="next">The block of the element after the corner; null where none follows.</param>
    /// <param name="problem">Why the reader does not expand the corner; null for an element it reads.</param>
    public static HeidenhainElement? Read(HeidenhainBlock block, SourceBlock corner, HeidenhainPoint point,
        out SourceBlock? next, out string? problem)
    {
        HeidenhainState state = block.Heidenhain;
        state.PlaneAxes(out string first, out string second, out _);
        HeidenhainPoint? pole = state.Pole;
        int poleDecimals = state.PoleDecimals;

        // The expansion stands for the path where the flow runs through the element before the corner, the corner and
        // the element after it: a block skip may leave one of them out, and the control then turns another corner (D58,
        // D5). A CC on either side of the corner sets the pole of the element after it.
        problem = block.Source.BlockSkip ? Skipped : null;
        next = state.NextBlock(block.Source);
        while (problem is null && next is not null
            && (ReferenceEquals(next, corner) || HeidenhainCorners.IsPole(next)))
        {
            if (next.BlockSkip)
            {
                problem = Skipped;
            }
            else if (HeidenhainCorners.IsPole(next))
            {
                pole = PoleOf(next, first, second, point, out poleDecimals, out problem);
            }

            next = state.NextBlock(next);
        }

        if (problem is not null || next is null)
        {
            problem ??= "no contour element follows the chamfer or the rounding";
            return null;
        }

        if (next.BlockSkip)
        {
            problem = Skipped;
            return null;
        }

        return ElementOf(block, next, first, second, point, pole, poleDecimals, out problem);
    }

    // The pole a CC sets as the reader reads it (HeidenhainArcs.PoleOf), counted from the corner point; a CC with a
    // word beyond the axes of the plane the reader keeps RAW, and the corner with it.
    private static HeidenhainPoint? PoleOf(SourceBlock pole, string first, string second, HeidenhainPoint point,
        out int decimals, out string? problem)
    {
        var words = new List<SourceWord>();
        for (int index = 1; index < pole.Words.Count; index++)
        {
            SourceWord word = pole.Words[index];
            string axis = HeidenhainMotion.AxisOf(word);
            if (word.Text.Length == 0 || (axis != first && axis != second))
            {
                decimals = 0;
                problem = $"the CC of the corner holds {word.Address}{word.Text}, which the reader keeps RAW";
                return null;
            }

            words.Add(word);
        }

        problem = null;
        return HeidenhainArcs.PoleOf(words, first, point, out decimals);
    }

    // The element of the block after the corner from its words, as the reader will read it: its end point absolute or
    // incremental from the corner point, or its polar point about the pole; the centre of C and CP the pole, of CR the
    // one its radius gives. Null, with the reason, where the reader does not expand the corner before it.
    private static HeidenhainElement? ElementOf(HeidenhainBlock block, SourceBlock next, string first, string second,
        HeidenhainPoint point, HeidenhainPoint? pole, int poleDecimals, out string? problem)
    {
        string kind = next.Words[0].Text.Length == 0 ? next.Words[0].Address : "";

        // TODO(question): CT continues the contour element before it tangentially (controllers heidenhain.md 2), and
        // after a CHF or RND that element is the chamfer or the rounding, whose end depends on the CT itself; no
        // document says what a CT after a chamfer or a rounding is tangent to. The reader keeps the corner RAW, and the
        // CT after it, whose direction it then does not know.
        if (kind == "CT")
        {
            problem = "no document says what a CT after a chamfer or a rounding is tangent to";
            return null;
        }

        if (kind is not ("L" or "LP" or "C" or "CR" or "CP"))
        {
            problem = "the block after the chamfer or the rounding is no contour element L, LP, C, CR or CP";
            return null;
        }

        bool arc = kind is "C" or "CR" or "CP";
        bool polar = kind is "LP" or "CP";
        decimal endFirst = point.First;
        decimal endSecond = point.Second;
        bool incremental = false;
        bool rapid = false;
        bool feed = false;
        bool? counterclockwise = null;
        SourceWord? radius = null;
        SourceWord? polarRadius = null;
        SourceWord? polarAngle = null;
        problem = null;
        for (int index = 1; index < next.Words.Count && problem is null; index++)
        {
            SourceWord word = next.Words[index];
            string axis = HeidenhainMotion.AxisOf(word);
            if (word.Text.Length > 0 && s_axes.Contains(axis))
            {
                // The end point in the plane of L, C and CR, absolute or incremental from the corner point.
                if (polar || (axis != first && axis != second) || word.Number is not decimal value)
                {
                    problem = "the element after the corner leaves the working plane or names its end by an expression";
                }
                else if (axis == first)
                {
                    incremental |= word.Address.StartsWith('I');
                    endFirst = word.Address.StartsWith('I') ? point.First + value : value;
                }
                else
                {
                    incremental |= word.Address.StartsWith('I');
                    endSecond = word.Address.StartsWith('I') ? point.Second + value : value;
                }
            }
            else if (arc && counterclockwise is null && word.Address == "DR" && word.Text is "+" or "-")
            {
                counterclockwise = word.Text == "+";
            }
            else if (kind == "CR" && radius is null && word.Address == "R" && word.Number is decimal size && size != 0)
            {
                radius = word;
            }
            else if (polar && polarAngle is null && word.Address is "PA" or "IPA" && word.Number is not null)
            {
                polarAngle = word;
            }
            else if (kind == "LP" && polarRadius is null && word.Address is "PR" or "IPR" && word.Number is not null)
            {
                polarRadius = word;
            }
            else if (!arc && word.Address == "FMAX" && word.Text.Length == 0)
            {
                rapid = true;
            }
            else if (word.Address == "F" && word.Number is not null)
            {
                feed = true;
            }
            else if (!Allowed(block, next, word))
            {
                problem = $"the element after the corner holds {word.Address}{word.Text}, which the reader does not "
                    + "expand a corner before";
            }
        }

        if (problem is null && rapid && feed)
        {
            problem = "the element after the corner moves at FMAX and at F";
        }
        else if (problem is null && arc && counterclockwise is null)
        {
            problem = "the arc after the corner has no direction DR+ or DR-";
        }
        else if (problem is null && kind != "L" && kind != "CR" && pole is null)
        {
            problem = "the element after the corner needs the pole of a CC, which the reader does not know";
        }

        if (problem is not null)
        {
            return null;
        }

        bool ccw = counterclockwise == true;
        var end = new HeidenhainPoint(endFirst, endSecond);
        return kind switch
        {
            "L" => Line(point, end, incremental),
            "LP" => PolarLine(point, pole!, poleDecimals, polarRadius, polarAngle, out problem),
            "C" => Arc(point, end, pole!, ccw, incremental, null, out problem),
            "CR" => RadiusArc(point, end, radius, ccw, incremental, out problem),
            _ => PolarArc(point, pole!, poleDecimals, polarAngle, ccw, out problem),
        };
    }

    // The words the element after a corner may carry besides its geometry: R0, RL and RR, since the corner keeps the
    // compensation of the element before it, and an M function the reader reads into the element's NCX block. An M
    // function that keeps the element RAW keeps the corner RAW too: the element as the source writes it counts from the
    // corner point, which an expanded corner does not reach (D58, D5). That is M140 in a block that moves, M128 with a
    // feed, M91 and M92, a table state NCX writes with a value (HeidenhainFunctions.ReadsWithMotion), and M99 where
    // its call stays RAW (HeidenhainCycles.WritesCall). So does an M function the machine's tables leave to a reader
    // rule, since the rule may take the block and write it otherwise than it is read here (D66).
    private static bool Allowed(HeidenhainBlock block, SourceBlock next, SourceWord word)
    {
        return word.Address switch
        {
            "R" => word.Text == "0",
            "RL" or "RR" => word.Text.Length == 0,
            "M" when NativeCode.Of(word) == "M99" => HeidenhainCycles.WritesCall(block.Heidenhain),
            "M" => HeidenhainFunctions.ReadsWithMotion(next, word, block.Templates),
            _ => false,
        };
    }

    private static HeidenhainElement Line(HeidenhainPoint point, HeidenhainPoint end, bool incremental)
    {
        return new HeidenhainElement
        {
            Start = HeidenhainVector.Of(point),
            End = HeidenhainVector.Of(end),
            Incremental = incremental,
            Decimals = Math.Max(end.First.Scale, end.Second.Scale),
        };
    }

    // LP ends at its polar point about the pole, a missing or incremental PR or PA taken from the corner point
    // (heidenhain 7 rule 5; HeidenhainArcs.PolarPoint).
    private static HeidenhainElement? PolarLine(HeidenhainPoint point, HeidenhainPoint pole, int poleDecimals,
        SourceWord? radius, SourceWord? angle, out string? problem)
    {
        problem = null;
        if (!HeidenhainArcs.PolarPoint(pole, poleDecimals, point, radius, angle, out decimal x, out decimal y))
        {
            problem = "the polar point of the LP after the corner is not known";
            return null;
        }

        return Line(point, new HeidenhainPoint(x, y), false);
    }

    // CR about the centre its radius gives from the corner point to its end, as the virtual machine computes it
    // (virtual machine 3.2; HeidenhainArcs.RadiusCenter).
    private static HeidenhainElement? RadiusArc(HeidenhainPoint point, HeidenhainPoint end, SourceWord? radius,
        bool ccw, bool incremental, out string? problem)
    {
        HeidenhainPoint? center = radius?.Number is decimal r ? HeidenhainArcs.RadiusCenter(point, end, r, ccw) : null;
        double chord = HeidenhainVector.Of(end).Minus(HeidenhainVector.Of(point)).Length;
        if (center is null || chord > (2 * Math.Abs((double)radius!.Number!.Value)) + 1e-9)
        {
            problem = "the CR after the corner needs its radius R, and a radius that reaches its end";
            return null;
        }

        return Arc(point, end, center, ccw, incremental, radius.Number, out problem);
    }

    // CP about the pole with the radius of the corner point, to its polar angle PA or by its sweep IPA; beyond 360
    // degrees the sweep is the ANGLE of the arc (D84; HeidenhainArcs.ReadPolarArc).
    private static HeidenhainElement? PolarArc(HeidenhainPoint point, HeidenhainPoint pole, int poleDecimals,
        SourceWord? angle, bool ccw, out string? problem)
    {
        decimal sweep = angle?.Number ?? 0;
        bool incremental = angle?.Address == "IPA";
        if (angle is null || (incremental && sweep != 0 && (sweep > 0) != ccw)
            || !HeidenhainArcs.PolarPoint(pole, poleDecimals, point, null, angle, out decimal x, out decimal y))
        {
            problem = "the CP after the corner needs its angle PA or IPA in its direction";
            return null;
        }

        HeidenhainElement? element = Arc(point, new HeidenhainPoint(x, y), pole, ccw, false, null, out problem);
        return incremental && Math.Abs(sweep) > 360m && element is not null
            ? element with { Sweep = (double)Math.Abs(sweep) * Math.PI / 180, WritesAngle = true }
            : element;
    }

    // An arc about its centre from the corner point to its end in its direction.
    // TODO(question): D122, an arc that ends where it starts is a full circle or nothing; the reader keeps a corner
    // next to it RAW.
    private static HeidenhainElement? Arc(HeidenhainPoint point, HeidenhainPoint end, HeidenhainPoint center, bool ccw,
        bool incremental, decimal? radius, out string? problem)
    {
        HeidenhainVector start = HeidenhainVector.Of(point);
        HeidenhainVector middle = HeidenhainVector.Of(center);
        double sweep = HeidenhainElement.SweepBetween(middle, start, HeidenhainVector.Of(end), ccw);
        if (sweep < 1e-9 || start.Minus(middle).Length < 1e-9)
        {
            problem = "the arc after the corner ends where it starts (D122)";
            return null;
        }

        problem = null;
        return new HeidenhainElement
        {
            Start = start,
            End = HeidenhainVector.Of(end),
            Center = middle,
            Counterclockwise = ccw,
            Radius = start.Minus(middle).Length,
            Sweep = sweep,
            WrittenRadius = radius,
            Incremental = incremental,
            Decimals = Math.Max(end.First.Scale, end.Second.Scale),
        };
    }
}
