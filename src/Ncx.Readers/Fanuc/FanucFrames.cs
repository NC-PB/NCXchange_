using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The frames of a Fanuc block (controllers fanuc.md 4, 9 rules 3 and 6; controller-mapping 1; language 4.2): G54 to
/// G59 and G54.1 P as ORIGIN, G53 as FRAME=MACHINE, G28 and G30 as HOME, G92 (G50 in system A) as SETPOS and RPM_MAX,
/// G52 as SHIFT with SHIFT=RESET, G68/G69 as ROTATE, G51.1/G50.1 as MIRROR, G43.4/G43.5 as TCPM, G12.1/G13.1 as POLAR,
/// G7.1 as CYLINDER, G5.1 as TOLERANCE, and the builder's codes of [transform]. The tilted plane of G68.2 is FanucTilt,
/// the chain of transforms FanucChain.
/// </summary>
internal static class FanucFrames
{
    // G54 to G59 are the datums 1 to 6 (language 4.2, ORIGIN).
    private static readonly string[] s_origins = ["G54", "G55", "G56", "G57", "G58", "G59"];

    private static readonly IdentValue s_on = new("ON");
    private static readonly IdentValue s_off = new("OFF");

    /// <summary>
    /// Reads the frame words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadOrigin(block);

        // G53 in the block: machine coordinates for that block, also with macro variables (controllers fanuc.md 4, 9
        // rule 3); the motion writes FRAME=MACHINE.
        block.MachineFrame = block.TakeCode("G53");
        ReadHome(block);
        ReadSetpos(block);
        ReadShift(block);
        ReadRotation(block);
        ReadMirror(block);
        FanucTilt.Read(block);
        ReadTcpm(block);
        ReadTransformations(block);
        ReadTolerance(block);
    }

    /// <summary>
    /// Writes the blocks of a change of the chain (FanucChain.Append, FanucChain.TryChange): before the main block,
    /// each in a block of its own, since a RESET and the entries after it depend on their order (language 4.2) while
    /// the state words of one block do not (virtual machine 3, step 3); after it when the main block selects the datum,
    /// which empties the chain first (language 4.2, ORIGIN).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="blocks">The NCX blocks of the change, in order.</param>
    public static void Write(FanucBlock block, List<DraftBlock> blocks)
    {
        List<DraftBlock> place = block.Draft.Main.Has("ORIGIN", null) ? block.Draft.After : block.Draft.Before;
        place.AddRange(blocks);
    }

    /// <summary>
    /// Why a block stays RAW whose code acts on entries of a chain the reader does not know (FanucChain.Known).
    /// </summary>
    /// <param name="code">The code, "G69".</param>
    public static string UnknownChain(string code)
    {
        return $"{code} acts on the transforms of the caller of its subprogram, or on those a called subprogram left, "
            + "which the reader does not know (virtual machine 3.9)";
    }

    // G54 to G59 are the datums 1 to 6, G54.1 Pn the datum 6 + n (language 4.2, ORIGIN; controller-mapping 1). ORIGIN
    // starts an empty chain (language 4.2).
    // TODO(question): fanuc 4 does not say whether G54 ends an active G52 or G68 on the control, while ORIGIN empties
    // the chain of NCX (language 4.2, D31); the reader writes ORIGIN and does not write the shift again.
    private static void ReadOrigin(FanucBlock block)
    {
        int? origin = null;
        for (int index = 0; index < s_origins.Length; index++)
        {
            if (block.TakeCode(s_origins[index]))
            {
                origin = index + 1;
            }
        }

        if (block.TakeCode("G54.1"))
        {
            SourceWord? extra = block.Take("P");
            if (extra?.Number is not decimal number || number < 1 || number != decimal.Truncate(number))
            {
                block.Draft.KeepAsRaw("G54.1 names no additional datum P");
                return;
            }

            origin = 6 + decimal.ToInt32(number);
        }

        if (origin is not int datum)
        {
            return;
        }

        block.Draft.AddState("ORIGIN", null, Integer(datum));
        ClearChain(block);
    }

    // G28 returns the named axes to the reference point, G30 P to the P-th one, POINT=P, the second by default; the
    // axes are unknown in the workpiece frame afterwards (controllers fanuc.md 4, 9 rule 3; controller-mapping 1, HOME;
    // virtual machine 3.4).
    // TODO(question): fanuc 4 says G28 "with a real distance" moves to the intermediate point first, and the documents
    // read G28 B0 of system A, where B is absolute, as HOME B; a value of 0 is no intermediate point in either mode and
    // any other value is one, absolute under G90 and incremental under G91 or by U W, until that is answered.
    private static void ReadHome(FanucBlock block)
    {
        bool reference = block.TakeCode("G28");
        bool further = !reference && block.TakeCode("G30");
        if (!reference && !further)
        {
            return;
        }

        SourceWord? point = further ? block.Take("P") : null;
        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        if (axes.Count == 0)
        {
            return;
        }

        var intermediate = new DraftBlock().WithVerb("RAPID");
        DraftBlock home = block.Draft.Main.WithVerb("HOME");
        foreach (FanucAxisWord axis in axes)
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value value)
            {
                return;
            }

            if (FanucNumbers.NumberOf(value) != 0m)
            {
                // The distance of an absolute address is absolute or incremental by G90 or G91, which the reader may
                // not know here (FanucCallerState).
                if (!axis.Incremental && block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance))
                {
                    block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
                    return;
                }

                intermediate.Add(axis.Key, value);
            }

            home.Add(axis.Axis, NoValue.Instance);
            block.State.ForgetPosition(axis.Axis);
        }

        if (intermediate.Words.Count > 0)
        {
            block.Draft.Before.Add(intermediate);
        }

        if (further)
        {
            decimal number = point?.Number ?? 2m;
            home.Add("POINT", Integer(decimal.ToInt32(decimal.Truncate(number))));
        }
    }

    // G92 X Z (mills, systems B and C) and G50 X Z (system A) declare the current position, SETPOS; with S they set
    // the speed limit, RPM_MAX (controllers fanuc.md 4; controller-mapping 1 SETPOS, 4 RPM_MAX; D55). On a mill G50
    // cancels the scaling, which NCX does not write.
    private static void ReadSetpos(FanucBlock block)
    {
        string code = block.System == GcodeSystem.A ? "G50" : "G92";
        if (!block.TakeCode(code))
        {
            return;
        }

        SourceWord? limit = block.Take("S");
        if (limit is not null && block.Fanuc.Unknowns.Spindle)
        {
            // The limit belongs to the spindle selected last, which the reader may not know here (FanucCallerState).
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("the spindle selected last, whose speed S limits"));
            return;
        }

        if (limit is not null && FanucMacro.ValueOf(block, limit) is Value speed)
        {
            string? role = block.State.LastSpindle;
            block.Draft.AddState("RPM_MAX", FanucFunctions.SpindleAddress(block, role), speed);
        }

        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        if (axes.Count == 0)
        {
            return;
        }

        DraftBlock setpos = block.Draft.Main.WithVerb("SETPOS");
        foreach (FanucAxisWord axis in axes)
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value value)
            {
                return;
            }

            setpos.Add(axis.Key, value);
            FanucMotion.Move(block, axis, value);
        }
    }

    // G52 replaces the earlier G52, so the reader writes SHIFT=RESET before the new SHIFT when one is active; G52 X0 Y0
    // Z0 cancels (controllers fanuc.md 4, 9 rule 6; controller-mapping 1, SHIFT; language 4.2; D31). The axes the new
    // G52 does not name are 0. The control holds the new shift also where the reader keeps the block as RAW.
    // TODO(question): fanuc 4 does not say what an incremental word of G52 does, U2.75 of system A (controller-mapping
    // 2, IX=); the reader adds it to that axis of the active shift, and the new G52 replaces the old one as a whole.
    // TODO(question): fanuc 4 does not say where the local coordinate system of a G52 stands against an active G68,
    // G68.2 or G51.1, whose entries stand in the chain of NCX (language 4.2): in front of them, behind them, or in the
    // place of the earlier G52; and the SHIFT=RESET of a G52 that replaces the earlier one would remove them. A G52
    // while the chain holds such an entry stays RAW.
    private static void ReadShift(FanucBlock block)
    {
        if (!block.TakeCode("G52"))
        {
            return;
        }

        FanucChain chain = block.Fanuc.Chain;
        var shift = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var words = new List<Word>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (axis.Word.Number is not decimal number || axis.Word.Expression is not null)
            {
                block.Draft.KeepAsRaw("G52 with a variable is kept as RAW");
                return;
            }

            if (!axis.Incremental && block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance))
            {
                block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
                return;
            }

            chain.Local.TryGetValue(axis.Axis, out decimal active);
            decimal amount = axis.Incremental ? active + number : number;
            shift[axis.Axis] = amount;
            words.Add(new Word
            {
                Key = axis.Axis,
                Value = axis.Incremental ? FanucNumbers.Of(amount) : axis.Word.ToNcxNumber()!,
            });
        }

        chain.Local.Clear();
        bool shifts = false;
        foreach (KeyValuePair<string, decimal> axis in shift)
        {
            chain.Local[axis.Key] = axis.Value;
            shifts |= axis.Value != 0;
        }

        block.Fanuc.ForgetPositions();
        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G52"));
            return;
        }

        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (entry.Owner != "G52")
            {
                block.Draft.KeepAsRaw(
                    $"fanuc 4 does not say where the local coordinate system of G52 stands against the active {entry.Owner}"
                    + " in the chain of transforms (language 4.2)");
                return;
            }
        }

        // The chain holds at most the one SHIFT of the earlier G52, so the change always cuts it.
        var blocks = new List<DraftBlock>();
        chain.TryChange(shifts ? [new FanucChainEntry("SHIFT", "G52", words)] : [], blocks);
        Write(block, blocks);
    }

    // G68 X Y R rotates the plane by R, G69 cancels it and the tilted plane of G68.2 (controllers fanuc.md 4;
    // controller-mapping 1, ROTATE and TILT).
    // TODO(question): ROTATE turns about the current origin (language 4.2) and G68 about the point X Y, and fanuc 4
    // does not say what G68 without X Y turns about; the reader writes ROTATE for G68 R without a centre or with X0 Y0
    // and keeps a G68 about another point as RAW.
    // TODO(question): fanuc 4 does not say what a G68 does while a G68 is active, a new rotation or one added to it; a
    // second G68 before the G69 stays RAW. In a subprogram whose caller the reader does not know, a G68 is appended to
    // the caller's chain.
    private static void ReadRotation(FanucBlock block)
    {
        if (block.TakeCode("G68"))
        {
            SourceWord? angle = block.Take("R");
            foreach (FanucAxisWord axis in FanucAxes.Unread(block))
            {
                block.MarkRead(axis.Word);
                if (axis.Word.Number != 0m)
                {
                    block.Draft.KeepAsRaw("G68 turns about the point X Y and ROTATE about the origin (language 4.2)");
                    return;
                }
            }

            if (angle is null)
            {
                block.Draft.KeepAsRaw("G68 names no angle R");
            }
            else if (block.Fanuc.Chain.Holds("G68"))
            {
                block.Draft.KeepAsRaw("fanuc 4 does not say what a G68 does while a G68 is active");
            }
            else if (FanucMacro.ValueOf(block, angle) is Value value)
            {
                var blocks = new List<DraftBlock>();
                block.Fanuc.Chain.Append(
                    new FanucChainEntry("ROTATE", "G68", [new Word { Key = "ROTATE", Value = value }]), blocks);
                Write(block, blocks);
                block.Fanuc.ForgetPositions();
            }

            return;
        }

        if (block.TakeCode("G69"))
        {
            ReadCancel(block);
        }
    }

    // G69 cancels G68 and the tilted plane of G68.2, its origin with it (controllers fanuc.md 4): the chain keeps the
    // entries of the other functions, and of a SHIFT that holds a G52 shift and the origin of the G68.2 together the G52
    // shift (FanucTilt).
    private static void ReadCancel(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G69"));
            return;
        }

        var desired = new List<FanucChainEntry>();
        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (entry.Owner is not ("G68" or "G68.2"))
            {
                desired.Add(entry);
            }
            else if (entry.LocalShift is IReadOnlyList<Word> local)
            {
                desired.Add(new FanucChainEntry("SHIFT", "G52", local));
            }
        }

        Change(block, desired, "G69");
    }

    // G51.1 X0 mirrors the named axes, G50.1 cancels (controllers fanuc.md 4; controller-mapping 1, MIRROR). The value
    // is the position of the mirror line and MIRROR mirrors about the origin (language 4.2), so another value stays
    // RAW.
    private static void ReadMirror(FanucBlock block)
    {
        bool mirror = block.TakeCode("G51.1");
        bool cancel = !mirror && block.TakeCode("G50.1");
        if (!mirror && !cancel)
        {
            return;
        }

        var axes = new List<string>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (mirror && axis.Word.Number != 0m)
            {
                block.Draft.KeepAsRaw("G51.1 mirrors about a line away from the origin, MIRROR about the origin");
                return;
            }

            axes.Add(axis.Axis);
        }

        FanucChain chain = block.Fanuc.Chain;
        if (mirror && axes.Count > 0)
        {
            Value value = axes.Count == 1 ? new IdentValue(axes[0]) : new ListValue(axes);
            var blocks = new List<DraftBlock>();
            chain.Append(new FanucChainEntry("MIRROR", "G51.1", [new Word { Key = "MIRROR", Value = value }]), blocks);
            Write(block, blocks);
            block.Fanuc.ForgetPositions();
        }
        else if (cancel && !chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G50.1"));
        }
        else if (cancel && chain.Holds("G51.1"))
        {
            var desired = new List<FanucChainEntry>();
            foreach (FanucChainEntry entry in chain.Entries)
            {
                if (entry.Owner != "G51.1")
                {
                    desired.Add(entry);
                }
            }

            Change(block, desired, "G50.1");
        }
    }

    // The chain becomes the one the control holds after the code (FanucChain.TryChange); where no cut of the chain means
    // the same under both readings of SHIFT=RESET, the block stays RAW (wave-1 question #95; D5).
    private static void Change(FanucBlock block, List<FanucChainEntry> desired, string code)
    {
        var blocks = new List<DraftBlock>();
        if (!block.Fanuc.Chain.TryChange(desired, blocks))
        {
            block.Draft.KeepAsRaw(
                $"{code} would unwind the chain of transforms with a SHIFT=RESET that the documents read in two ways, "
                + "the shifts or the last one (language 4.2, virtual machine 2.1)");
            return;
        }

        Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // G43.4 is tool center point control, G43.5 the same with the tool vector as I J K on the lines, TCPM=ON; G49 ends
    // it (controllers fanuc.md 4; controller-mapping 1, TCPM; language 4.2); a builder's codes, G435 and G436 on the
    // Nakamura, come from [transform]. H of G43.4 is the length offset, OFFSET:LEN. Where the reader does not know
    // whether TCPM is on (FanucCallerState), G49 writes TCPM=OFF, which it is after the G49 either way.
    private static void ReadTcpm(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        bool vector = block.TakeCode("G43.5");
        bool on = vector || block.TakeCode("G43.4") || (!IsNativeTcpm(transform?.TcpmOn)
            && FanucTemplates.TryMatch(block, transform?.TcpmOn, out _));
        bool active = block.Fanuc.Tcpm || block.Fanuc.Unknowns.Tcpm;
        bool off = !on && ((active && block.HasCode("G49"))
            || (!IsNativeTcpm(transform?.TcpmOff) && FanucTemplates.TryMatch(block, transform?.TcpmOff, out _)));
        if (on)
        {
            block.Draft.AddState("TCPM", null, s_on);
            block.Fanuc.Tcpm = true;
            block.Fanuc.VectorTcpm = vector;
            block.Fanuc.Unknowns.Tcpm = false;
            if (block.Find("H") is SourceWord length && FanucAxes.Of(length, block) is null)
            {
                block.MarkRead(length);
                if (length.Number is decimal register && register == decimal.Truncate(register))
                {
                    block.Draft.AddState("OFFSET", "LEN", Integer(decimal.ToInt32(register)));
                }
                else
                {
                    block.Draft.KeepAsRaw("the H of G43.4 names no offset register");
                }
            }
        }
        else if (off)
        {
            block.Draft.AddState("TCPM", null, s_off);
            block.Fanuc.Tcpm = false;
            block.Fanuc.VectorTcpm = false;
            block.Fanuc.Unknowns.Tcpm = false;
        }
    }

    // The native codes of tool center point control are read by their number, so a [transform] template that writes
    // them is not matched a second time.
    private static bool IsNativeTcpm(string? template)
    {
        return template is not null
            && (template.StartsWith("G43.4", StringComparison.Ordinal)
                || template.StartsWith("G43.5", StringComparison.Ordinal)
                || template.StartsWith("G49", StringComparison.Ordinal));
    }

    // G12.1 and G13.1 switch polar interpolation, POLAR; G7.1 C r the cylinder surface with its radius, CYLINDER=r, and
    // G7.1 C0 off; a builder's codes, G112, G113 and G107 on the Nakamura, come from [transform] (controllers fanuc.md
    // 4; controller-mapping 1, CYLINDER and POLAR; language 4.2; D96, D102).
    private static void ReadTransformations(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        if (block.TakeCode("G12.1") || FanucTemplates.TryMatch(block, transform?.PolarOn, out _))
        {
            block.Draft.AddState("POLAR", null, s_on);
            block.Fanuc.Polar = true;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }
        else if (block.TakeCode("G13.1") || FanucTemplates.TryMatch(block, transform?.PolarOff, out _))
        {
            block.Draft.AddState("POLAR", null, s_off);
            block.Fanuc.Polar = false;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }

        Value? cylinder = null;
        if (FanucTemplates.TryMatch(block, transform?.CylinderOff, out _))
        {
            cylinder = s_off;
        }
        else if (FanucTemplates.TryMatch(block, transform?.CylinderOn, out TemplateValues values))
        {
            cylinder = values.TryGetNumber("r", out decimal radius) && radius != 0 ? FanucNumbers.Of(radius) : s_off;
        }
        else if (block.TakeCode("G7.1"))
        {
            SourceWord? radius = block.Take("C");
            cylinder = radius?.Number switch
            {
                null => null,
                0m => s_off,
                _ => radius.ToNcxNumber(),
            };
            if (cylinder is null)
            {
                block.Draft.KeepAsRaw("G7.1 names no radius C as a number");
                return;
            }
        }

        if (cylinder is not null)
        {
            block.Draft.AddState("CYLINDER", null, cylinder);
            block.Fanuc.ForgetPositions();
        }
    }

    // G5.1 Q1 switches AI contour control on and Q0 off; the tolerance itself sits in a parameter, so NCX TOLERANCE
    // reaches Fanuc only as the on and off switch (controllers fanuc.md 4; controller-mapping 1, TOLERANCE; D85).
    // TODO(question): the phase plan reads G5.1 Q1 as TOLERANCE, while controller-mapping 1 keeps its value RAW and
    // TOLERANCE takes the tolerance as its value (language 4.1); G5.1 Q0 is TOLERANCE=OFF and G5.1 Q1 stays RAW.
    private static void ReadTolerance(FanucBlock block)
    {
        if (!block.TakeCode("G5.1"))
        {
            return;
        }

        if (block.Take("Q")?.Number == 0m)
        {
            block.Draft.AddState("TOLERANCE", null, s_off);
            return;
        }

        block.Draft.KeepAsRaw(
            "G5.1 Q1 switches AI contour control on with the tolerance of a parameter (controller-mapping 1, "
            + "TOLERANCE)");
    }

    // ORIGIN empties the chain of NCX, and a new frame makes the positions of the reader unknown (language 4.2,
    // virtual machine 3.4).
    private static void ClearChain(FanucBlock block)
    {
        block.Fanuc.Chain.Clear();
        block.Fanuc.ForgetPositions();
    }

    private static IntegerValue Integer(int number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }
}
