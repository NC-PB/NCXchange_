using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The chamfer CHF and the rounding RND between two contour elements (controllers heidenhain.md 2) have no NCX word:
/// the reader expands them into the explicit LINE and ARC blocks they stand for (D58, language 4.3). The element before
/// the corner ends where the chamfer or the rounding begins, the CHF or RND block writes the chamfer line or the
/// rounding arc to where the element after it begins, and that element is read from the end of the corner, so that it
/// ends where the source's does. A chamfer cuts a corner between two lines; a rounding rounds a corner between two
/// lines, a line and an arc, or two arcs (HeidenhainRounding). A corner the reader does not expand stays RAW, the
/// elements about it as the source writes them (D5).
/// </summary>
internal static class HeidenhainCorners
{
    // Below this two directions are one: the elements of a rounding run on in one direction, or the element after the
    // corner runs back on the element before it; and a point is where another is.
    private const double Straight = 1e-9;

    // The axis addresses of a motion block (controllers heidenhain.md 2).
    private static readonly string[] s_axes = ["X", "Y", "Z", "A", "B", "C", "U", "V", "W"];

    /// <summary>
    /// Tells whether a block is a chamfer CHF or a rounding RND (controllers heidenhain.md 2).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsCorner(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address is "CHF" or "RND";
    }

    /// <summary>
    /// Tells whether a block is a CC, which sets the pole and is no contour element (controllers heidenhain.md 2).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsPole(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address == "CC" && block.Words[0].Text.Length == 0;
    }

    /// <summary>
    /// Prepares the chamfer or the rounding of the CHF or RND block after a contour element once the element is read:
    /// the element ends where the corner begins, and the corner waits for its block; or the reason the corner stays RAW
    /// waits for it. A CC may stand between the element and the corner.
    /// </summary>
    /// <param name="block">The block read last.</param>
    /// <param name="start">The position in the working plane before the block moved; null where it is not
    /// known.</param>
    public static void Prepare(HeidenhainBlock block, HeidenhainPoint? start)
    {
        HeidenhainState state = block.Heidenhain;

        // A corner holds from the element before it through its CHF or RND block to the element after it, which is read
        // by now (CurrentPosition, FromCornerEnd).
        if (state.Corner is not null && block.Line >= state.Corner.NextLine)
        {
            state.Corner = null;
        }

        // A CC between the element and the CHF or RND block sets the pole of the element after the corner; the element
        // before the corner has prepared it by the time the CC is read.
        SourceBlock? corner = state.NextBlock(block.Source);
        while (corner is not null && IsPole(corner))
        {
            corner = state.NextBlock(corner);
        }

        if (corner is null || !IsCorner(corner) || state.Corner?.Line == corner.Line)
        {
            return;
        }

        string? problem = Expand(block, start, corner);
        if (problem is not null)
        {
            state.Corner = new HeidenhainCorner { Line = corner.Line, NextLine = corner.Line, Problem = problem };
        }
    }

    /// <summary>
    /// Reads a CHF or RND block: the chamfer line or the rounding arc the element before it prepared, or RAW where the
    /// reader does not expand the corner (D58, D5).
    /// </summary>
    /// <param name="block">The block being read, whose first word is CHF or RND.</param>
    public static void ReadCorner(HeidenhainBlock block)
    {
        block.MarkAllRead();
        HeidenhainState state = block.Heidenhain;
        HeidenhainCorner? corner = state.Corner is not null && state.Corner.Line == block.Line ? state.Corner : null;
        if (corner is null || corner.Problem is not null || corner.End is not HeidenhainPoint end)
        {
            state.Corner = null;
            block.Draft.KeepAsRaw((corner?.Problem ?? "a chamfer or a rounding stands between two contour elements, "
                + "and the reader has read none before it") + " (D58)");
            return;
        }

        HeidenhainDraftBlock? drawn = corner.Block;
        if (drawn is not null && drawn.Verb is string verb)
        {
            block.Draft.Main.WithVerb(verb, drawn.VerbValue).Words.AddRange(drawn.Words);
        }

        block.State.SetPosition(corner.First, end.First);
        block.State.SetPosition(corner.Second, end.Second);
        state.Tangent = corner.Tangent;
    }

    /// <summary>
    /// The position the blocks from an expanded corner up to the element after it count from: the corner point, where
    /// the source stands, not the end of the corner, where NCX stands; the current position otherwise. A CC or a polar
    /// coordinate between the element before the corner and the CHF or RND block counts from the end of that element,
    /// which is the corner point.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static HeidenhainPoint? CurrentPosition(HeidenhainBlock block)
    {
        // TODO(question): D255, heidenhain 2 does not say where a coordinate after a CHF or RND counts from, the corner
        // point the element before programs or the end of the corner the tool stands at; the reader counts from the
        // corner point, the incremental words (FromCornerEnd), a CC alone or by IX and IY, and a polar coordinate that
        // takes the current position, as the Fanuc reader counts the G91 line after ,C and ,R, until D255 is answered.
        HeidenhainCorner? corner = block.Heidenhain.Corner;
        return corner is { Problem: null, Point: HeidenhainPoint point } && block.Line <= corner.NextLine
            ? point
            : block.Heidenhain.Position();
    }

    /// <summary>
    /// The value of an axis word of the element after an expanded corner: an incremental word counts from the corner
    /// point in the source and from the end of the corner in NCX, so it is written less the way the corner went, and
    /// the element ends where the source's does (D58, language 4.3; D255); any other word as read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The axis word.</param>
    /// <param name="value">Its value as read.</param>
    public static Value FromCornerEnd(HeidenhainBlock block, SourceWord word, Value value)
    {
        HeidenhainCorner? corner = After(block);
        if (corner is null || !word.Address.StartsWith('I')
            || !corner.Shift.TryGetValue(HeidenhainMotion.AxisOf(word), out decimal shift) || shift == 0
            || HeidenhainNumbers.NumberOf(value) is not decimal distance)
        {
            return value;
        }

        return HeidenhainNumbers.Of(distance - shift);
    }

    /// <summary>
    /// The arc after an expanded corner names both axes of the plane at its end: an axis the source leaves out stays at
    /// the corner point, where NCX would keep it at the end of the corner (D58; D255).
    /// </summary>
    /// <param name="block">The block being read, a C or a CR.</param>
    /// <param name="main">The ARC block it reads into.</param>
    public static void CompleteEnd(HeidenhainBlock block, HeidenhainDraftBlock main)
    {
        if (After(block) is not HeidenhainCorner corner || corner.Point is not HeidenhainPoint point)
        {
            return;
        }

        foreach (string axis in new[] { corner.First, corner.Second })
        {
            if (!main.Has(axis, null) && !main.Has("I" + axis, null))
            {
                decimal value = axis == corner.First ? point.First : point.Second;
                main.Add(axis, HeidenhainNumbers.Of(value));
                block.State.SetPosition(axis, value);
            }
        }
    }

    /// <summary>
    /// The radius R of the CR after an expanded rounding: the arc loses its start to the rounding and may keep a half
    /// turn or less of more, and its R turns positive (language 4.3, R; D58).
    /// </summary>
    /// <param name="block">The block being read, a CR.</param>
    /// <param name="radius">The radius as read.</param>
    public static Value RadiusAfterCorner(HeidenhainBlock block, Value radius)
    {
        return After(block) is { NextRadiusTurns: true } && HeidenhainNumbers.NumberOf(radius) is decimal number
            ? HeidenhainNumbers.Of(Math.Abs(number))
            : radius;
    }

    /// <summary>
    /// The sweep ANGLE of the CP IPA after an expanded rounding, less the part of the arc the rounding took (D84, D58).
    /// </summary>
    /// <param name="block">The block being read, a CP.</param>
    /// <param name="sweep">The sweep in degrees as read.</param>
    public static decimal SweepAfterCorner(HeidenhainBlock block, decimal sweep)
    {
        return After(block) is HeidenhainCorner corner ? sweep - corner.NextSweepTrim : sweep;
    }

    // The corner between the element of the block and the element after the CHF or RND block, in the working plane: a
    // chamfer cuts it at its length along both lines, a rounding is the arc of radius R tangent to both elements (D58,
    // language 4.3).
    private static string? Expand(HeidenhainBlock block, HeidenhainPoint? start, SourceBlock corner)
    {
        HeidenhainState state = block.Heidenhain;
        if (block.Draft.IsRaw)
        {
            return "the block before the chamfer or the rounding is kept RAW";
        }

        if (!state.PlaneAxes(out string first, out string second, out _))
        {
            return "a chamfer or a rounding lies in the working plane, and the plane is not known";
        }

        HeidenhainElement? before = ElementBefore(block, start, first, second, out string? problem);
        if (before is null)
        {
            return problem;
        }

        problem = Size(corner, out decimal size, out int sizeDecimals);
        if (problem is not null)
        {
            return problem;
        }

        HeidenhainElement? after = HeidenhainNextElement.Read(block, corner, state.Position()!, out SourceBlock? next,
            out problem);
        if (after is null || next is null)
        {
            return problem;
        }

        // TODO(question): heidenhain 2 places CHF between two motion blocks and language 4.3 has the readers expand it,
        // but no document says what a chamfer next to an arc stands for, where its length is measured along the arc and
        // so where the chamfer line begins and ends (wave-2 question #88 asks what it measures along a line); the
        // reader keeps it RAW, the elements about it as the source writes them.
        bool isChamfer = corner.Words[0].Address == "CHF";
        if (isChamfer && (before.IsArc || after.IsArc))
        {
            return "no document says what a chamfer next to an arc measures";
        }

        if ((!before.IsArc && before.Length == 0) || (!after.IsArc && after.Length == 0))
        {
            return "a line of the corner has no length";
        }

        // Computed points keep the decimals of the numbers they come from, at least three (wave-1 question #46).
        int decimals = Math.Max(Math.Max(HeidenhainNumbers.LeastDecimals, sizeDecimals),
            Math.Max(before.Decimals, after.Decimals));
        return before.IsArc || after.IsArc
            ? NextToArc(block, before, after, corner, next, size, decimals)
            : BetweenLines(block, before, after, corner, next, isChamfer, size, decimals);
    }

    // The contour element the block reads into, in the plane: a line L or LP at the feed or at FMAX from where it
    // starts to the corner point, or an arc C, CR, CT or CP; null, with the reason, where the reader does not expand a
    // corner after the block: a cycle call, a retract or a move in the machine frame in it, a move out of the plane, a
    // point the reader does not know.
    private static HeidenhainElement? ElementBefore(HeidenhainBlock block, HeidenhainPoint? start, string first,
        string second, out string? problem)
    {
        HeidenhainDraftBlock main = block.Draft.Main;
        string keyword = block.Keyword(0);
        bool straight = keyword is "L" or "LP" && main.Verb is "LINE" or "RAPID";
        bool arc = keyword is "C" or "CR" or "CT" or "CP" && main.Verb == "ARC";
        if ((!straight && !arc) || main.PositionsCall || main.Has("FRAME", null) || WritesOtherMotion(block.Draft))
        {
            problem = "the reader expands a chamfer or a rounding after a contour element L, LP, C, CR, CT or CP, and "
                + "the block before it is none, or calls a cycle, retracts or moves in the machine frame";
            return null;
        }

        if (!InPlane(main, first, second))
        {
            problem = "a chamfer or a rounding lies in the working plane, and the element before it leaves the plane";
            return null;
        }

        HeidenhainPoint? point = block.Heidenhain.Position();
        if (start is null || point is null)
        {
            problem = "the corner or the start of the element before it is not known";
            return null;
        }

        problem = null;
        var element = new HeidenhainElement
        {
            Start = HeidenhainVector.Of(start),
            End = HeidenhainVector.Of(point),
            Decimals = Math.Max(Math.Max(start.First.Scale, start.Second.Scale),
                Math.Max(point.First.Scale, point.Second.Scale)),
        };
        return straight ? element : ArcBefore(main, element, start, point, first, second, out problem);
    }

    // The arc the block reads into: C, CT and CP about the CENTER they write, CR about the centre its radius gives from
    // where it starts (virtual machine 3.2), over its sweep from there to the corner point or its ANGLE of more than a
    // turn (D84).
    // TODO(question): D122, an arc that ends where it starts is a full circle or nothing; the reader keeps a corner
    // next to it RAW.
    private static HeidenhainElement? ArcBefore(HeidenhainDraftBlock main, HeidenhainElement element,
        HeidenhainPoint start, HeidenhainPoint point, string first, string second, out string? problem)
    {
        bool ccw = main.VerbValue is IdentValue { Name: "CCW" };
        decimal? radius = HeidenhainNumbers.NumberOf(main.Find("R", null));
        decimal? angle = HeidenhainNumbers.NumberOf(main.Find("ANGLE", null));
        HeidenhainPoint? center = null;
        if (HeidenhainNumbers.NumberOf(main.Find("CENTER", first)) is decimal x
            && HeidenhainNumbers.NumberOf(main.Find("CENTER", second)) is decimal y)
        {
            center = new HeidenhainPoint(x, y);
        }
        else if (radius is decimal r)
        {
            center = HeidenhainArcs.RadiusCenter(start, point, r, ccw);
        }

        HeidenhainVector middle = center is null ? element.End : HeidenhainVector.Of(center);
        double sweep = angle is decimal degrees
            ? (double)degrees * Math.PI / 180
            : HeidenhainElement.SweepBetween(middle, element.Start, element.End, ccw);
        if (center is null || sweep < Straight || element.End.Minus(middle).Length < Straight)
        {
            problem = "the arc before the corner has no centre the reader knows, or ends where it starts (D122)";
            return null;
        }

        problem = null;
        return element with
        {
            Center = middle,
            Counterclockwise = ccw,
            Radius = element.End.Minus(middle).Length,
            Sweep = sweep,
            WrittenRadius = radius,
            WritesAngle = angle is not null,
        };
    }

    // Between two lines a chamfer reaches its length along both from the corner point and a rounding reaches
    // R tan(a / 2) for a turn by the angle a (D58, language 4.3).
    private static string? BetweenLines(HeidenhainBlock block, HeidenhainElement before, HeidenhainElement after,
        SourceBlock corner, SourceBlock next, bool isChamfer, decimal size, int decimals)
    {
        HeidenhainVector inward = before.DirectionAt(before.End);
        HeidenhainVector outward = after.DirectionAt(after.Start);
        double turn = Math.Acos(Math.Clamp(inward.Dot(outward), -1.0, 1.0));
        if (turn > Math.PI - Straight)
        {
            return "the line after the corner runs back on the line before it";
        }

        if (!isChamfer && turn < Straight)
        {
            // Two lines in one direction have no corner to round: the rounding writes nothing.
            WriteNothing(block, corner, next);
            return null;
        }

        // TODO(question): D255, heidenhain 2 names CHF 2 a chamfer without saying what its length measures, the way
        // along each line from the corner point or the chamfer line itself; the reader takes the way along each line,
        // as the Fanuc reader takes ,C, until D255 is answered.
        double reach = isChamfer ? (double)size : (double)size * Math.Tan(turn / 2);
        if (reach > before.Length || reach > after.Length)
        {
            return "the chamfer or the rounding is longer than a line of the corner";
        }

        HeidenhainPoint begin = before.End.Minus(inward.Times(reach)).Round(decimals);
        HeidenhainPoint end = after.Start.Plus(outward.Times(reach)).Round(decimals);

        // The corner goes to where the line after it begins: a line for a chamfer, an arc of radius R turning with the
        // corner for a rounding, CCW for a turn to the left (language 4.3, ARC and R).
        var drawn = new HeidenhainDraftBlock();
        if (isChamfer)
        {
            drawn.WithVerb("LINE");
        }
        else
        {
            drawn.WithVerb("ARC", new IdentValue(inward.Cross(outward) > 0 ? "CCW" : "CW"))
                .Add("R", HeidenhainNumbers.Of(size));
        }

        HeidenhainPoint tangent = isChamfer
            ? new HeidenhainPoint(end.First - begin.First, end.Second - begin.Second)
            : Direction(outward);
        Finish(block, before, after, corner, next, drawn, begin, end, tangent, decimals);
        return null;
    }

    // Next to an arc the rounding touches both elements where HeidenhainRounding fits it: the element before ends there
    // and the element after starts there, an arc about its own centre (D58, language 4.3).
    private static string? NextToArc(HeidenhainBlock block, HeidenhainElement before, HeidenhainElement after,
        SourceBlock corner, SourceBlock next, decimal size, int decimals)
    {
        HeidenhainVector inward = before.DirectionAt(before.End);
        HeidenhainVector outward = after.DirectionAt(after.Start);
        double turn = inward.Cross(outward);
        if (Math.Abs(turn) < Straight)
        {
            if (inward.Dot(outward) < 0)
            {
                return "the element after the corner runs back on the element before it";
            }

            // An arc that goes on from the element before it in its direction leaves no corner to round.
            WriteNothing(block, corner, next);
            return null;
        }

        bool left = turn > 0;
        string? problem = HeidenhainRounding.Fit(before, after, (double)size, left, out HeidenhainVector center,
            out HeidenhainVector touchBefore, out HeidenhainVector touchAfter);
        if (problem is not null)
        {
            return problem;
        }

        // An arc the rounding leaves nothing of, once its points are rounded, would end where it starts (D122).
        HeidenhainPoint begin = touchBefore.Round(decimals);
        HeidenhainPoint end = touchAfter.Round(decimals);
        if ((before.IsArc && HeidenhainVector.Of(begin).Minus(before.Start).Length < Straight)
            || (after.IsArc && HeidenhainVector.Of(end).Minus(after.End).Length < Straight))
        {
            return "the rounding leaves nothing of an arc of the corner (D122)";
        }

        // The rounding goes from where it touches the element before to where it touches the element after, turning
        // with the corner; more than a half turn has a negative R (language 4.3, R).
        double sweep = HeidenhainElement.SweepBetween(center, touchBefore, touchAfter, left);
        HeidenhainDraftBlock drawn = new HeidenhainDraftBlock().WithVerb("ARC", new IdentValue(left ? "CCW" : "CW"))
            .Add("R", HeidenhainNumbers.Of(sweep <= Math.PI ? size : -size));
        Finish(block, before, after, corner, next, drawn, begin, end, Direction(after.DirectionAt(touchAfter)),
            decimals);
        return null;
    }

    // The element of the block ends where the corner begins, the corner waits for its block with the chamfer line or
    // the rounding arc to where the element after it begins, and that element is read from there: its incremental words
    // less the way the corner went (FromCornerEnd), an arc by its radius or its sweep with what the rounding leaves of
    // it (RadiusAfterCorner, SweepAfterCorner).
    private static void Finish(HeidenhainBlock block, HeidenhainElement before, HeidenhainElement after,
        SourceBlock corner, SourceBlock next, HeidenhainDraftBlock drawn, HeidenhainPoint begin, HeidenhainPoint end,
        HeidenhainPoint tangent, int decimals)
    {
        HeidenhainState state = block.Heidenhain;
        state.PlaneAxes(out string first, out string second, out _);
        HeidenhainPoint point = state.Position()!;
        drawn.Add(first, HeidenhainNumbers.Of(end.First)).Add(second, HeidenhainNumbers.Of(end.Second));
        Shorten(block, before, first, second, begin, decimals);

        var shift = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (after.Incremental)
        {
            shift[first] = end.First - point.First;
            shift[second] = end.Second - point.Second;
        }

        double taken = after.IsArc ? after.FromStart(HeidenhainVector.Of(end)) : 0;
        state.Corner = new HeidenhainCorner
        {
            Line = corner.Line,
            Block = drawn,
            First = first,
            Second = second,
            End = end,
            Point = point,
            Tangent = tangent,
            NextLine = next.Line,
            Shift = shift,
            NextRadiusTurns = after.WrittenRadius < 0 && after.Sweep - taken <= Math.PI,
            NextSweepTrim = after.WritesAngle ? Degrees(taken, decimals) : 0,
        };
    }

    // The element of the block ends where the corner begins: its end point absolute in the plane, or for an arc of more
    // than a turn its sweep ANGLE less the part the corner took (D84); an arc by its radius left with a half turn or
    // less of more has a positive R (language 4.3, R).
    private static void Shorten(HeidenhainBlock block, HeidenhainElement before, string first, string second,
        HeidenhainPoint begin, int decimals)
    {
        HeidenhainDraftBlock main = block.Draft.Main;
        double taken = before.IsArc ? before.ToEnd(HeidenhainVector.Of(begin)) : 0;
        if (before.WritesAngle && HeidenhainNumbers.NumberOf(main.Find("ANGLE", null)) is decimal angle)
        {
            main.Remove("ANGLE");
            main.Add("ANGLE", HeidenhainNumbers.Of(angle - Degrees(taken, decimals)));
        }
        else
        {
            main.Remove(first);
            main.Remove(second);
            main.Remove("I" + first);
            main.Remove("I" + second);
            main.Add(first, HeidenhainNumbers.Of(begin.First)).Add(second, HeidenhainNumbers.Of(begin.Second));
        }

        if (before.WrittenRadius is decimal radius && radius < 0 && before.Sweep - taken <= Math.PI)
        {
            main.Remove("R");
            main.Add("R", HeidenhainNumbers.Of(-radius));
        }

        block.State.SetPosition(first, begin.First);
        block.State.SetPosition(second, begin.Second);
    }

    // Two elements that run on in one direction have no corner to round: the rounding writes nothing, and the element
    // after it starts at the corner point.
    private static void WriteNothing(HeidenhainBlock block, SourceBlock corner, SourceBlock next)
    {
        HeidenhainState state = block.Heidenhain;
        state.PlaneAxes(out string first, out string second, out _);
        HeidenhainPoint point = state.Position()!;
        state.Corner = new HeidenhainCorner
        {
            Line = corner.Line,
            First = first,
            Second = second,
            End = point,
            Point = point,
            Tangent = state.Tangent,
            NextLine = next.Line,
        };
    }

    // The length of a CHF or the radius of an RND: CHF 2, RND R4, and RND 4 as heidenhain 2 writes it; a word beyond it
    // keeps the corner RAW.
    private static string? Size(SourceBlock corner, out decimal size, out int decimals)
    {
        size = 0;
        decimals = 0;
        SourceWord head = corner.Words[0];
        SourceWord? given = head.Text.Length > 0 ? head : null;
        for (int index = 1; index < corner.Words.Count; index++)
        {
            SourceWord word = corner.Words[index];

            // TODO(question): D255, heidenhain 2 gives the F of an L as the feed that stays and does not say what an F
            // in a CHF or RND block feeds, the corner alone or the lines after it as well; the reader keeps such a
            // block RAW, the elements about it as the source writes them, until D255 is answered.
            if (word.Address == "F")
            {
                return "a CHF or RND block with a feed F of its own is not expanded";
            }

            bool isSize = word.Address.Length == 0 || (word.Address == "R" && head.Address == "RND");
            if (given is not null || !isSize)
            {
                return $"{word.Address}{word.Text} has no place in a {head.Address} block";
            }

            given = word;
        }

        if (given?.Number is not decimal number || number <= 0)
        {
            return "the chamfer or the rounding has no size";
        }

        size = number;
        decimals = HeidenhainNumbers.DecimalsOf(given);
        return null;
    }

    // The expanded corner whose element after it the block is; null for any other block.
    private static HeidenhainCorner? After(HeidenhainBlock block)
    {
        HeidenhainCorner? corner = block.Heidenhain.Corner;
        return corner is { Problem: null } && corner.NextLine == block.Line ? corner : null;
    }

    // The element moves in the working plane only, no word of another axis.
    private static bool InPlane(HeidenhainDraftBlock main, string first, string second)
    {
        foreach (Word word in main.Words)
        {
            string axis = word.Key.StartsWith('I') ? word.Key.Substring(1) : word.Key;
            if (word.Addr is null && s_axes.Contains(axis) && axis != first && axis != second)
            {
                return false;
            }
        }

        return true;
    }

    // A block the element writes after itself with a motion or a call of its own, the RETRACT of M140 or the call of
    // M99, would stand between the element and the corner.
    private static bool WritesOtherMotion(HeidenhainDraft draft)
    {
        foreach (HeidenhainDraftBlock other in draft.InOrder())
        {
            if (!ReferenceEquals(other, draft.Main) && other.Verb is not null)
            {
                return true;
            }
        }

        return false;
    }

    // A direction of the geometry as the reader keeps it for a CT (HeidenhainState.Tangent).
    private static HeidenhainPoint Direction(HeidenhainVector direction)
    {
        return new HeidenhainPoint((decimal)direction.First, (decimal)direction.Second);
    }

    // An angle of the geometry in degrees, with the decimals of the corner.
    private static decimal Degrees(double radians, int decimals)
    {
        return HeidenhainNumbers.Round(radians * 180 / Math.PI, decimals);
    }
}
