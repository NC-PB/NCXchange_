using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The frames of a SINUMERIK block (controllers siemens.md 3, 4, 11 rule 4; controller-mapping 1; language 4.2; D31):
/// G54 to G57 and G505 to G599 as ORIGIN 1 to 99, G500 as ORIGIN=0; TRANS, ROT, MIRROR and SCALE cut the chain at its
/// first entry and then append, ATRANS, AROT, AMIRROR append; ROT RPL= and a rotation about the tool axis as ROTATE, a
/// rotation about another axis as TILT; G58 and G59 set the absolute and the additive part of the shift; G74 as HOME,
/// G75 FP= as HOME POINT=; PRESETON as SETPOS. The tilt of CYCLE800 and ROTS is SiemensTilt, G53, G153 and SUPA the
/// FRAME=MACHINE of SiemensMotion.
/// </summary>
internal static class SiemensFrames
{
    // The programmable frame instructions: the replacing ones delete the whole programmable frame before they act
    // (controllers siemens.md 4).
    private static readonly string[] s_instructions = ["TRANS", "ATRANS", "ROT", "AROT", "MIRROR", "AMIRROR", "SCALE",
        "ASCALE"];

    /// <summary>
    /// Reads the frame words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadOrigin(block);
        ReadInstruction(block);
        ReadPartShift(block);
        ReadHome(block);
        ReadPreset(block);
    }

    // G54 to G57 are the datums 1 to 4, G505 to G599 5 and up, G500 cancels them (controller-mapping 1, ORIGIN); ORIGIN
    // starts an empty chain (language 4.2).
    // TODO(question): siemens 4 does not say whether G54 to G599 and G500 end the programmable frame on the control,
    // while ORIGIN empties the chain of NCX (as wave-2 question #59 asks for the Fanuc G54); the reader writes ORIGIN
    // and does not write the programmable frame again.
    private static void ReadOrigin(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? code = SiemensBlock.CodeOf(word);
            if (code is null || SiemensGroups.GroupOf(code) != SiemensGroups.Datum)
            {
                continue;
            }

            block.MarkRead(word);
            int number = int.Parse(code.AsSpan(1), CultureInfo.InvariantCulture);
            int datum = number switch
            {
                500 => 0,
                >= 505 => number - 500,
                _ => number - 53,
            };
            block.Draft.AddState("ORIGIN", null, new IntegerValue(datum, datum.ToString(CultureInfo.InvariantCulture)));
            block.Facts.Chain.Clear();
            block.Facts.Unknown.Remove(SiemensFacts.Frames);
            block.Siemens.ForgetPositions();
        }
    }

    // TRANS X Y Z, ROT, MIRROR X0, SCALE: the replacing instructions delete every earlier programmable frame
    // instruction, so the reader cuts the chain at its first entry and appends the new word; ATRANS, AROT, AMIRROR
    // build on the current frame and are appended as written (controllers siemens.md 4; controller-mapping 1, SHIFT,
    // ROTATE, MIRROR; language 4.2, D31).
    private static void ReadInstruction(SiemensBlock block)
    {
        SourceWord? instruction = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length == 0 && Array.IndexOf(s_instructions, word.Address) >= 0)
            {
                instruction = word;
                break;
            }
        }

        if (instruction is null)
        {
            return;
        }

        block.MarkRead(instruction);
        string name = instruction.Address;
        if (name == "ASCALE")
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw("ASCALE scales the frame, which NCX has no word for (controllers siemens.md 4)");
            return;
        }

        List<SourceWord> axes = FrameWords(block);
        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        bool replacing = name is "TRANS" or "ROT" or "MIRROR" or "SCALE";
        if (replacing && !chain.TryCut(blocks))
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw($"{name} deletes the programmable frame, and the chain holds a tilt of CYCLE800 "
                + "or is "
                + "not known here (controllers siemens.md 4; virtual machine 3.9)");
            return;
        }

        SiemensChainEntry? entry = name switch
        {
            "TRANS" or "ATRANS" => Shift(block, name, axes),
            "ROT" or "AROT" => Rotation(block, name, axes),
            "MIRROR" or "AMIRROR" => Mirror(block, name, axes),
            _ => Scale(block, instruction, axes),
        };
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (entry is not null)
        {
            chain.Append(entry, blocks);
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // TRANS X10 Y20 is SHIFT X=10 Y=20, an axis left out is 0 (language 4.2); TRANS alone clears the programmable
    // frame and appends nothing.
    private static SiemensChainEntry? Shift(SiemensBlock block, string name, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        var shift = new SiemensDraftBlock().WithVerb("SHIFT");
        foreach (SourceWord word in axes)
        {
            if (Value(block, word) is not Value value)
            {
                return null;
            }

            shift.Add(SiemensAxes.AxisOf(block, word)!, value);
        }

        return new SiemensChainEntry("SHIFT", name, shift);
    }

    // ROT RPL=30 and ROT Z30 under G17 turn about the tool axis, ROTATE=30; ROT X180 turns about another axis, the
    // spatial angle of TILT about it; ROT with several axes is a rotation in Euler order that the reader keeps as RAW
    // (controller-mapping 1, ROTATE; language 4.2).
    private static SiemensChainEntry? Rotation(SiemensBlock block, string name, List<SourceWord> axes)
    {
        SourceWord? plane = block.Take("RPL");
        var tool = SiemensPlane.Of(block.Facts.WorkingPlane).Tool;
        if (plane is null && axes.Count == 0)
        {
            return null;
        }

        if ((plane is not null && axes.Count > 0) || axes.Count > 1)
        {
            block.Draft.KeepAsRaw($"{name} turns about several axes in Euler order, which the reader keeps as RAW "
                + "(controllers siemens.md 4)");
            return null;
        }

        SourceWord angleWord = plane ?? axes[0];
        if (Value(block, angleWord) is not Value angle)
        {
            return null;
        }

        string axis = plane is null ? SiemensAxes.AxisOf(block, angleWord)! : tool;
        if (axis == tool)
        {
            return new SiemensChainEntry("ROTATE", name, new SiemensDraftBlock().Add("ROTATE", angle));
        }

        string spatial = axis switch
        {
            "X" => "A",
            "Y" => "B",
            _ => "C",
        };
        var tilt = new SiemensDraftBlock().WithVerb("TILT");
        foreach (string key in new[] { "A", "B", "C" })
        {
            tilt.Add(key, key == spatial ? angle : new IntegerValue(0, "0"));
        }

        return new SiemensChainEntry("TILT", name, tilt);
    }

    // MIRROR X0 mirrors the named axes, the value is meaningless; MIRROR alone clears (controller-mapping 1, MIRROR).
    private static SiemensChainEntry? Mirror(SiemensBlock block, string name, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        var names = new List<string>();
        foreach (SourceWord word in axes)
        {
            block.MarkRead(word);
            names.Add(SiemensAxes.AxisOf(block, word)!);
        }

        Value value = names.Count == 1 ? new IdentValue(names[0]) : new ListValue(names);
        return new SiemensChainEntry("MIRROR", name, new SiemensDraftBlock().Add("MIRROR", value));
    }

    // SCALE with factors has no NCX word; after the cut of the chain the factors stay a RAW word of the block, SCALE
    // alone only clears (controllers siemens.md 4).
    private static SiemensChainEntry? Scale(SiemensBlock block, SourceWord instruction, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        block.Draft.RawWords.Add(block.SpanOf(instruction));
        foreach (SourceWord word in axes)
        {
            block.KeepAsRawWord(word);
        }

        // The scale stays in the frame of the control, where only the RAW word says so, and the next instruction that
        // deletes the frame deletes it too: the reader no longer knows the chain (D5).
        block.Facts.MakeUnknown(SiemensFacts.Frames);
        return null;
    }

    // G58 X.. replaces the absolute part of the programmable shift per axis, G59 X.. the additive part (controllers
    // siemens.md 4; controller-mapping 1, ORIGIN): the reader replaces the shift of TRANS or of ATRANS where it is the
    // last entry of the chain, or appends one to an empty chain, and G59 appends one after the shift of TRANS.
    private static void ReadPartShift(SiemensBlock block)
    {
        bool absolute = block.TakeCode("G58");
        bool additive = !absolute && block.TakeCode("G59");
        if (!absolute && !additive)
        {
            return;
        }

        SiemensChain chain = block.Facts.Chain;
        SiemensChainEntry? old = chain.LastOf(absolute ? "TRANS" : "ATRANS");
        var shift = new SiemensDraftBlock().WithVerb("SHIFT");
        if (old is not null)
        {
            shift.Words.AddRange(old.Block.Words);
        }

        foreach (SourceWord word in FrameWords(block))
        {
            if (Value(block, word) is not Value value)
            {
                return;
            }

            string axis = SiemensAxes.AxisOf(block, word)!;
            shift.Remove(axis);
            shift.Add(axis, value);
        }

        // A G59 without an additive shift before it appends one where the shift of TRANS or G58 is the last entry.
        var blocks = new List<SiemensDraftBlock>();
        bool fits = old is not null || chain.Entries.Count == 0
            || (additive && chain.Entries[^1] is { Kind: "SHIFT", Instruction: "TRANS" });
        if (!fits || !chain.TryReplace(old, new SiemensChainEntry("SHIFT", absolute ? "TRANS" : "ATRANS", shift),
            blocks))
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw((absolute ? "G58" : "G59") + " sets a part of a shift that is not the last entry of "
                + "the chain of transforms, or one the reader does not know (language 4.2, D31)");
            return;
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // G74 X1=0 Y1=0 Z1=0 returns the axes to the reference point by their machine names, HOME X Y Z; G75 X0 Z0 FP=2 to
    // the fixed point FP, HOME X Z POINT=2 (controllers siemens.md 3; controller-mapping 1, HOME).
    // TODO(question): controller-mapping 1 reads the fixed points of G75 as POINT, 1 to 4, and G74 as HOME, while a
    // HOME without POINT is the first reference point (virtual machine 3, step 5); G75 without FP, the fixed point 1,
    // is written HOME with POINT=1.
    private static void ReadHome(SiemensBlock block)
    {
        bool reference = block.TakeCode("G74");
        bool fixedPoint = !reference && block.TakeCode("G75");
        if (!reference && !fixedPoint)
        {
            return;
        }

        SiemensDraftBlock home = block.Draft.Main.WithVerb("HOME");
        foreach (SourceWord word in block.Unread())
        {
            string? axis = word.Text.Length > 0 && !word.Text.StartsWith('(')
                ? SiemensAxes.MachineAxisOf(block.Machine, word.Address)
                : null;
            if (axis is null)
            {
                continue;
            }

            block.MarkRead(word);
            home.Add(axis, NoValue.Instance);
            block.State.ForgetPosition(axis);
        }

        if (fixedPoint)
        {
            SourceWord? point = block.Take("FP");
            int? number = point is null ? 1 : SiemensNumbers.WholeNumber(point.Text);
            if (number is not int fixedNumber || fixedNumber < 1)
            {
                block.Draft.KeepAsRaw("FP of G75 names no fixed point 1 to 4 (controllers siemens.md 3)");
                return;
            }

            home.Add("POINT", new IntegerValue(fixedNumber, fixedNumber.ToString(CultureInfo.InvariantCulture)));
        }

        block.Facts.Tangent = null;
    }

    // PRESETON(C, 0) declares the current position of an axis, SETPOS C=0; PRESETONS keeps the reference status and
    // reads the same (controller-mapping 1, SETPOS; D55).
    private static void ReadPreset(SiemensBlock block)
    {
        SourceWord? preset = block.Take("PRESETON") ?? block.Take("PRESETONS");
        if (preset is null)
        {
            return;
        }

        List<string> arguments = SiemensArguments.Split(preset.Text);
        if (arguments.Count == 0 || arguments.Count % 2 != 0)
        {
            block.Draft.KeepAsRaw($"{preset.Address} gives no pairs of axis and position (controllers siemens.md 3)");
            return;
        }

        SiemensDraftBlock setpos = block.Draft.Main.WithVerb("SETPOS");
        for (int index = 0; index < arguments.Count; index += 2)
        {
            string? axis = SiemensAxes.MachineAxisOf(block.Machine, arguments[index].Trim().ToUpperInvariant());
            Value? value = SiemensExpression.ValueOf(block, arguments[index + 1], out string? problem);
            if (axis is null || value is null)
            {
                block.Draft.KeepAsRaw($"{preset.Address} names an axis or a position NCX cannot write: {problem}");
                return;
            }

            setpos.Add(axis, value);
            SiemensMotion.Move(block, axis, value, incremental: false);
        }
    }

    // The axis words of a frame block, read; a value of AC() or IC() is none a frame takes.
    private static List<SourceWord> FrameWords(SiemensBlock block)
    {
        var words = new List<SourceWord>();
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length > 0 && !word.Text.StartsWith('(') && SiemensAxes.AxisOf(block, word) is not null)
            {
                block.MarkRead(word);
                words.Add(word);
            }
        }

        return words;
    }

    private static Value? Value(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        Value? value = SiemensMotion.Inner(word.Text, out _) is null
            ? SiemensExpression.ValueOf(block, word.Text, out string? problem)
            : null;
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}={word.Text} is no value a frame takes (language 4.2)");
        }

        return value;
    }
}
