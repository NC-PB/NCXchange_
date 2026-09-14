using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The chamfer CHF and the rounding RND between two motion blocks (controllers heidenhain.md 2) have no NCX word: the
/// reader expands them into the explicit LINE and ARC blocks they stand for (D58, language 4.3), between two straight
/// lines, as the Fanuc reader expands ,C and ,R (FanucCorners). The line before the corner ends where the chamfer or the
/// rounding begins, the CHF or RND block writes the chamfer line or the rounding arc to where the line after it begins,
/// and that line is read from the end of the corner, its incremental words less the way the corner went, so that it ends
/// where the source's does. A corner the reader does not expand stays RAW, the lines about it as the source writes them
/// (D5).
/// </summary>
internal static class HeidenhainCorners
{
    // Below this the two lines of a rounding run on in one direction and there is no corner to round, or the line after
    // the corner runs back on the line before it.
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
    /// Prepares the chamfer or the rounding of the CHF or RND block after a line once the line is read: the line ends
    /// where the corner begins, and the corner waits for its block; or the reason the corner stays RAW waits for it.
    /// </summary>
    /// <param name="block">The block read last.</param>
    /// <param name="start">The position in the working plane before the block moved; null where it is not known.</param>
    public static void Prepare(HeidenhainBlock block, HeidenhainPoint? start)
    {
        HeidenhainState state = block.Heidenhain;

        // A corner holds from the line before it through its CHF or RND block to the line after it, which is read by
        // now (FromCornerEnd).
        if (state.Corner is not null && state.Corner.Line != block.Line)
        {
            state.Corner = null;
        }

        SourceBlock? corner = state.NextBlock(block.Source);
        if (corner is null || !IsCorner(corner))
        {
            return;
        }

        string? problem = Expand(block, start, corner);
        if (problem is not null)
        {
            state.Corner = new HeidenhainCorner { Line = corner.Line, Problem = problem };
        }
    }

    /// <summary>
    /// Reads a CHF or RND block: the chamfer line or the rounding arc the line before it prepared, or RAW where the
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
            block.Draft.KeepAsRaw((corner?.Problem ?? "the reader expands a chamfer or a rounding after a line L or LP "
                + "at the feed") + " (D58)");
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
    /// The value of an axis word of the line after an expanded corner: an incremental word counts from the corner point
    /// in the source and from the end of the corner in NCX, so it is written less the way the corner went, and the line
    /// ends where the source's does (D58, language 4.3); any other word as read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The axis word.</param>
    /// <param name="value">Its value as read.</param>
    public static Value FromCornerEnd(HeidenhainBlock block, SourceWord word, Value value)
    {
        // TODO(question): heidenhain 2 gives IX+30 as incremental without saying where the IX of the line after a CHF or
        // RND counts from, the corner point the line before programs or the end of the corner the tool stands at; the
        // reader counts it from the corner point, as the Fanuc reader counts the G91 line after ,C and ,R.
        HeidenhainCorner? corner = block.Heidenhain.Corner;
        if (corner is null || corner.NextLine != block.Line || !word.Address.StartsWith('I')
            || !corner.Shift.TryGetValue(HeidenhainMotion.AxisOf(word), out decimal shift) || shift == 0
            || HeidenhainNumbers.NumberOf(value) is not decimal distance)
        {
            return value;
        }

        return HeidenhainNumbers.Of(distance - shift);
    }

    // The corner between the line of the block and the line after the CHF or RND block, in the working plane: a chamfer
    // cuts it at its length along both lines, a rounding is the arc of radius R tangent to both (D58, language 4.3).
    private static string? Expand(HeidenhainBlock block, HeidenhainPoint? start, SourceBlock corner)
    {
        HeidenhainState state = block.Heidenhain;
        HeidenhainDraftBlock main = block.Draft.Main;
        if (block.Draft.IsRaw)
        {
            return "the block before the chamfer or the rounding is kept RAW";
        }

        // Klartext writes a chamfer or a rounding between two contour elements (controllers heidenhain.md 2); the reader
        // expands it between two straight lines at the feed in the working plane, as the Fanuc reader does, and keeps it
        // RAW next to a rapid move, an arc, a cycle call, a retract or a move in the machine frame.
        if (block.Keyword(0) is not ("L" or "LP") || main.Verb != "LINE" || main.PositionsCall
            || main.Has("FRAME", null) || WritesOtherMotion(block.Draft))
        {
            return "the reader expands a chamfer or a rounding after a line L or LP at the feed, and the block before it "
                + "is none";
        }

        string? sizeProblem = Size(corner, out decimal size, out int sizeDecimals);
        if (sizeProblem is not null)
        {
            return sizeProblem;
        }

        // The expansion stands for the path where the flow runs through the line, the corner and the line after it: a
        // block skip may leave one of them out, and the control then turns another corner (D58, D5).
        SourceBlock? next = state.NextBlock(corner);
        if (block.Source.BlockSkip || corner.BlockSkip || next is null || next.BlockSkip)
        {
            return "a block of the corner carries a block skip, or no block follows it";
        }

        if (!state.PlaneAxes(out string first, out string second, out _) || !InPlane(main, first, second))
        {
            return "a chamfer or a rounding lies in the working plane, and the line before it leaves the plane or the "
                + "plane is not known";
        }

        HeidenhainPoint? cornerPoint = state.Position();
        if (start is null || cornerPoint is null)
        {
            return "the corner or the start of the line before it is not known";
        }

        string? nextProblem = NextEnd(next, first, second, CompensationOf(block.Source), cornerPoint,
            out HeidenhainPoint end, out bool nextIncremental);
        if (nextProblem is not null)
        {
            return nextProblem;
        }

        double inFirst = (double)(cornerPoint.First - start.First);
        double inSecond = (double)(cornerPoint.Second - start.Second);
        double outFirst = (double)(end.First - cornerPoint.First);
        double outSecond = (double)(end.Second - cornerPoint.Second);
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
        double turn = Math.Acos(Math.Clamp((inFirst * outFirst) + (inSecond * outSecond), -1.0, 1.0));
        if (turn > Math.PI - Straight)
        {
            return "the line after the corner runs back on the line before it";
        }

        bool isChamfer = corner.Words[0].Address == "CHF";
        if (!isChamfer && turn < Straight)
        {
            // Two lines in one direction have no corner to round: the rounding writes nothing.
            state.Corner = new HeidenhainCorner
            {
                Line = corner.Line,
                First = first,
                Second = second,
                End = cornerPoint,
                Tangent = state.Tangent,
                NextLine = next.Line,
            };
            return null;
        }

        // A chamfer reaches its length along both lines; a rounding reaches R tan(a / 2) for a turn by the angle a.
        // TODO(question): heidenhain 2 names CHF 2 a chamfer without saying what its length measures, the way along each
        // line from the corner point or the chamfer line itself; the reader takes the way along each line, as the Fanuc
        // reader takes ,C.
        double reach = isChamfer ? (double)size : (double)size * Math.Tan(turn / 2);
        if (reach > inLength || reach > outLength)
        {
            return "the chamfer or the rounding is longer than a line of the corner";
        }

        // Computed points keep the decimals of the numbers they come from, at least three (wave-1 question #46).
        int decimals = Math.Max(HeidenhainNumbers.LeastDecimals, sizeDecimals);
        foreach (HeidenhainPoint point in new[] { start, cornerPoint, end })
        {
            decimals = Math.Max(decimals, Math.Max(point.First.Scale, point.Second.Scale));
        }

        var begin = new HeidenhainPoint(
            HeidenhainNumbers.Round((double)cornerPoint.First - (reach * inFirst), decimals),
            HeidenhainNumbers.Round((double)cornerPoint.Second - (reach * inSecond), decimals));
        var cornerEnd = new HeidenhainPoint(
            HeidenhainNumbers.Round((double)cornerPoint.First + (reach * outFirst), decimals),
            HeidenhainNumbers.Round((double)cornerPoint.Second + (reach * outSecond), decimals));

        // The corner goes to where the line after it begins: a line for a chamfer, an arc of radius R turning with the
        // corner for a rounding, CCW for a turn to the left (language 4.3, ARC and R).
        var drawn = new HeidenhainDraftBlock();
        if (isChamfer)
        {
            drawn.WithVerb("LINE");
        }
        else
        {
            bool left = (inFirst * outSecond) - (inSecond * outFirst) > 0;
            drawn.WithVerb("ARC", new IdentValue(left ? "CCW" : "CW"));
        }

        drawn.Add(first, HeidenhainNumbers.Of(cornerEnd.First)).Add(second, HeidenhainNumbers.Of(cornerEnd.Second));
        if (!isChamfer)
        {
            drawn.Add("R", HeidenhainNumbers.Of(size));
        }

        // The line of the block ends where the corner begins, its end point absolute in the plane.
        main.Remove(first);
        main.Remove(second);
        main.Remove("I" + first);
        main.Remove("I" + second);
        main.Add(first, HeidenhainNumbers.Of(begin.First)).Add(second, HeidenhainNumbers.Of(begin.Second));
        block.State.SetPosition(first, begin.First);
        block.State.SetPosition(second, begin.Second);

        // The source counts the incremental words of the line after the corner from the corner point, NCX from the end
        // of the corner (FromCornerEnd).
        var shift = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (nextIncremental)
        {
            shift[first] = cornerEnd.First - cornerPoint.First;
            shift[second] = cornerEnd.Second - cornerPoint.Second;
        }

        state.Corner = new HeidenhainCorner
        {
            Line = corner.Line,
            Block = drawn,
            First = first,
            Second = second,
            End = cornerEnd,
            Tangent = isChamfer
                ? new HeidenhainPoint(cornerEnd.First - begin.First, cornerEnd.Second - begin.Second)
                : new HeidenhainPoint(end.First - cornerPoint.First, end.Second - cornerPoint.Second),
            NextLine = next.Line,
            Shift = shift,
        };
        return null;
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

            // TODO(question): heidenhain 2 gives the F of an L as the feed that stays and does not say what an F in a
            // CHF or RND block feeds, the corner alone or the lines after it as well; the reader keeps such a block RAW,
            // the lines about it as the source writes them.
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

    // Where the line after the corner ends in the plane, from its words as the source writes them: absolute, or
    // incremental from the corner point, an axis it does not name where the corner is. The reader expands the corner
    // only before a line L at the feed in the working plane with numbers, with the radius compensation of the line
    // before it, and, where it counts incremental words from the corner, without an M function a reader rule could take
    // the block for (D66).
    private static string? NextEnd(SourceBlock next, string first, string second, string? compensation,
        HeidenhainPoint cornerPoint, out HeidenhainPoint end, out bool incremental)
    {
        end = cornerPoint;
        incremental = false;
        if (next.Words.Count == 0 || next.Words[0].Address != "L" || next.Words[0].Text.Length > 0)
        {
            return "the block after the chamfer or the rounding is no line L";
        }

        decimal endFirst = cornerPoint.First;
        decimal endSecond = cornerPoint.Second;
        bool functions = false;
        for (int index = 1; index < next.Words.Count; index++)
        {
            SourceWord word = next.Words[index];
            string axis = HeidenhainMotion.AxisOf(word);
            if ((axis == first || axis == second) && word.Number is decimal value)
            {
                bool relative = word.Address.StartsWith('I');
                incremental |= relative;
                if (axis == first)
                {
                    endFirst = relative ? cornerPoint.First + value : value;
                }
                else
                {
                    endSecond = relative ? cornerPoint.Second + value : value;
                }
            }
            else if (CompensationOf(word) is string written)
            {
                if (written != compensation)
                {
                    return "the line after the corner changes the radius compensation";
                }
            }
            else if (word.Address == "M" && NativeCode.Of(word) is not ("M91" or "M92"))
            {
                functions = true;
            }
            else if (word.Address != "F" || word.Number is null)
            {
                return $"the line after the corner holds {word.Address}{word.Text}, which the reader does not expand a "
                    + "corner before";
            }
        }

        if (incremental && functions)
        {
            return "the incremental line after the corner carries an M function";
        }

        end = new HeidenhainPoint(endFirst, endSecond);
        return null;
    }

    // The line moves in the working plane only, no word of another axis.
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

    // A block the line writes after itself with a motion or a call of its own, the RETRACT of M140 or the call of M99,
    // would stand between the line and the corner.
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

    // R0, RL and RR, the radius compensation of a line (controllers heidenhain.md 2); null for no compensation word.
    private static string? CompensationOf(SourceBlock line)
    {
        foreach (SourceWord word in line.Words)
        {
            if (CompensationOf(word) is string compensation)
            {
                return compensation;
            }
        }

        return null;
    }

    private static string? CompensationOf(SourceWord word)
    {
        return word.Address switch
        {
            "R" when word.Text == "0" => "R0",
            "RL" or "RR" when word.Text.Length == 0 => word.Address,
            _ => null,
        };
    }
}
