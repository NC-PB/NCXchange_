using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// SETPOS folded into SHIFT (machine-config 3: "Heidenhain: none, the compiler folds it into SHIFT"; D55): Klartext has
/// no command that declares the current position, so the compiler writes the cycle 7 datum shift that makes the
/// position read as the declared value. Its value is the setpos shift the virtual machine records for the axis,
/// newSetposShift = oldPos - declared (virtual machine 3.4, D101): SETPOS X=0 at X+10 is CYCL DEF 7.0 NULLPUNKT,
/// CYCL DEF 7.1 X+10. The cycle stands where the SETPOS stands, after the transforms of the chain active there, and a
/// transform appended later is applied to the frame it shifted (language 4.2, D31); only ORIGIN clears it.
/// </summary>
internal static class HeidenhainSetpos
{
    /// <summary>
    /// What the control has active under this key of the target state: the place of the cycle 7 of the setpos shifts,
    /// the number of chain entries it stands after. Unknown at the start of a program and of every walk of a
    /// subprogram.
    /// </summary>
    public const string PlaceKey = "SETPOS";

    // The state key of a setpos shift that is UNKNOWN, SETPOS:C (ChannelSnapshot.Unknown; virtual machine 1, D101).
    private const string UnknownShiftKey = "SETPOS:";

    /// <summary>
    /// Writes SETPOS as cycle 7 with the setpos shift of every axis it names and of every other axis whose setpos shift
    /// stands, since a new cycle 7 replaces the previous one (controllers heidenhain.md 3; machine-config 3; D55).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="origin">True when the block has ORIGIN, which cleared the setpos shifts before the SETPOS.</param>
    public static void Write(HeidenhainBlock writing, bool origin)
    {
        if (writing.Take("SETPOS") is null)
        {
            return;
        }

        List<string>? axes = NamedAxes(writing);
        if (axes is null || !FitsTheChain(writing, origin))
        {
            return;
        }

        // The setpos shifts of the axes the block does not name stand as well (virtual machine 3.4); one that is
        // unknown was reported at its own SETPOS.
        foreach (string axis in StandingAxes(writing.After))
        {
            if (!axes.Contains(axis) && !ShiftUnknown(writing.After, axis))
            {
                axes.Add(axis);
            }
        }

        var values = new List<string>();
        foreach (string axis in HeidenhainChain.SortedAxes(axes))
        {
            string letter = HeidenhainAxes.LetterOf(writing, axis);
            values.Add(letter + HeidenhainNumbers.Signed(writing, letter, writing.After.Frame.SetposShift[axis]));
        }

        HeidenhainChain.WriteDatumShift(writing, values);
        writing.Target.Set(PlaceKey, writing.After.Frame.Chain.Count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The place of the cycle 7 of the setpos shifts that stand before the block, the number of chain entries it stands
    /// after; the end of the chain where the target state does not know it; null when no setpos shift stands.
    /// </summary>
    public static int? PlaceBefore(HeidenhainBlock writing)
    {
        if (StandingAxes(writing.Before).Count == 0)
        {
            return null;
        }

        return PlaceOf(writing) ?? writing.Before.Frame.Chain.Count;
    }

    /// <summary>
    /// Cancels the cycle 7 of the setpos shifts that stood before the block, cycle 7 with each of their axes at 0, as
    /// the cycle 7 of a SHIFT is cancelled (controllers heidenhain.md 3): ORIGIN clears the setpos shifts (virtual
    /// machine 3.4).
    /// </summary>
    public static void Cancel(HeidenhainBlock writing)
    {
        var values = new List<string>();
        foreach (string axis in HeidenhainChain.SortedAxes(StandingAxes(writing.Before)))
        {
            string letter = HeidenhainAxes.LetterOf(writing, axis);
            values.Add(letter + HeidenhainNumbers.Signed(writing, letter, 0m));
        }

        HeidenhainChain.WriteDatumShift(writing, values);
        writing.Target.Forget(PlaceKey);
    }

    /// <summary>
    /// A RESET that removes transforms of the chain which stood before the cycle 7 of a SETPOS: a transform that turns
    /// none of the axes whose setpos shift stands acts the same before and after that shift, and the cycle then stands
    /// after the entries left before it.
    /// </summary>
    // TODO(question): language 4.2 applies SETPOS to the frame active where it stands and lets a RESET remove chain
    // entries only, and virtual machine 3.4 reads the setpos shift on top of the chain; where the setpos shift of an
    // axis acts once a ROTATE, MIRROR, TILT or TILT_AXIS that stood before the SETPOS and turns that axis is removed is
    // not given, nor where the Klartext cycle acts that cancels a transform the cycle 7 of the SETPOS follows (D253).
    // Such a RESET is reported (CMP114) and nothing is guessed; a place the target state does not know counts as the
    // end of the chain.
    public static void CheckRemoved(HeidenhainBlock writing, ChainChange change)
    {
        if (change.Removed.Count == 0)
        {
            return;
        }

        int count = writing.Before.Frame.Chain.Count;
        int place = PlaceOf(writing) ?? count;
        List<string> standing = StandingAxes(writing.Before);
        int index = count;
        foreach (TransformEntry removed in change.Removed)
        {
            index--;
            foreach (string axis in standing)
            {
                if (index < place && Turns(removed, axis))
                {
                    writing.Error(DiagnosticCodes.HeidenhainSetposFrameRemoved,
                        $"The {removed.Kind.ToString().ToUpperInvariant()} the block removes stood in the chain before "
                        + $"the SETPOS whose shift of {axis} the compiler writes as cycle 7, and it turns {axis}; "
                        + "where that shift acts once the frame it was declared in is gone is not given "
                        + "(machine-config 3; language 4.2; virtual machine 3.4; D253).");
                    return;
                }
            }
        }

        int kept = count - change.Removed.Count;
        if (PlaceOf(writing) is int known && known > kept)
        {
            writing.Target.Set(PlaceKey, kept.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The axes whose setpos shift stands in a state: not 0, or unknown (virtual machine 1, 3.4, D101); a setpos shift
    /// of 0 leaves the frame as it is.
    /// </summary>
    public static List<string> StandingAxes(ChannelSnapshot state)
    {
        var axes = new List<string>();
        foreach (KeyValuePair<string, decimal> shift in state.Frame.SetposShift)
        {
            if (shift.Value != 0m || ShiftUnknown(state, shift.Key))
            {
                axes.Add(shift.Key);
            }
        }

        return axes;
    }

    /// <summary>
    /// The coordinate of an axis in the workpiece frame of the program, where the Klartext coordinates stand: the
    /// position store keeps the physical value, and the workpiece coordinate is read through the setpos shift, the
    /// stored value itself where that shift is unknown (virtual machine 3.4, D101); null while the axis is not known in
    /// the workpiece frame.
    /// </summary>
    public static decimal? WorkpieceCoordinate(ChannelSnapshot state, string axis)
    {
        if (!state.Motion.Position.TryGetValue(axis, out AxisPosition position) || !position.Known
            || position.Frame != PositionFrame.Workpiece)
        {
            return null;
        }

        if (ShiftUnknown(state, axis) || !state.Frame.SetposShift.TryGetValue(axis, out decimal shift))
        {
            return position.Value;
        }

        return position.Value - shift;
    }

    // The axes of the absolute axis words of the block, each marked as written; null when the shift of one of them has
    // no Klartext form, which is reported.
    // TODO(question): the position of an axis known in the MACHINE frame only, after HOME or a machine-frame move, is
    // not known in the frame of the active preset without a datum table (virtual machine 3.4, D35), and virtual machine
    // 3.4 and D101 record the setpos shift against the machine position; how such a SETPOS is folded into cycle 7, a
    // shift against the preset, is not given. It is reported (CMP113) and nothing is guessed.
    // TODO(question): D117: whether SETPOS takes the incremental forms (SETPOS IX=0) is open, and the virtual machine
    // changes nothing for them; an incremental word is left unwritten, the ERROR CMP101, as D117 recommends an ERROR.
    private static List<string>? NamedAxes(HeidenhainBlock writing)
    {
        var axes = new List<string>();
        bool written = true;
        foreach (HeidenhainAxisWord word in HeidenhainAxes.Of(writing.Block))
        {
            if (word.Incremental)
            {
                continue;
            }

            writing.MarkWritten(word.Word);
            if (!KnownInWorkpieceFrame(writing, word.Axis))
            {
                writing.Error(DiagnosticCodes.HeidenhainSetposWithoutWorkpiecePosition,
                    $"{word.Word.ToCanonical()}: the position of {word.Axis} before the block is not known in the "
                    + "workpiece frame (after HOME or a machine-frame move it is known in the MACHINE frame only), and "
                    + "the compiler writes SETPOS as the cycle 7 datum shift against the active preset, which it does "
                    + "not know there (machine-config 3; virtual machine 3.4; D35, D55, D101).");
                written = false;
            }
            else if (HeidenhainNumbers.NumberOf(word.Word.Value) is null)
            {
                // Controllers heidenhain.md 6 and the TODO(question) of HeidenhainNumbers: the shift would be the
                // formula position minus the value, and a coordinate of Klartext takes a number or a Q parameter.
                writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
                    $"{word.Word.ToCanonical()}: cycle 7 would take the position of {word.Axis} minus this value, a "
                    + "formula, and Klartext takes a number or a Q parameter there; nothing is written for it "
                    + "(controllers heidenhain.md 6; machine-config 3; virtual machine 3.4).");
                written = false;
            }
            else
            {
                axes.Add(word.Axis);
            }
        }

        return written ? axes : null;
    }

    // The cycle 7 of the SETPOS is the one cycle 7 the control has active, after the transforms of the chain active at
    // the SETPOS (language 4.2, D31); a new cycle 7 replaces the previous one (controllers heidenhain.md 3).
    // TODO(question): D253: where a replacing Klartext transform acts when others follow the one it replaces is open;
    // a SETPOS while a SHIFT stands in the chain, and one whose cycle 7 replaces the cycle 7 of an earlier SETPOS that
    // transforms of the chain follow (or may follow, where the target state does not know its place), are reported
    // (CMP106), as HeidenhainChain.Append reports a SHIFT while one stands.
    private static bool FitsTheChain(HeidenhainBlock writing, bool origin)
    {
        foreach (TransformEntry entry in writing.After.Frame.Chain)
        {
            if (entry.Kind == TransformKind.Shift)
            {
                writing.Error(DiagnosticCodes.HeidenhainTransformReplacesAnother,
                    "The SETPOS of the block is written as cycle 7 while a SHIFT stands in the chain, and Klartext "
                    + "replaces the cycle 7 of that SHIFT with the new one, so the frame of the control would not be "
                    + "the frame of the program (controllers heidenhain.md 3; machine-config 3; language 4.2, D31, "
                    + "D253).");
                return false;
            }
        }

        int following = writing.After.Frame.Chain.Count - (PlaceOf(writing) ?? 0);
        if (!origin && StandingAxes(writing.Before).Count > 0 && following > 0)
        {
            writing.Error(DiagnosticCodes.HeidenhainTransformReplacesAnother,
                "The cycle 7 of the SETPOS would replace the cycle 7 of the SETPOS before it, which transforms of the "
                + "chain follow, and where the new cycle acts then is not given (controllers heidenhain.md 3; "
                + "machine-config 3; language 4.2, D31, D253).");
            return false;
        }

        return true;
    }

    // The position of the axis before the block is known in the workpiece frame and not through a setpos shift
    // recorded against the machine position (virtual machine 3.4, D101), so that its setpos shift is one against the
    // active preset.
    private static bool KnownInWorkpieceFrame(HeidenhainBlock writing, string axis)
    {
        return writing.Before.Motion.Position.TryGetValue(axis, out AxisPosition position)
            && position.Known
            && position.Frame == PositionFrame.Workpiece
            && !ShiftUnknown(writing.Before, axis)
            && !writing.Before.Frame.SetposAgainstMachine.ContainsKey(axis)
            && !writing.After.Frame.SetposAgainstMachine.ContainsKey(axis)
            && writing.After.Frame.SetposShift.ContainsKey(axis);
    }

    // The entry turns or mirrors the axis: a ROTATE the two axes of its working plane, a MIRROR the axes it names, a
    // TILT or TILT_AXIS every axis (language 4.2); a SHIFT turns none. A shift of an axis no entry turns acts the same
    // before and after the entry.
    private static bool Turns(TransformEntry entry, string axis)
    {
        return entry.Kind switch
        {
            TransformKind.Shift => false,
            TransformKind.Rotate => Array.IndexOf(HeidenhainAxes.PlaneAxes(entry.Workplane), axis) >= 0,
            TransformKind.Mirror => entry.Mirrored.Contains(axis),
            _ => true,
        };
    }

    // The setpos shift of the axis is UNKNOWN: set from an expression, or directly after a HOME without a reference
    // point (virtual machine 1, D101).
    private static bool ShiftUnknown(ChannelSnapshot state, string axis)
    {
        return state.Unknown.Contains(UnknownShiftKey + axis);
    }

    // The place of the cycle 7 of the setpos shifts as the target state keeps it; null where it does not know it.
    private static int? PlaceOf(HeidenhainBlock writing)
    {
        return int.TryParse(writing.Target.ActiveOf(PlaceKey), NumberStyles.None, CultureInfo.InvariantCulture,
            out int place)
            ? place
            : null;
    }
}
