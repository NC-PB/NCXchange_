using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The tilted working plane of G68.2 (controllers fanuc.md 4; controller-mapping 1, TILT, MOVE; language 4.2; D82):
/// G68.2 X Y Z I J K with Euler angles (P0) or roll, pitch and yaw (P1) is a SHIFT to its origin and a TILT with the
/// spatial angles A B C, entries of the chain (FanucChain); a G53.1 in the next block positions the rotary axes,
/// MOVE=TURN; other P forms, and G68.1, stay RAW.
/// </summary>
internal static class FanucTilt
{
    // Computed angles keep the decimals of the source angles, at least these (FanucNumbers.Round).
    private const int LeastDecimals = 3;

    // Below this the cosine of B is 0, and A and C turn about the same axis.
    private const double GimbalLimit = 1e-9;

    /// <summary>
    /// Reads G68.2, G53.1 and G68.1 of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        // TODO(question): controller-mapping 1 reads G68.1 with G53.1 as TILT_AXIS (D82), and G68.1 turns the
        // coordinate system about an axis (fanuc 4) while TILT_AXIS takes the rotary axis positions of the machine; the
        // documents do not say how the words of G68.1 become the axis angles. G68.1 stays RAW.
        if (block.TakeCode("G68.1"))
        {
            block.Draft.KeepAsRaw("G68.1, the 3D coordinate conversion, is kept as RAW");
            return;
        }

        // G53.1 after G68.2 positions the rotary axes to the plane: MOVE=TURN on the TILT (controller-mapping 1).
        if (block.FindCode("G53.1") is SourceWord turn)
        {
            block.MarkRead(turn);
            if (!block.Fanuc.TurnsOfTilts.Contains(block.Line))
            {
                block.Draft.KeepAsRaw(
                    "G53.1 positions the rotary axes to the G68.2 of the block before it, and there is none");
                return;
            }
        }

        if (block.TakeCode("G68.2"))
        {
            ReadTilt(block);
        }
    }

    // TODO(question): fanuc 4 does not say what a G68.2 does while a tilted plane is active, a new plane or one in the
    // active plane; a second G68.2 before the G69 stays RAW. In a subprogram whose caller the reader does not know, a
    // G68.2 is appended to the caller's chain.
    private static void ReadTilt(FanucBlock block)
    {
        if (block.Fanuc.Chain.Holds("G68.2"))
        {
            block.Draft.KeepAsRaw("fanuc 4 does not say what a G68.2 does while a tilted plane is active");
            return;
        }

        SourceWord? convention = block.Take("P");
        SourceWord? first = block.Take("I");
        SourceWord? second = block.Take("J");
        SourceWord? third = block.Take("K");
        if (first?.Number is not decimal i || second?.Number is not decimal j || third?.Number is not decimal k)
        {
            block.Draft.KeepAsRaw("G68.2 needs its angles I, J and K as numbers");
            return;
        }

        int decimals = LeastDecimals;
        foreach (SourceWord word in new[] { first, second, third })
        {
            decimals = Math.Max(decimals, FanucNumbers.DecimalsOf(word));
        }

        decimal a;
        decimal b;
        decimal c;
        switch (convention?.Number ?? 0m)
        {
            case 0m:
                SpatialAngles((double)i, (double)j, (double)k, out double spatialA, out double spatialB,
                    out double spatialC);
                a = FanucNumbers.Round(spatialA, decimals);
                b = FanucNumbers.Round(spatialB, decimals);
                c = FanucNumbers.Round(spatialC, decimals);
                break;
            case 1m:
                // Roll, pitch and yaw turn about X, Y and Z of the frame in that order, the spatial angles of TILT.
                a = i;
                b = j;
                c = k;
                break;
            default:
                block.Draft.KeepAsRaw($"G68.2 P{convention!.Text} is kept as RAW (controller-mapping 1, TILT)");
                return;
        }

        // The origin X Y Z of the plane is a SHIFT before the TILT, in the frame where G68.2 stands (language 4.2,
        // D31).
        var origin = new List<Word>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value value)
            {
                return;
            }

            if (FanucNumbers.NumberOf(value) != 0m)
            {
                origin.Add(new Word { Key = axis.Axis, Value = value });
            }
        }

        var tilt = new FanucChainEntry("TILT", "G68.2",
        [
            new Word { Key = "A", Value = FanucNumbers.Of(a) },
            new Word { Key = "B", Value = FanucNumbers.Of(b) },
            new Word { Key = "C", Value = FanucNumbers.Of(c) },
        ]);
        var blocks = new List<DraftBlock>();
        FanucChain chain = block.Fanuc.Chain;
        if (origin.Count == 0)
        {
            chain.Append(tilt, blocks);
        }
        else if (LocalShiftAtTheEnd(chain, origin) is FanucChainEntry local)
        {
            // The origin and the G52 shift directly in front of it are two shifts of one frame: they stand added in one
            // SHIFT, so that the chain holds one shift and its RESET means the same under both readings of language 4.2
            // (wave-1 question #95); the G69 writes the G52 shift again (FanucFrames).
            var desired = new List<FanucChainEntry>(chain.Entries);
            desired[^1] = new FanucChainEntry("SHIFT", "G68.2", Sum(local.Words, origin), local.Words);
            desired.Add(tilt);
            chain.TryChange(desired, blocks);
        }
        else
        {
            chain.Append(new FanucChainEntry("SHIFT", "G68.2", origin), blocks);
            chain.Append(tilt, blocks);
        }

        if (block.Fanuc.TiltsTurned.Contains(block.Line))
        {
            blocks[^1].Add("MOVE", new IdentValue("TURN"));
        }

        FanucFrames.Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // The SHIFT of a G52 at the end of a known chain that holds no other shift, where every word of the origin is a
    // number; null otherwise.
    private static FanucChainEntry? LocalShiftAtTheEnd(FanucChain chain, List<Word> origin)
    {
        if (!chain.Known || chain.Entries.Count == 0 || chain.Entries[^1] is not { Kind: "SHIFT", Owner: "G52" } last)
        {
            return null;
        }

        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (entry.Kind == "SHIFT" && !ReferenceEquals(entry, last))
            {
                return null;
            }
        }

        foreach (Word word in origin)
        {
            if (FanucNumbers.NumberOf(word.Value) is null)
            {
                return null;
            }
        }

        return last;
    }

    // Two shifts of one frame added axis by axis, the axes in the order they come.
    private static List<Word> Sum(IReadOnlyList<Word> first, List<Word> second)
    {
        var axes = new List<string>();
        var sums = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (Word word in new List<Word>(first).Concat(second))
        {
            if (!sums.ContainsKey(word.Key))
            {
                axes.Add(word.Key);
            }

            sums[word.Key] = sums.GetValueOrDefault(word.Key) + (FanucNumbers.NumberOf(word.Value) ?? 0m);
        }

        var sum = new List<Word>(axes.Count);
        foreach (string axis in axes)
        {
            sum.Add(new Word { Key = axis, Value = FanucNumbers.Of(sums[axis]) });
        }

        return sum;
    }

    // The Euler angles of G68.2 P0 turn about Z by I, about the new X by J and about the new Z by K; the spatial angles
    // of TILT turn about X by A, then about Y by B, then about Z by C of the frame where the word stands (language
    // 4.2), so R = Rz(I) Rx(J) Rz(K) = Rz(C) Ry(B) Rx(A).
    private static void SpatialAngles(double i, double j, double k, out double a, out double b, out double c)
    {
        double sinI = Math.Sin(Radians(i));
        double cosI = Math.Cos(Radians(i));
        double sinJ = Math.Sin(Radians(j));
        double cosJ = Math.Cos(Radians(j));
        double sinK = Math.Sin(Radians(k));
        double cosK = Math.Cos(Radians(k));
        double r00 = (cosI * cosK) - (sinI * cosJ * sinK);
        double r01 = -(cosI * sinK) - (sinI * cosJ * cosK);
        double r10 = (sinI * cosK) + (cosI * cosJ * sinK);
        double r11 = -(sinI * sinK) + (cosI * cosJ * cosK);
        double r20 = sinJ * sinK;
        double r21 = sinJ * cosK;
        double r22 = cosJ;
        b = Math.Asin(Math.Clamp(-r20, -1.0, 1.0));
        if (Math.Abs(Math.Cos(b)) > GimbalLimit)
        {
            a = Math.Atan2(r21, r22);
            c = Math.Atan2(r10, r00);
        }
        else
        {
            a = 0;
            c = Math.Atan2(-r01, r11);
        }

        a = Degrees(a);
        b = Degrees(b);
        c = Degrees(c);
    }

    private static double Radians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double Degrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }
}
