using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// ORIGIN and the frame chain in Klartext (controllers heidenhain.md 3; controller-mapping 1; language 4.2; D31, D82):
/// ORIGIN=n as CYCL DEF 247 with Q339=n, SHIFT as cycle 7, MIRROR as cycle 8, ROTATE as cycle 10, TILT and TILT_AXIS as
/// PLANE SPATIAL and PLANE AXIAL through [transform] with the {move} of MOVE. What a block removed from the chain is
/// cancelled from the end, then what it appended is written in program order (D31, ChainWriter); SETPOS is folded into
/// cycle 7 after them (machine-config 3, HeidenhainSetpos).
/// </summary>
internal static class HeidenhainChain
{
    // The cycles that act where they stand, with the names the documents write (controllers heidenhain.md 1 and 3).
    private const string Origin = "CYCL DEF 247 INIT. REF.PKT";
    private const string Shift = "CYCL DEF 7";
    private const string ShiftName = " NULLPUNKT";
    private const string Mirror = "CYCL DEF 8";
    private const string MirrorName = " SPIEGELN";
    private const string Rotation = "CYCL DEF 10";
    private const string RotationName = " DREHUNG";

    // The words of the chain (language 4.2): the verbs SHIFT, TILT and TILT_AXIS with their axis words, the words
    // ROTATE and MIRROR, the RESET forms, and the options MOVE and ROT of a tilt.
    private static readonly string[] s_chainWords = ["SHIFT", "TILT", "TILT_AXIS", "ROTATE", "MIRROR", "MOVE", "ROT"];

    /// <summary>
    /// Writes ORIGIN and what the block did to the chain: the cancels of the entries it removed, the last first, cycle
    /// 247 for ORIGIN, then the entries it appended in program order (D31), then SETPOS as cycle 7, which acts after
    /// the other words of its block (virtual machine 3 step 4; machine-config 3).
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        TakeChainWords(writing);
        ChainChange change = ChainWriter.Between(writing.Before.Frame, writing.After.Frame);
        bool origin = writing.Block.Find("ORIGIN") is not null;
        if (!origin)
        {
            HeidenhainSetpos.CheckRemoved(writing, change);
        }

        // TODO(question): D253: whether cycle 247 ends an active cycle 7, 8, 10 or tilted plane on the control is
        // open; the compiler cancels every entry that ORIGIN removes from the chain before cycle 247, and the cycle 7
        // of the setpos shifts that ORIGIN clears (virtual machine 3.4), which holds whichever the answer. The chain
        // is unwound from the end, the cycle 7 of a SETPOS where it stands among the entries (D31).
        int? setposPlace = origin ? HeidenhainSetpos.PlaceBefore(writing) : null;
        int index = writing.Before.Frame.Chain.Count;
        foreach (TransformEntry removed in change.Removed)
        {
            index--;
            if (setposPlace is int place && index < place)
            {
                HeidenhainSetpos.Cancel(writing);
                setposPlace = null;
            }

            Cancel(writing, removed);
        }

        if (setposPlace is not null)
        {
            HeidenhainSetpos.Cancel(writing);
        }

        if (writing.Take("ORIGIN") is Word originWord)
        {
            WriteOrigin(writing, originWord);
        }

        foreach (TransformEntry appended in change.Appended)
        {
            Append(writing, appended);
        }

        HeidenhainSetpos.Write(writing, origin);
    }

    /// <summary>
    /// Writes cycle 7, the datum shift, with its axis values, CYCL DEF 7.0 NULLPUNKT, 7.1 X+60, 7.2 Y+40 (controllers
    /// heidenhain.md 3).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="axes">The axis values in their order: X+60.</param>
    public static void WriteDatumShift(HeidenhainBlock writing, List<string> axes)
    {
        writing.Line(Parts(Shift, ShiftName, axes));
    }

    /// <summary>
    /// Axes in the canonical order X Y Z A B C, then the machine axes by name (language 5 rule 6).
    /// </summary>
    public static List<string> SortedAxes(IEnumerable<string> axes)
    {
        string[] order = ["X", "Y", "Z", "A", "B", "C"];
        var sorted = new List<string>(axes);
        sorted.Sort((first, second) =>
        {
            int firstRank = Array.IndexOf(order, first) is int rank && rank >= 0 ? rank : order.Length;
            int secondRank = Array.IndexOf(order, second) is int other && other >= 0 ? other : order.Length;
            return firstRank != secondRank ? firstRank.CompareTo(secondRank) : string.CompareOrdinal(first, second);
        });
        return sorted;
    }

    // CYCL DEF 247 INIT. REF.PKT Q339=n activates the preset n, ORIGIN=n (controllers heidenhain.md 3;
    // controller-mapping 1, ORIGIN).
    private static void WriteOrigin(HeidenhainBlock writing, Word origin)
    {
        if (HeidenhainNumbers.NumberOf(origin.Value) is not decimal preset)
        {
            HeidenhainNumbers.ReportValue(writing, origin);
            return;
        }

        writing.Line(HeidenhainCycles.Continued(Origin, ["Q339=" + HeidenhainNumbers.Signed(writing, "Q339", preset)]));
    }

    // An appended entry, unless an entry of its kind still stands in the chain before it: cycles 7, 8 and 10 and a new
    // PLANE replace the one before them on the control (controllers heidenhain.md 3), so the chain of NCX would not be
    // the chain of the control (language 4.2, D31).
    // TODO(question): D253: where a replacing Klartext transform acts when others follow the one it replaces is open;
    // an entry appended while one of its kind stands in the chain is reported (CMP106), and so is a SHIFT while the
    // setpos shift of a SETPOS stands, whose cycle 7 it would replace (machine-config 3, HeidenhainSetpos).
    private static void Append(HeidenhainBlock writing, TransformEntry entry)
    {
        foreach (TransformEntry earlier in writing.After.Frame.Chain)
        {
            if (ReferenceEquals(earlier, entry))
            {
                break;
            }

            if (FamilyOf(earlier.Kind) == FamilyOf(entry.Kind))
            {
                writing.Error(DiagnosticCodes.HeidenhainTransformReplacesAnother,
                    $"The {entry.Kind.ToString().ToUpperInvariant()} of the block would stand in the chain after the "
                    + $"{earlier.Kind.ToString().ToUpperInvariant()} before it, and Klartext replaces that transform "
                    + "with the new one, so the chain of the control would not be the chain of the program "
                    + "(controllers heidenhain.md 3; language 4.2, D31, D253).");
                return;
            }
        }

        if (entry.Kind == TransformKind.Shift && HeidenhainSetpos.StandingAxes(writing.After).Count > 0)
        {
            writing.Error(DiagnosticCodes.HeidenhainTransformReplacesAnother,
                "The SHIFT of the block would stand in the chain while the setpos shift of a SETPOS stands, which the "
                + "compiler writes as cycle 7, and Klartext replaces that cycle with the new one, so the frame of the "
                + "control would not be the frame of the program (controllers heidenhain.md 3; machine-config 3; "
                + "language 4.2, D31, D253).");
            return;
        }

        switch (entry.Kind)
        {
            case TransformKind.Shift:
                WriteShift(writing, entry);
                break;
            case TransformKind.Mirror:
                WriteMirror(writing, entry.Mirrored);
                break;
            case TransformKind.Rotate:
                WriteRotation(writing, entry);
                break;
            default:
                WriteTilt(writing, entry);
                break;
        }
    }

    // The cancel of an entry: cycle 7 with every axis of its shift at 0, cycle 8 without axes, cycle 10 with ROT+0,
    // PLANE RESET through TILT_OFF (controllers heidenhain.md 3; machine-config 5).
    private static void Cancel(HeidenhainBlock writing, TransformEntry entry)
    {
        switch (entry.Kind)
        {
            case TransformKind.Shift:
                var axes = new List<string>();
                foreach (string axis in SortedAxes(entry.Shift.Keys))
                {
                    string letter = HeidenhainAxes.LetterOf(writing, axis);
                    axes.Add(letter + HeidenhainNumbers.Signed(writing, letter, 0m));
                }

                WriteDatumShift(writing, axes);
                break;
            case TransformKind.Mirror:
                WriteMirror(writing, []);
                break;
            case TransformKind.Rotate:
                writing.Line(Parts(Rotation, RotationName, ["ROT" + HeidenhainNumbers.Signed(writing, "ROT", 0m)]));
                break;
            default:
                writing.Template(writing.Machine.Transform?.TiltOff, "[transform] TILT_OFF (machine-config 5)",
                    new TemplateValues());
                break;
        }
    }

    // CYCL DEF 7.0 NULLPUNKT, 7.1 X+60, 7.2 Y+40, 7.3 Z-5: the datum shift axis by axis (controllers heidenhain.md 3).
    // A shift from an expression is written with the Q parameter of its word.
    private static void WriteShift(HeidenhainBlock writing, TransformEntry entry)
    {
        var axes = new List<string>();
        foreach (string axis in SortedAxes(entry.Shift.Keys))
        {
            string letter = HeidenhainAxes.LetterOf(writing, axis);
            string? value = entry.Shift[axis] is decimal shift
                ? HeidenhainNumbers.Signed(writing, letter, shift)
                : ExpressionOf(writing, axis);
            if (value is null)
            {
                return;
            }

            axes.Add(letter + value);
        }

        WriteDatumShift(writing, axes);
    }

    // CYCL DEF 8.0 SPIEGELN, 8.1 X Y: the mirrored axes; 8.1 without axes cancels (controllers heidenhain.md 3).
    private static void WriteMirror(HeidenhainBlock writing, IReadOnlyList<string> mirrored)
    {
        var letters = new List<string>();
        foreach (string axis in SortedAxes(mirrored))
        {
            letters.Add(HeidenhainAxes.LetterOf(writing, axis));
        }

        writing.Line(Mirror + ".0" + MirrorName + "\n" + Mirror + ".1" + (letters.Count > 0 ? " " : "")
            + string.Join(" ", letters));
    }

    // CYCL DEF 10.0 DREHUNG, 10.1 ROT+30: the rotation of the working plane (controllers heidenhain.md 3).
    private static void WriteRotation(HeidenhainBlock writing, TransformEntry entry)
    {
        Word? word = writing.Block.Find("ROTATE");
        string? angle = null;
        if (entry.Angle is decimal rotation)
        {
            angle = HeidenhainNumbers.Signed(writing, "ROT", rotation);
        }
        else if (word is not null)
        {
            angle = HeidenhainNumbers.SignedValue(writing, "ROT", word.Value);
        }

        if (angle is null)
        {
            if (word is not null)
            {
                HeidenhainNumbers.ReportValue(writing, word);
            }

            return;
        }

        writing.Line(Parts(Rotation, RotationName, ["ROT" + angle]));
    }

    // PLANE SPATIAL through TILT_ON, PLANE AXIAL through TILT_AXIS_ON, with the angles {a} {b} {c} and the {move} of
    // [transform] move for MOVE (controllers heidenhain.md 3; controller-mapping 1, TILT, TILT_AXIS, MOVE;
    // machine-config 5; D82). An empty TILT_AXIS_ON means the target writes TILT_AXIS only with the kinematics module
    // (D82).
    // TODO(question): the phase plan writes PLANE with MOVE and ROT from [transform], and machine-config 5 gives
    // [transform] a {move} but no placeholder for ROT; ROT=COORD is not written, with the WARNING of language 4.2 for a
    // target that has no such option.
    private static void WriteTilt(HeidenhainBlock writing, TransformEntry entry)
    {
        bool byAxes = entry.Kind == TransformKind.TiltAxis;
        string? template = byAxes ? writing.Machine.Transform?.TiltAxisOn : writing.Machine.Transform?.TiltOn;
        string what = byAxes
            ? "[transform] TILT_AXIS_ON (machine-config 5, D82)"
            : "[transform] TILT_ON (machine-config 5)";
        if (template?.Length == 0)
        {
            writing.Error(DiagnosticCodes.HeidenhainWordNeedsKinematics,
                $"The {what} of the machine is empty: the target writes TILT_AXIS only with the kinematics module, "
                + "which converts between TILT and TILT_AXIS (language 4.2, D82).");
            return;
        }

        var values = new TemplateValues();
        foreach (KeyValuePair<string, decimal?> angle in entry.Angles)
        {
            if (angle.Value is decimal degrees)
            {
                values.Set(angle.Key.ToLowerInvariant(), degrees);
            }
        }

        string move = entry.Move.ToString().ToUpperInvariant();
        if (writing.Machine.Transform?.Move.TryGetValue(move, out string? moveText) == true)
        {
            values.Set("move", moveText);
        }

        if (entry.Rot == TiltRot.Coord)
        {
            writing.Warning(DiagnosticCodes.HeidenhainRotNotWritten,
                "ROT=COORD is not written: the [transform] templates of the machine have no placeholder for it, and a "
                + "target that has no such option ignores it with a WARNING (language 4.2, D82; machine-config 5).");
        }

        writing.Template(template, what, values);
    }

    // The value of an axis of the SHIFT verb from its word, a Q parameter; null when it has no Klartext form, which is
    // reported.
    private static string? ExpressionOf(HeidenhainBlock writing, string axis)
    {
        foreach (HeidenhainAxisWord word in HeidenhainAxes.Of(writing.Block))
        {
            if (word.Axis != axis || word.Incremental)
            {
                continue;
            }

            string letter = HeidenhainAxes.LetterOf(writing, axis);
            string? value = HeidenhainNumbers.SignedValue(writing, letter, word.Word.Value);
            if (value is null)
            {
                HeidenhainNumbers.ReportValue(writing, word.Word);
            }

            return value;
        }

        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"The shift of {axis} is not known and has no word in the block (virtual machine 1).");
        return null;
    }

    // The words of the chain are written by the entries they appended or removed, or change nothing, a RESET of a kind
    // no entry of the chain has (language 4.2).
    private static void TakeChainWords(HeidenhainBlock writing)
    {
        foreach (string key in s_chainWords)
        {
            writing.TakeAll(key);
        }

        if (writing.Block.Verb?.Key is "SHIFT" or "TILT" or "TILT_AXIS")
        {
            foreach (HeidenhainAxisWord axis in HeidenhainAxes.Of(writing.Block))
            {
                writing.MarkWritten(axis.Word);
            }
        }
    }

    // A cycle over several blocks: n.0 with its name, then n.1, n.2 ... with one value each (controllers heidenhain.md
    // 3), or n.1 alone for a cycle whose values stand in one block.
    private static string Parts(string cycle, string name, List<string> values)
    {
        var lines = new List<string> { cycle + ".0" + name };
        for (int index = 0; index < values.Count; index++)
        {
            lines.Add(cycle + "." + (index + 1).ToString(CultureInfo.InvariantCulture) + " " + values[index]);
        }

        return string.Join("\n", lines);
    }

    // SHIFT, MIRROR and ROTATE each replace their own kind on the control; PLANE replaces both kinds of tilt.
    private static TransformKind FamilyOf(TransformKind kind)
    {
        return kind == TransformKind.TiltAxis ? TransformKind.Tilt : kind;
    }
}
