using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The chamfers, roundings and contour angles of a SINUMERIK line (controllers siemens.md 3; controller-mapping 2;
/// language 4.3; D58): CHF=2 a chamfer of that length, CHR=2 a chamfer of that width along each line, RND=4 a rounding
/// of that radius, RNDM=4 the same at every following corner until RNDM=0, with the feed FRC= of one corner or FRCM=
/// of all; ANG= a line by its angle, alone or in two blocks whose lines meet. The reader expands them into the LINE
/// and ARC blocks they stand for: the line ends where the corner begins, a line or an arc follows to where the next
/// line begins. A corner the reader cannot compute keeps its block RAW (D5).
/// </summary>
internal static class SiemensCorners
{
    /// <summary>
    /// Reads the corner words of a motion block after its axis words.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The motion block.</param>
    /// <param name="plane">The working plane.</param>
    /// <param name="start">The positions before the block moved.</param>
    public static void Read(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        SiemensFacts facts = block.Facts;
        ReadModal(block);
        Value? cornerFeed = block.Take("FRC") is SourceWord feed ? Number(block, feed) : facts.ModalCornerFeed;
        ReadAngle(block, main, plane, start);
        SourceWord? chamfer = block.Take("CHF");
        SourceWord? width = block.Take("CHR");
        SourceWord? rounding = block.Take("RND");
        if (block.Draft.IsRaw)
        {
            return;
        }

        int count = (chamfer is null ? 0 : 1) + (width is null ? 0 : 1) + (rounding is null ? 0 : 1);
        if (count > 1)
        {
            block.Draft.KeepAsRaw("a block carries one chamfer CHF or CHR or one rounding RND (D58)");
            return;
        }

        SourceWord? corner = chamfer ?? width ?? rounding;
        decimal? size = corner?.Number;
        bool isRounding = rounding is not null;
        if (corner is null)
        {
            // RNDM rounds every corner between two lines until RNDM=0 (controllers siemens.md 3).
            if (facts.RoundingRadius is not decimal modal || main.Verb != "LINE" || NextLine(block) is null)
            {
                return;
            }

            size = modal;
            isRounding = true;
        }

        if (size is not decimal amount || amount <= 0)
        {
            block.Draft.KeepAsRaw("the chamfer or the rounding has no size as a number (D58)");
            return;
        }

        string? problem = Expand(block, main, plane, start, amount, isRounding, chamfer is not null, cornerFeed);
        if (problem is not null)
        {
            block.Draft.KeepAsRaw(problem + " (D58)");
        }
    }

    /// <summary>
    /// RNDM=4 rounds the corners after it, RNDM=0 ends it; FRCM= feeds them (controllers siemens.md 3). A block that
    /// only switches them writes nothing: the reader expands the corners (D58).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void ReadModal(SiemensBlock block)
    {
        SiemensFacts facts = block.Facts;
        if (block.Take("RNDM") is SourceWord modal)
        {
            if (modal.Number is not decimal radius)
            {
                block.Draft.KeepAsRaw("RNDM rounds with a radius that is no number (D58)");
                return;
            }

            facts.RoundingRadius = radius == 0 ? null : radius;
            facts.Unknown.Remove(SiemensFacts.Rounding);
        }

        if (block.Take("FRCM") is SourceWord feed)
        {
            facts.ModalCornerFeed = feed.Number == 0 ? null : Number(block, feed);
        }
    }

    // The corner between the line of the block and the line of the next block: a chamfer cuts it at the given
    // distance along both lines, CHF by the length of the chamfer itself, a rounding is the arc of the given radius
    // tangent to both.
    private static string? Expand(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        Dictionary<string, decimal> start, decimal size, bool isRounding, bool byLength, Value? feed)
    {
        if (main.Verb != "LINE")
        {
            return "a chamfer or a rounding after a rapid move or an arc is not expanded";
        }

        if (block.Facts.DiameterMode != "DIAMOF" && (plane.First == "X" || plane.Second == "X"))
        {
            return "a chamfer or a rounding on a diameter axis is not expanded";
        }

        SiemensPoint? from = plane.PointOf(start);
        SiemensPoint? at = plane.PointOf(block.State.Positions);
        SourceBlock? next = NextLine(block);
        if (from is not SiemensPoint a || at is not SiemensPoint c || next is null
            || NextEnd(block, next, plane, c) is not SiemensPoint e)
        {
            return "the corner, its start or an absolute line after it in the plane is not known";
        }

        double inFirst = (double)(c.First - a.First);
        double inSecond = (double)(c.Second - a.Second);
        double outFirst = (double)(e.First - c.First);
        double outSecond = (double)(e.Second - c.Second);
        double inLength = Math.Sqrt((inFirst * inFirst) + (inSecond * inSecond));
        double outLength = Math.Sqrt((outFirst * outFirst) + (outSecond * outSecond));
        if (inLength == 0 || outLength == 0)
        {
            return "a line of the corner has no length";
        }

        inFirst /= inLength;
        inSecond /= inLength;
        outFirst /= outLength;
        outSecond /= outLength;

        // A chamfer of width w reaches w along both lines, one of length L reaches L / (2 cos(a / 2)) for a turn by the
        // angle a; a rounding reaches R tan(a / 2).
        double turn = Math.Acos(Math.Clamp((inFirst * outFirst) + (inSecond * outSecond), -1.0, 1.0));
        if (turn < 1e-9)
        {
            return isRounding ? null : "a chamfer between two lines of one direction is no corner";
        }

        double reach = isRounding ? (double)size * Math.Tan(turn / 2)
            : byLength ? (double)size / (2 * Math.Cos(turn / 2)) : (double)size;
        if (double.IsInfinity(reach) || double.IsNaN(reach) || reach > inLength || reach > outLength)
        {
            return "the chamfer or the rounding is longer than a line of the corner";
        }

        int decimals = Decimals(block, next);
        decimal beginFirst = SiemensNumbers.Round((double)c.First - (reach * inFirst), decimals);
        decimal beginSecond = SiemensNumbers.Round((double)c.Second - (reach * inSecond), decimals);
        decimal endFirst = SiemensNumbers.Round((double)c.First + (reach * outFirst), decimals);
        decimal endSecond = SiemensNumbers.Round((double)c.Second + (reach * outSecond), decimals);
        main.Remove(plane.First);
        main.Remove(plane.Second);
        main.Remove("I" + plane.First);
        main.Remove("I" + plane.Second);
        main.Add(plane.First, SiemensNumbers.Of(beginFirst)).Add(plane.Second, SiemensNumbers.Of(beginSecond));
        var cornerBlock = new SiemensDraftBlock();
        if (isRounding)
        {
            bool left = (inFirst * outSecond) - (inSecond * outFirst) > 0;
            cornerBlock.WithVerb("ARC", new IdentValue(left ? "CCW" : "CW"));
        }
        else
        {
            cornerBlock.WithVerb("LINE");
        }

        cornerBlock.Add(plane.First, SiemensNumbers.Of(endFirst)).Add(plane.Second, SiemensNumbers.Of(endSecond));
        if (isRounding)
        {
            cornerBlock.Add("R", SiemensNumbers.Of(size));
        }

        // FRC feeds the corner, the feed of the path returns with the next line (controller-mapping 2, F).
        if (feed is not null)
        {
            cornerBlock.Add("F", feed);
            block.Facts.WrittenFeed = feed;
        }

        block.Draft.After.Insert(0, cornerBlock);
        block.State.SetPosition(plane.First, endFirst);
        block.State.SetPosition(plane.Second, endSecond);
        block.Facts.Tangent = new SiemensPoint((decimal)outFirst, (decimal)outSecond);
        return null;
    }

    // ANG=a gives a line by its angle to the first axis of the plane: with one end coordinate the other follows from
    // the start point; with none, the next block gives its end point and its own angle, and this line ends where the
    // two lines meet (controllers siemens.md 3; controller-mapping 2; D58).
    private static void ReadAngle(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane,
        Dictionary<string, decimal> start)
    {
        SourceWord? angleWord = block.Take("ANG");
        if (block.Siemens.AngleSecondLine == block.Line)
        {
            block.Siemens.AngleSecondLine = null;
            return;
        }

        if (angleWord is null)
        {
            return;
        }

        SiemensPoint? from = plane.PointOf(start);
        bool first = main.Has(plane.First, null) || main.Has("I" + plane.First, null);
        bool second = main.Has(plane.Second, null) || main.Has("I" + plane.Second, null);
        if (main.Verb is not ("LINE" or "RAPID") || angleWord.Number is not decimal angle || from is not SiemensPoint a
            || (first && second))
        {
            block.Draft.KeepAsRaw("ANG gives a line from a known start point by its angle and at most one end "
                + "coordinate (controllers siemens.md 3; D58)");
            return;
        }

        double radians = (double)angle * Math.PI / 180;
        int decimals = Decimals(block, null);
        if (first || second)
        {
            OneBlock(block, main, plane, a, first, radians, decimals);
            return;
        }

        TwoBlocks(block, main, plane, a, radians, decimals);
    }

    // One end coordinate and the angle: the other coordinate of the point on the line (D58).
    private static void OneBlock(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, SiemensPoint a,
        bool first, double radians, int decimals)
    {
        double along = first ? Math.Cos(radians) : Math.Sin(radians);
        if (Math.Abs(along) < 1e-9 || (first ? block.State.Positions.ContainsKey(plane.First)
            : block.State.Positions.ContainsKey(plane.Second)) is false)
        {
            block.Draft.KeepAsRaw("ANG gives a line along the axis its end coordinate names, or from an end the reader "
                + "does not know (D58)");
            return;
        }

        decimal known = first ? block.State.Positions[plane.First] : block.State.Positions[plane.Second];
        double t = (double)(known - (first ? a.First : a.Second)) / along;
        decimal other = SiemensNumbers.Round((double)(first ? a.Second : a.First)
            + (t * (first ? Math.Sin(radians) : Math.Cos(radians))), decimals);
        string axis = first ? plane.Second : plane.First;
        main.Remove(axis);
        main.Remove("I" + axis);
        main.Add(axis, SiemensNumbers.Of(other));
        block.State.SetPosition(axis, other);
    }

    // No end coordinate: the next block carries both and its own ANG; the line ends where the two lines meet.
    private static void TwoBlocks(SiemensBlock block, SiemensDraftBlock main, SiemensPlane plane, SiemensPoint a,
        double radians, int decimals)
    {
        SourceBlock? next = block.Siemens.NextBlock(block.Source);
        SourceWord? secondAngle = next?.Find("ANG");
        SiemensPoint? e = next is null ? null : NextEnd(block, next, plane, null);
        if (next is null || secondAngle?.Number is not decimal angle2 || e is not SiemensPoint end)
        {
            block.Draft.KeepAsRaw("ANG without an end coordinate needs the next block with its end point and its own "
                + "ANG (controllers siemens.md 3; D58)");
            return;
        }

        double b = (double)angle2 * Math.PI / 180;
        double d1x = Math.Cos(radians);
        double d1y = Math.Sin(radians);
        double d2x = Math.Cos(b);
        double d2y = Math.Sin(b);
        double cross = (d1x * d2y) - (d1y * d2x);
        if (Math.Abs(cross) < 1e-9)
        {
            block.Draft.KeepAsRaw("the two lines of the ANG contour are parallel (D58)");
            return;
        }

        double t = (((double)(end.First - a.First) * d2y) - ((double)(end.Second - a.Second) * d2x)) / cross;
        decimal x = SiemensNumbers.Round((double)a.First + (t * d1x), decimals);
        decimal y = SiemensNumbers.Round((double)a.Second + (t * d1y), decimals);
        main.Add(plane.First, SiemensNumbers.Of(x)).Add(plane.Second, SiemensNumbers.Of(y));
        block.State.SetPosition(plane.First, x);
        block.State.SetPosition(plane.Second, y);
        block.Siemens.AngleSecondLine = next.Line;
    }

    // The next block with words is a line in the plane: no other code of group 1, no block skip, no label a jump
    // enters; null otherwise.
    private static SourceBlock? NextLine(SiemensBlock block)
    {
        SourceBlock? next = block.Siemens.Units.NextInUnit(block.Source);
        if (next is null || next.BlockSkip)
        {
            return null;
        }

        SiemensUnit? unit = block.Unit;
        if (unit is not null && ((SiemensUnits.LabelOf(next) is string label && unit.Targets.Contains(label))
            || (SiemensUnits.NumberLabelOf(next) is string number && unit.Targets.Contains(number))))
        {
            return null;
        }

        bool line = block.Facts.MotionCode == "G1";
        foreach (SourceWord word in next.Words)
        {
            string? command = SiemensGroups.CommandOf(word);
            if (command is not null && SiemensGroups.GroupOf(command) == SiemensGroups.Motion)
            {
                line = command == "G1";
            }
        }

        return line ? next : null;
    }

    // Where the line of the next block ends in the plane: its words absolute, a missing axis at the corner; null when
    // a word is incremental, an expression, or moves another axis.
    private static SiemensPoint? NextEnd(SiemensBlock block, SourceBlock next, SiemensPlane plane, SiemensPoint? corner)
    {
        bool incremental = block.Facts.Incremental;
        decimal? first = corner?.First;
        decimal? second = corner?.Second;
        foreach (SourceWord word in next.Words)
        {
            string? code = SiemensBlock.CodeOf(word);
            incremental = code == "G91" || (code != "G90" && incremental);
            if (word.Address is "AP" or "RP")
            {
                return null;
            }

            string? axis = word.Text.Length > 0 && !word.Text.StartsWith('(') ? SiemensAxes.AxisOf(block, word) : null;
            if (axis is null)
            {
                continue;
            }

            string text = word.Text;
            bool absolute = !incremental;
            if (SiemensMotion.Inner(text, out string form) is string inner && form is "AC" or "IC")
            {
                absolute = form == "AC";
                text = inner;
            }

            decimal? value = SiemensNumbers.NumberOf(SiemensNumbers.Parse(text));
            if (!absolute || value is null || (axis != plane.First && axis != plane.Second))
            {
                return null;
            }

            first = axis == plane.First ? value : first;
            second = axis == plane.Second ? value : second;
        }

        return first is decimal x && second is decimal y ? new SiemensPoint(x, y) : null;
    }

    private static Value? Number(SiemensBlock block, SourceWord word)
    {
        Value? value = SiemensExpression.ValueOf(block, word.Text, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}={word.Text} has no NCX value: {problem}");
        }

        return value;
    }

    // Computed points keep the decimals of the source words they come from, at least three (wave-1 question #46).
    private static int Decimals(SiemensBlock block, SourceBlock? next)
    {
        int decimals = SiemensNumbers.LeastDecimals;
        SourceBlock[] sources = next is null ? [block.Source] : [block.Source, next];
        foreach (SourceBlock source in sources)
        {
            foreach (SourceWord word in source.Words)
            {
                if (word.Number is not null)
                {
                    decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(word.Text));
                }
            }
        }

        return decimals;
    }
}
