using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Chamfers and corner roundings attached to a line, ,C2 and ,R4 (controllers fanuc.md 4): the reader expands them into
/// the explicit lines and arc they stand for (D58, language 4.3). The line of the block ends where the chamfer or the
/// rounding begins, and a line or an arc follows to where the line of the next block begins; the next block is read
/// from that point, its incremental plane words less the way the corner went, so that it ends where the source's
/// does.
/// </summary>
internal static class FanucCorners
{
    // Computed points keep the decimals of the source words they come from, at least these (FanucNumbers.Round).
    private const int LeastDecimals = 3;

    /// <summary>
    /// Expands the chamfer or the rounding of a line block, or keeps the block as RAW when the corner cannot be
    /// computed.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The main block with its motion.</param>
    /// <param name="start">The positions before the block moved.</param>
    public static void Read(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start)
    {
        SourceWord? chamfer = block.Take(",C");
        SourceWord? rounding = block.Take(",R");
        if (chamfer is null && rounding is null)
        {
            return;
        }

        string? problem = chamfer is not null && rounding is not null
            ? "a block carries a chamfer ,C or a rounding ,R, not both"
            : Expand(block, main, start, chamfer ?? rounding!, chamfer is not null);
        if (problem is not null)
        {
            block.Draft.KeepAsRaw(problem + " (D58)");
        }
    }

    // The corner between the line of the block and the line of the next block, in the working plane: a chamfer cuts
    // the corner at the given distance along both lines, a rounding is the arc of the given radius tangent to both.
    private static string? Expand(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start,
        SourceWord corner, bool isChamfer)
    {
        if (main.Verb != "LINE")
        {
            return "a chamfer or a rounding after a rapid move or an arc is not expanded";
        }

        if (corner.Number is not decimal size || size <= 0)
        {
            return "the chamfer or the rounding has no size";
        }

        FanucMotion.PlaneAxes(block, out string first, out string second, out _, out _, out _);
        if (block.Fanuc.Polar || FanucMotion.DiameterInPlane(block, first, second))
        {
            return "a chamfer or a rounding in a polar plane or on a diameter axis is not expanded";
        }

        bool knownStart = start.TryGetValue(first, out decimal startFirst)
            & start.TryGetValue(second, out decimal startSecond);
        bool knownCorner = block.State.Positions.TryGetValue(first, out decimal cornerFirst)
            & block.State.Positions.TryGetValue(second, out decimal cornerSecond);
        SourceBlock? next = block.Fanuc.NextBlock(block.Source);
        if (!knownStart || !knownCorner || next is null
            || !NextEnd(block, next, first, second, cornerFirst, cornerSecond, out decimal endFirst,
                out decimal endSecond, out int nextDecimals, out bool nextIncremental))
        {
            return "the corner, its start or the line after it is not known";
        }

        // The expansion stands for the path where the flow runs from the corner into the next line: a block skip may
        // leave that line out, and a jump that enters an incremental line starts it from another point (D58, D5).
        if (next.BlockSkip)
        {
            return "the line after the corner carries a block skip";
        }

        if (nextIncremental && next.Find("N") is SourceWord label
            && block.Fanuc.JumpTargets.Contains(FanucState.LabelOf(label)))
        {
            return "a jump enters the incremental line after the corner";
        }

        double inFirst = (double)(cornerFirst - startFirst);
        double inSecond = (double)(cornerSecond - startSecond);
        double outFirst = (double)(endFirst - cornerFirst);
        double outSecond = (double)(endSecond - cornerSecond);
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

        // A chamfer reaches its size along both lines; a rounding reaches R tan(a / 2) for a turn by the angle a.
        double turn = Math.Acos(Math.Clamp((inFirst * outFirst) + (inSecond * outSecond), -1.0, 1.0));
        if (!isChamfer && turn < 1e-9)
        {
            return null;
        }

        double reach = isChamfer ? (double)size : (double)size * Math.Tan(turn / 2);
        if (double.IsInfinity(reach) || reach > inLength || reach > outLength)
        {
            return "the chamfer or the rounding is longer than a line of the corner";
        }

        int decimals = Math.Max(LeastDecimals, Math.Max(nextDecimals, DecimalsOf(block, corner, first, second)));
        decimal beginFirst = FanucNumbers.Round((double)cornerFirst - (reach * inFirst), decimals);
        decimal beginSecond = FanucNumbers.Round((double)cornerSecond - (reach * inSecond), decimals);
        decimal endOfCornerFirst = FanucNumbers.Round((double)cornerFirst + (reach * outFirst), decimals);
        decimal endOfCornerSecond = FanucNumbers.Round((double)cornerSecond + (reach * outSecond), decimals);

        // The line of the block ends where the corner begins; the corner goes to where the next line begins, a line for
        // a chamfer and an arc of radius R, turning with the corner, for a rounding.
        main.Remove(first);
        main.Remove(second);
        main.Remove("I" + first);
        main.Remove("I" + second);
        main.Add(first, FanucNumbers.Of(beginFirst)).Add(second, FanucNumbers.Of(beginSecond));
        var cornerBlock = new DraftBlock();
        if (isChamfer)
        {
            cornerBlock.WithVerb("LINE");
        }
        else
        {
            bool left = (inFirst * outSecond) - (inSecond * outFirst) > 0;
            cornerBlock.WithVerb("ARC", new IdentValue(left ? "CCW" : "CW"));
        }

        cornerBlock.Add(first, FanucNumbers.Of(endOfCornerFirst)).Add(second, FanucNumbers.Of(endOfCornerSecond));
        if (!isChamfer)
        {
            cornerBlock.Add("R", corner.ToNcxNumber()!);
        }

        block.Draft.After.Insert(0, cornerBlock);
        block.State.SetPosition(first, endOfCornerFirst);
        block.State.SetPosition(second, endOfCornerSecond);

        // The source counts the incremental words of the next line from the corner, NCX from the end of the corner:
        // the next block is written less the way the corner went (FromCornerEnd).
        block.Fanuc.CornerShift.Clear();
        block.Fanuc.CornerLine = nextIncremental ? next.Line : null;
        if (nextIncremental)
        {
            block.Fanuc.CornerShift[first] = endOfCornerFirst - cornerFirst;
            block.Fanuc.CornerShift[second] = endOfCornerSecond - cornerSecond;
        }

        return null;
    }

    /// <summary>
    /// The value of an axis word of the block after an expanded corner: an incremental plane word counts from the
    /// programmed corner in the source and from the end of the corner in NCX, so it is written less the way the corner
    /// went, and the line ends where the source's does (D58, language 4.3); any other word as read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axis">The axis word.</param>
    /// <param name="value">Its value as read.</param>
    public static Value FromCornerEnd(FanucBlock block, FanucAxisWord axis, Value value)
    {
        if (!axis.Incremental || block.Fanuc.CornerLine != block.Line
            || !block.Fanuc.CornerShift.TryGetValue(axis.Axis, out decimal shift) || shift == 0
            || FanucNumbers.NumberOf(value) is not decimal distance)
        {
            return value;
        }

        return FanucNumbers.Of(distance - shift);
    }

    // Where the line of the next block ends in the plane: its words absolute or incremental from the corner, a missing
    // axis unchanged, and whether a word of it is incremental; false when the next block is no line in the plane with
    // numbers.
    private static bool NextEnd(FanucBlock block, SourceBlock next, string first, string second, decimal cornerFirst,
        decimal cornerSecond, out decimal endFirst, out decimal endSecond, out int decimals, out bool incremental)
    {
        endFirst = cornerFirst;
        endSecond = cornerSecond;
        decimals = 0;
        incremental = false;
        bool? incrementalMode = null;
        foreach (SourceWord word in next.Words)
        {
            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (code is "G90" or "G91")
            {
                incrementalMode = code == "G91";
            }
            else if (code is not null && code is not ("G1" or "G40" or "G41" or "G42"))
            {
                return false;
            }
        }

        foreach (SourceWord word in next.Words)
        {
            if (FanucAxes.Of(word, block) is not FanucAxisWord axis)
            {
                continue;
            }

            if ((axis.Axis != first && axis.Axis != second) || word.Number is not decimal value)
            {
                return false;
            }

            bool byAddress = axis.Incremental && !block.Incremental;
            bool relative = byAddress || (incrementalMode ?? axis.Incremental);
            incremental |= relative;
            decimals = Math.Max(decimals, FanucNumbers.DecimalsOf(word));
            if (axis.Axis == first)
            {
                endFirst = relative ? cornerFirst + value : value;
            }
            else
            {
                endSecond = relative ? cornerSecond + value : value;
            }
        }

        return true;
    }

    // The decimals of the plane words of the block and of the corner word.
    private static int DecimalsOf(FanucBlock block, SourceWord corner, string first, string second)
    {
        int decimals = FanucNumbers.DecimalsOf(corner);
        foreach (SourceWord word in block.Source.Words)
        {
            if (FanucAxes.Of(word, block) is FanucAxisWord axis && (axis.Axis == first || axis.Axis == second))
            {
                decimals = Math.Max(decimals, FanucNumbers.DecimalsOf(word));
            }
        }

        return decimals;
    }
}
