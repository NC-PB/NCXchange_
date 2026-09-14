using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The swivelled plane of CYCLE800 and ROTS (controllers siemens.md 4, 11 rule 4; controller-mapping 1, TILT,
/// TILT_AXIS, MOVE, RETRACT; language 4.2, 4.3; D82, D83): CYCLE800(_FR, _TC, _ST, _MODE, _X0, _Y0, _Z0, _A, _B, _C,
/// _X1, _Y1, _Z1, _DIR, _FR_I, _DMODE) is TILT with the angles, or TILT_AXIS where _MODE gives the rotary axes
/// directly,
/// with MOVE from _DIR and _ST and a RETRACT block in front of it from _FR; CYCLE800() is TILT=RESET; ROTS and AROTS
/// with two spatial angles are TILT. The reader keeps the form the source used (D82).
/// </summary>
internal static class SiemensTilt
{
    // The places of the parameters of CYCLE800 (controllers siemens.md 4).
    private const int Retract = 0;
    private const int Carrier = 1;
    private const int Swivel = 2;
    private const int Mode = 3;
    private const int FirstAngle = 7;
    private const int Direction = 13;
    private const int RetractDistance = 14;

    /// <summary>
    /// Reads CYCLE800, ROTS and AROTS.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (block.Take("CYCLE800") is SourceWord cycle)
        {
            List<string> arguments = SiemensArguments.Split(cycle.Text);
            if (arguments.Count == 0)
            {
                Reset(block);
            }
            else
            {
                Swivel800(block, arguments);
            }

            return;
        }

        SourceWord? rots = block.Take("ROTS") ?? block.Take("AROTS");
        if (rots is not null)
        {
            ReadRots(block, rots);
        }
    }

    // CYCLE800() cancels the swivel: the RESET of the tilts of CYCLE800 the chain holds, TILT=RESET where it holds no
    // tilt at all; a tilt of ROTS stays, which CYCLE800() does not cancel (controller-mapping 1, TILT).
    private static void Reset(SiemensBlock block)
    {
        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        if (chain.Tilt() is null && chain.Known)
        {
            blocks.Add(SiemensChain.ResetOf("TILT"));
        }
        else if (!chain.TryRemoveSwivel(blocks))
        {
            block.Draft.KeepAsRaw("CYCLE800() would reset a tilt that is not the last entry of the chain of "
                + "transforms, "
                + "or one the reader does not know (language 4.2, D31)");
            return;
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // CYCLE800 with its parameters: the angles of _MODE into TILT or TILT_AXIS, MOVE from _DIR and _ST, the RETRACT of
    // _FR in front of it; a new swivel replaces the one of an earlier CYCLE800, an additive one (_ST ones digit 1)
    // builds on it (controllers siemens.md 4; controller-mapping 1, TILT).
    private static void Swivel800(SiemensBlock block, List<string> arguments)
    {
        int? retract = Whole(arguments, Retract);
        int? swivel = Whole(arguments, Swivel);
        int? mode = Whole(arguments, Mode);
        int? direction = Whole(arguments, Direction);
        var angles = new decimal?[3];
        for (int index = 0; index < 3; index++)
        {
            angles[index] = Number(arguments, FirstAngle + index);
        }

        if (retract is not int fr || swivel is not int st || mode is not int modeBits || direction is not int dir
            || angles[0] is null || angles[1] is null || angles[2] is null || SiemensArguments.StringOf(
                arguments.Count > Carrier ? arguments[Carrier] : "") == "0")
        {
            block.Draft.KeepAsRaw("CYCLE800 gives _FR, _ST, _MODE, the angles and _DIR as numbers, and a tool carrier "
                + "other than \"0\", which deselects it (controllers siemens.md 4)");
            return;
        }

        if (!ReferencePointsAreZero(arguments) || (st / 100) % 10 != 0)
        {
            block.Draft.KeepAsRaw("CYCLE800 with a reference point before or after the rotation, or with the tool "
                + "aligned instead of the plane, has no NCX word (controllers siemens.md 4)");
            return;
        }

        SiemensDraftBlock? entry = Entry(block, modeBits, angles);
        if (entry is null)
        {
            return;
        }

        bool tracks = (st / 10) % 10 == 1;
        entry.Add("MOVE", new IdentValue(tracks ? "MOVE" : dir == 0 ? "STAY" : "TURN"));
        SiemensDraftBlock? retractBlock = RetractOf(block, fr, arguments);
        if (block.Draft.IsRaw)
        {
            return;
        }

        var blocks = new List<SiemensDraftBlock>();
        if (retractBlock is not null)
        {
            blocks.Add(retractBlock);
        }

        SiemensChain chain = block.Facts.Chain;
        var next = new SiemensChainEntry(entry.Verb!, "CYCLE800", entry);
        if ((st % 10 != 1 && !chain.TryRemoveSwivel(blocks)) || !chain.TryReplace(null, next, blocks))
        {
            block.Draft.KeepAsRaw("the new swivel of CYCLE800 would replace a tilt that is not the last entry of "
                + "the chain "
                + "of transforms, or one the reader does not know (language 4.2, D31)");
            return;
        }

        WarnNotCarried(block, st, dir, fr);
        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // _MODE bits 7 and 6: 00 axis-wise angles with the rotation order in bits 5 to 0 (57 X Y Z, 27 Z Y X), 01 spatial
    // angles, 10 projection angles, 11 the rotary axes directly (controller-mapping 1, TILT and TILT_AXIS; D82).
    // TODO(question): TILT turns about X, Y and Z in that order (language 4.2), and controller-mapping 11 reads the
    // axis-wise modes 27 and 39 as TILT "with the angles in the order the mode gives" without a conversion between the
    // orders; the reader writes TILT for mode 57, the order of TILT, for the spatial angles, and for any order where at
    // most one angle turns, and keeps the other orders and the projection angles as RAW.
    // TODO(question): which rotary axes _A and _B of the rotary-axes mode name is not in the documents; the reader
    // names the two rotary axes of the machine that no spindle owns, in the order of [[axis]], and keeps the block RAW
    // on a machine without exactly two.
    private static SiemensDraftBlock? Entry(SiemensBlock block, int mode, decimal?[] angles)
    {
        int kind = (mode >> 6) & 3;
        int turning = 0;
        foreach (decimal? angle in angles)
        {
            turning += angle == 0 ? 0 : 1;
        }

        if ((kind == 0 && (mode == 57 || turning <= 1)) || kind == 1)
        {
            var tilt = new SiemensDraftBlock().WithVerb("TILT");
            string[] keys = ["A", "B", "C"];
            for (int index = 0; index < 3; index++)
            {
                tilt.Add(keys[index], SiemensNumbers.Of(angles[index]!.Value));
            }

            return tilt;
        }

        if (kind == 3 && RotaryAxes(block.Machine) is List<string> rotary)
        {
            var axial = new SiemensDraftBlock().WithVerb("TILT_AXIS");
            axial.Add(rotary[0], SiemensNumbers.Of(angles[0]!.Value))
                .Add(rotary[1], SiemensNumbers.Of(angles[1]!.Value));
            return axial;
        }

        block.Draft.KeepAsRaw($"CYCLE800 with _MODE {mode.ToString(CultureInfo.InvariantCulture)} gives its angles in "
            + "an order or a form the reader does not convert (controller-mapping 1, TILT)");
        return null;
    }

    // _FR retracts before the swivel: 0 none, 1 machine Z, 2 Z then XY, 4 in tool direction to the limit, 5 by _FR_I
    // (controller-mapping 1, TILT; D83).
    private static SiemensDraftBlock? RetractOf(SiemensBlock block, int retract, List<string> arguments)
    {
        switch (retract)
        {
            case 0:
                return null;
            case 1 or 2 or 4:
                return new SiemensDraftBlock().WithVerb("RETRACT");
            case 5 when Number(arguments, RetractDistance) is decimal distance && distance > 0:
                return new SiemensDraftBlock().WithVerb("RETRACT", SiemensNumbers.Of(distance));
            default:
                block.Draft.KeepAsRaw("_FR of CYCLE800 names no retract 0, 1, 2, 4 or 5 with its distance (controllers "
                    + "siemens.md 4)");
                return null;
        }
    }

    // The values of CYCLE800 no NCX word carries: the digits of _ST above the hundreds, the preferred solution of
    // _DIR, the XY part of the retract _FR=2 (controllers siemens.md 4).
    private static void WarnNotCarried(SiemensBlock block, int swivel, int direction, int retract)
    {
        var values = new List<string>();
        if (swivel >= 1000)
        {
            values.Add("the digits of _ST above the hundreds");
        }

        if (direction > 0)
        {
            values.Add("the preferred solution +1 of _DIR");
        }

        if (retract == 2)
        {
            values.Add("the retract in X and Y of _FR=2");
        }

        if (values.Count > 0)
        {
            block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensValueNotCarried,
                $"{string.Join(", ", values)} of CYCLE800 {(values.Count == 1 ? "has" : "have")} no NCX word and "
                + $"{(values.Count == 1 ? "is" : "are")} not written (controllers siemens.md 4; D82, D83)."));
        }
    }

    // ROTS X30 Y45 turns the frame by two spatial angles, TILT with the third 0; ROTS replaces the programmable frame,
    // AROTS builds on it (controllers siemens.md 4; controller-mapping 1, TILT).
    private static void ReadRots(SiemensBlock block, SourceWord rots)
    {
        var tilt = new SiemensDraftBlock().WithVerb("TILT");
        var values = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (SourceWord word in block.Unread())
        {
            string? axis = word.Text.Length > 0 ? SiemensAxes.AxisOf(block, word) : null;
            string? key = axis switch
            {
                "X" => "A",
                "Y" => "B",
                "Z" => "C",
                _ => null,
            };
            if (key is null)
            {
                continue;
            }

            block.MarkRead(word);
            if (SiemensExpression.ValueOf(block, word.Text, out string? problem) is not Value value)
            {
                block.Draft.KeepAsRaw($"{rots.Address} {word.Address}={word.Text} has no NCX value: {problem}");
                return;
            }

            values[key] = value;
        }

        foreach (string key in new[] { "A", "B", "C" })
        {
            tilt.Add(key, values.TryGetValue(key, out Value? value) ? value : new IntegerValue(0, "0"));
        }

        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        if (values.Count == 0 || (rots.Address == "ROTS" && !chain.TryCut(blocks)))
        {
            block.Draft.KeepAsRaw($"{rots.Address} needs its spatial angles and a chain the reader knows (controllers "
                + "siemens.md 4)");
            return;
        }

        chain.Append(new SiemensChainEntry("TILT", rots.Address, tilt), blocks);
        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // The rotary axes of a swivel head or table: the rotary axes that no spindle owns, two of them.
    private static List<string>? RotaryAxes(MachineConfig machine)
    {
        var axes = new List<string>();
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary
                && (axis.Owner is null || machine.FindResource(axis.Owner)?.IsSpindle != true))
            {
                axes.Add(axis.NcxName);
            }
        }

        return axes.Count == 2 ? axes : null;
    }

    private static bool ReferencePointsAreZero(List<string> arguments)
    {
        foreach (int index in new[] { 4, 5, 6, 10, 11, 12 })
        {
            if (index < arguments.Count && arguments[index].Length > 0 && Number(arguments, index) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int? Whole(List<string> arguments, int index)
    {
        return index < arguments.Count ? SiemensNumbers.WholeNumber(arguments[index]) : null;
    }

    private static decimal? Number(List<string> arguments, int index)
    {
        return index < arguments.Count ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) : null;
    }
}
