namespace Ncx.Readers.Siemens;

/// <summary>
/// The hole patterns that call the modal cycle of MCALL at every position (controllers siemens.md 7, 11 rule 5;
/// controller-mapping 5, repeats): HOLES1(SPCA, SPCO, STA1, FDIS, DBH, NUM), a row from the reference point SPCA SPCO
/// at the angle STA1, its first hole FDIS from the point and the next ones DBH apart; HOLES2(CPA, CPO, RAD, STA1, INDA,
/// NUM), a circle about CPA CPO of radius RAD from the angle STA1 in steps of INDA, the full circle divided by NUM
/// where INDA is 0. The reader expands them into one CYCLE_CALL per hole.
/// </summary>
internal static class SiemensPatterns
{
    // The most holes the reader expands.
    private const int MaxHoles = 9999;

    /// <summary>
    /// Reads HOLES1 or HOLES2 into the calls of the modal cycle.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="pattern">The pattern word with its arguments.</param>
    public static void Read(SiemensBlock block, SourceWord pattern)
    {
        block.MarkRead(pattern);
        SiemensCycle? cycle = block.Facts.ModalCycle;
        List<string> arguments = SiemensArguments.Split(pattern.Text);
        var values = new List<double>();
        int decimals = SiemensNumbers.LeastDecimals;
        foreach (string argument in arguments.GetRange(0, Math.Min(6, arguments.Count)))
        {
            if (SiemensNumbers.NumberOf(SiemensNumbers.Parse(argument)) is not decimal number)
            {
                break;
            }

            values.Add((double)number);
            decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(argument));
        }

        int count = values.Count == 6 ? (int)values[5] : 0;
        if (cycle is null || values.Count < 6 || count < 1 || count > MaxHoles || values[5] != count)
        {
            block.Draft.KeepAsRaw($"{pattern.Address} calls the modal cycle of MCALL at positions of numbers; the "
                + "reader expands it only so (controllers siemens.md 7)");
            return;
        }

        var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
        double step = pattern.Address == "HOLES2" && values[4] == 0 ? 360.0 / count : values[4];
        for (int hole = 0; hole < count; hole++)
        {
            double x;
            double y;
            if (pattern.Address == "HOLES1")
            {
                double distance = values[3] + (hole * step);
                x = values[0] + (distance * Math.Cos(values[2] * Math.PI / 180));
                y = values[1] + (distance * Math.Sin(values[2] * Math.PI / 180));
            }
            else
            {
                double angle = (values[3] + (hole * step)) * Math.PI / 180;
                x = values[0] + (values[2] * Math.Cos(angle));
                y = values[1] + (values[2] * Math.Sin(angle));
            }

            decimal first = SiemensNumbers.Round(x, decimals);
            decimal second = SiemensNumbers.Round(y, decimals);
            SiemensDraftBlock call = hole == 0 ? block.Draft.Main : new SiemensDraftBlock();
            call.WithVerb("CYCLE_CALL").Add(plane.First, SiemensNumbers.Of(first)).Add(plane.Second,
                SiemensNumbers.Of(second));
            if (hole > 0)
            {
                block.Draft.After.Add(call);
            }

            block.State.SetPosition(plane.First, first);
            block.State.SetPosition(plane.Second, second);
        }

        if (cycle.ReturnPlane is decimal returnPlane)
        {
            block.State.SetPosition(cycle.Axis, returnPlane);
        }
    }
}
