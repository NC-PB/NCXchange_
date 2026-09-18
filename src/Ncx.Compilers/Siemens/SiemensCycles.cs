using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The cycles of a SINUMERIK block (controllers siemens.md 7, 12 rule 5; controller-mapping 5; machine-config 6;
/// language 4.7): the call with the full signature of the catalog entry, the absolute planes of NCX back to RFP, SDIS
/// and DP, RTP the plane the tool returns to, AXIS as _AXN, the fixed values, CYCLE_F as the modal F in the block
/// before; MCALL for the calls at positions, which every following block with a position repeats, MCALL alone to end
/// it before a motion that is no call and for CYCLE=OFF; a direct call at the current position.
/// </summary>
internal static partial class SiemensCycles
{
    // The target key of the modal call the control has active, OFF or the call (controllers siemens.md 7).
    private const string ModalCall = "MCALL";
    private const string Off = "OFF";

    // The words of a cycle definition (language 4.7).
    private static readonly string[] s_cycleWords =
    [
        "CYCLE", "AXIS", "SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_RETRACT", "PECK", "CYCLE_F", "CYCLE_DWELL",
        "PITCH", "CONTOUR",
    ];

    // The verbs that move the tool, which a modal call would repeat at their positions (language 4.3).
    private static readonly string[] s_motions = ["RAPID", "LINE", "ARC", "HOME", "RETRACT"];

    // The planes along the drilling axis, absolute coordinates (language 4.7).
    private static readonly string[] s_planes = ["SURFACE", "CLEARANCE", "DEPTH", "SAFE"];

    /// <summary>
    /// What stands before the main line of the block: MCALL alone where a modal call is active and the block moves the
    /// tool without calling the cycle, calls a subprogram or ends the cycle (CYCLE=OFF), and, for a CYCLE_CALL at a
    /// position, the modal F of the cycle and MCALL with the call where the control has not that call active
    /// (controllers siemens.md 7; controller-mapping 5, CYCLE_CALL, CYCLE=OFF and CYCLE_F).
    /// </summary>
    public static void WriteBefore(SiemensBlock write)
    {
        Block block = write.Block;
        if (block.Find("CYCLE") is Word cycle)
        {
            WriteDefinition(write, cycle);
        }

        bool moves = block.Verb is Word verb && s_motions.Contains(verb.Key);
        if (moves || block.Has("CALL") || block.Has("CYCLE", null, "OFF"))
        {
            EndModalCall(write);
        }

        if (block.Verb?.Key != "CYCLE_CALL")
        {
            return;
        }

        write.Written("CYCLE_CALL");
        List<SiemensAxisWord> axes = SiemensAxes.Of(block);
        if (axes.Count == 0)
        {
            return;
        }

        if (CallOf(write) is not string call)
        {
            // The ERROR of the call stands alone for its block.
            write.WrittenAll();
            return;
        }

        // Armed again where the control has another call or may have none: an unknown modal call, after a label or a
        // RAW that names MCALL, is armed again as well.
        if (write.ActiveOf(ModalCall) != call)
        {
            WriteCycleFeed(write);
            write.Write("MCALL " + call);
            write.Target.Set(ModalCall, call);
        }

        // The block with the position calls the modal cycle there, at rapid in the plane (virtual machine 3.3).
        SiemensMotion.Change(write, "G0", SiemensLine.MotionRank, "G0");
        SiemensMotion.WriteDistance(write);
        SiemensMotion.WriteAxes(write, axes);
    }

    /// <summary>
    /// What stands after the main line: the direct call of a CYCLE_CALL without axis words, once at the current
    /// position (controllers siemens.md 7; controller-mapping 5, CYCLE_CALL).
    /// </summary>
    public static void WriteAfter(SiemensBlock write)
    {
        if (write.Block.Verb?.Key != "CYCLE_CALL" || SiemensAxes.Of(write.Block).Count > 0)
        {
            return;
        }

        if (CallOf(write) is string call)
        {
            WriteCycleFeed(write);
            write.Write(call);
        }
    }

    /// <summary>
    /// MCALL alone where the control has a modal call active or may have one: a modal call the target state does not
    /// know, after a label or a RAW that names MCALL, may be armed, and every following block with a position would
    /// call it until MCALL alone switches it off (controllers siemens.md 7; controller-mapping 5, CYCLE=OFF).
    /// </summary>
    public static void EndModalCall(SiemensBlock write)
    {
        if (write.ActiveOf(ModalCall) != Off)
        {
            write.Write("MCALL");
            write.Target.Set(ModalCall, Off);
        }
    }

    /// <summary>
    /// A program starts without a modal call, as the cycle of the virtual machine starts OFF (virtual machine 2.6); a
    /// subprogram as well, since every call of this compiler stands after MCALL alone where a modal call may be armed
    /// (SiemensFlow, the calls), so that no caller of the file enters it with one (controllers siemens.md 7; D99).
    /// </summary>
    public static void BeginUnit(SiemensBlock write)
    {
        write.Target.Set(ModalCall, Off);
    }

    /// <summary>
    /// True where the control has no modal call active, as far as the target state knows.
    /// </summary>
    public static bool ModalCallOff(SiemensBlock write)
    {
        return write.ActiveOf(ModalCall) == Off;
    }

    /// <summary>
    /// Records that the control has no modal call active.
    /// </summary>
    public static void ModalCallEnded(SiemensBlock write)
    {
        write.Target.Set(ModalCall, Off);
    }

    /// <summary>
    /// After a RAW line: a RAW that names MCALL may arm a modal call or end one, so the modal call is unknown after it;
    /// any other RAW leaves it as MCALL alone before the RAW left it (controllers siemens.md 7, 8: MCALL makes a call
    /// modal and MCALL alone ends it).
    /// </summary>
    // TODO(question): the documents do not say whether a RAW line without MCALL, through a subprogram it calls (a
    // builder cycle), can leave a modal call armed after its return; the modal call stays off after such a RAW, since
    // MCALL alone after every RAW would stand before the next motion after every MSG and STOPRE of a program
    // (controllers sample-corpus.md: MSG before every operation), until that is answered.
    public static void AfterRaw(SiemensBlock write, string raw)
    {
        if (McallWord().IsMatch(raw))
        {
            write.MakeUnknown(ModalCall);
        }
    }

    // A definition writes nothing of its own: the call writes it, where the control needs it (language 4.7).
    private static void WriteDefinition(SiemensBlock write, Word cycle)
    {
        foreach (string key in s_cycleWords)
        {
            write.Written(key);
        }

        if (cycle.Addr is null)
        {
            return;
        }

        // The native parameters of a CYCLE:SIEMENS=n block belong to the definition (language 4.7.1, D94).
        foreach (Word word in write.Block.Words)
        {
            if (word.Definition is null)
            {
                write.Written(word);
            }
        }
    }

    // The modal F before the call is the feed of the cycles that take it, the CYCLE_F of the definition or the program
    // feed where it has none (controllers siemens.md 7; controller-mapping 5, CYCLE_F; machine-config 6).
    private static void WriteCycleFeed(SiemensBlock write)
    {
        CycleSnapshot cycle = write.After.Cycle;
        CycleEntry? entry = cycle.Controller is null ? write.Machine.CycleCatalog.Find(cycle.Name ?? "") : null;
        if (entry is null || entry.CycleF is null || !entry.IsAddressWord("CYCLE_F"))
        {
            return;
        }

        string? feed = null;
        if (Parameter(cycle, "CYCLE_F") is Value value)
        {
            feed = SiemensExpressions.ValueText(write, value, "F");
        }
        else if (write.After.Motion.Feed is decimal known)
        {
            feed = write.Format("F", known);
        }

        if (feed is not null && write.Target.Changes("F", feed))
        {
            write.Write(SiemensAxes.Address("F", feed, "", Parameter(cycle, "CYCLE_F") is ExprValue));
        }
    }

    // The call of the active cycle with its positional parameters: CYCLE81(RTP, RFP, SDIS, DP, ...); null, with an
    // ERROR, where the catalog has no entry or a value the call needs is missing.
    private static string? CallOf(SiemensBlock write)
    {
        CycleSnapshot cycle = write.After.Cycle;
        if (cycle.Name is not string name)
        {
            return null;
        }

        if (cycle.Controller is not null)
        {
            return NativeCallOf(write, cycle, name);
        }

        if (write.Machine.CycleCatalog.Find(name) is not CycleEntry entry || entry.Signature.Count == 0)
        {
            write.Error(DiagnosticCodes.SiemensCycleNotInCatalog,
                $"CYCLE={name} has no entry with a signature in the cycle catalog of the machine (language 4.7.1; "
                + "machine-config 6).");
            return null;
        }

        var positions = new string?[entry.Signature.Count];
        return Fill(write, cycle, entry, positions) ? Call(entry.Native, positions) : null;
    }

    // The words of the definition at the positions of the signature (machine-config 6; controller-mapping 5).
    private static bool Fill(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry, string?[] positions)
    {
        Value? surface = Parameter(cycle, "SURFACE");
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key is "CYCLE_RETRACT" or "SAFE"
                || (word.Key == "CYCLE_F" && entry.IsAddressWord("CYCLE_F")))
            {
                continue;
            }

            if (entry.NativeOf(word.Key) is not string native || Index(entry, native) is not int index)
            {
                // TODO(question): D181: the documents do not say how PECK is written with FDEP, FDPR and _DAM of
                // CYCLE83, and the catalog maps it to no position; a word no position carries is an ERROR, until D181
                // is answered.
                write.Error(DiagnosticCodes.SiemensCycleWordNotWritable,
                    $"{word.ToCanonical()} has no position in {entry.Native} of the cycle catalog, and no rule of the "
                    + "family carries it (machine-config 6; controllers siemens.md 12 rule 5; D181).");
                return false;
            }

            positions[index] = ValueOf(write, cycle, entry, word, surface);
        }

        if (!FillReturnPlane(write, cycle, entry, positions) || !FillAxis(write, cycle, entry, positions))
        {
            return false;
        }

        foreach (KeyValuePair<string, decimal> fixedValue in entry.Fixed)
        {
            if (Index(entry, fixedValue.Key) is int index)
            {
                positions[index] = fixedValue.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        return true;
    }

    // The cycle always returns to RTP: RTP = SAFE for CYCLE_RETRACT=SAFE, RTP = CLEARANCE for CYCLE_RETRACT=CLEARANCE
    // (controller-mapping 5, CYCLE_RETRACT).
    // TODO(question): D188: without SAFE, CYCLE_RETRACT=SAFE gives RTP no value; the compiler, which must write it,
    // reports the ERROR, as D188 recommends, until D188 is answered.
    private static bool FillReturnPlane(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry, string?[] positions)
    {
        if (entry.NativeOf("SAFE") is not string native || Index(entry, native) is not int index)
        {
            return true;
        }

        bool safe = Parameter(cycle, "CYCLE_RETRACT")?.ToCanonical() == "SAFE";
        string plane = safe ? "SAFE" : "CLEARANCE";
        if (Parameter(cycle, plane) is not Value value)
        {
            write.Error(DiagnosticCodes.SiemensCycleValueMissing,
                $"CYCLE_RETRACT={(safe ? "SAFE" : "CLEARANCE")} returns to {plane}, which the cycle does not give, and "
                + $"{native} of {entry.Native} needs it (controller-mapping 5, CYCLE_RETRACT; D188).");
            return false;
        }

        positions[index] = PlaneText(write, cycle, value);
        return true;
    }

    // AXIS is _AXN, 1, 2, 3 the first, second and third geometry axis of the plane, where the entry maps it; an empty
    // _AXN, and an entry without it, drill along the tool axis of the plane (controller-mapping 5, AXIS; D59).
    private static bool FillAxis(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry, string?[] positions)
    {
        string[] plane = write.After.Frame.Workplane switch
        {
            Workplane.ZX => ["Z", "X", "Y"],
            Workplane.YZ => ["Y", "Z", "X"],
            _ => ["X", "Y", "Z"],
        };
        int number = Array.IndexOf(plane, cycle.Axis) + 1;
        if (number == plane.Length)
        {
            return true;
        }

        if (number > 0 && entry.NativeOf("AXIS") is string native && Index(entry, native) is int index)
        {
            positions[index] = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        write.Error(DiagnosticCodes.SiemensCycleValueMissing,
            $"The cycle drills along {cycle.Axis}, and {entry.Native} drills along the tool axis of the plane or "
            + "another geometry axis by _AXN only (controller-mapping 5, AXIS; D59).");
        return false;
    }

    // A word of the definition as the native value: a plane absolute along the drilling axis, CLEARANCE as SDIS from
    // the reference plane (machine-config 6, absolute_from_surface; controller-mapping 5).
    private static string? ValueOf(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry, Word word,
        Value? surface)
    {
        if (entry.AbsoluteFromSurface.Contains(word.Key) && surface is not null)
        {
            if (Number(word.Value) is decimal ncx && Number(surface) is decimal reference)
            {
                return write.Format(DecimalsAddress(write, cycle), entry.ToNative(word.Key, ncx, reference));
            }

            string? value = SiemensExpressions.ValueText(write, word.Value, "SDIS");
            string? from = SiemensExpressions.ValueText(write, surface, "SDIS");
            return value is null || from is null ? null : "(" + value + ")-(" + from + ")";
        }

        if (s_planes.Contains(word.Key))
        {
            return PlaneText(write, cycle, word.Value);
        }

        return SiemensExpressions.ValueText(write, word.Value, word.Key);
    }

    // A plane along the drilling axis with the factor of that axis, the diameter of X or the Z of a side whose datum
    // runs the other way (language 4.7; machine-config 5).
    private static string? PlaneText(SiemensBlock write, CycleSnapshot cycle, Value value)
    {
        return SiemensAxes.ValueOf(write, cycle.Axis, value, DecimalsAddress(write, cycle));
    }

    private static string DecimalsAddress(SiemensBlock write, CycleSnapshot cycle)
    {
        return SiemensAxes.DecimalsAddress(write, SiemensAxes.LetterOf(write, cycle.Axis));
    }

    // A native cycle of CYCLE:SIEMENS=n with its native parameters: CYCLE and the number, the parameters at the
    // positions of the signature where the catalog has the cycle, else in source order (language 4.7.1, D94).
    // TODO(question): D144: the form of n in CYCLE:SIEMENS=n is open, and the parser takes an integer; n is the number
    // of CYCLEn, until D144 is answered.
    private static string? NativeCallOf(SiemensBlock write, CycleSnapshot cycle, string name)
    {
        string native = IsDigits(name) ? "CYCLE" + name : name;
        CycleEntry? entry = write.Machine.CycleCatalog.FindNative(native);
        if (entry is null || entry.Signature.Count == 0)
        {
            var values = new List<string?>();
            foreach (Word word in cycle.Parameters)
            {
                values.Add(SiemensExpressions.ValueText(write, word.Value, word.Key));
            }

            return Call(native, values.ToArray());
        }

        var positions = new string?[entry.Signature.Count];
        foreach (Word word in cycle.Parameters)
        {
            if (Index(entry, word.Key) is not int index)
            {
                write.Error(DiagnosticCodes.SiemensCycleWordNotWritable,
                    $"{word.ToCanonical()} is no parameter of the signature of {native} in the cycle catalog "
                    + "(machine-config 6; language 4.7.1, D94).");
                return null;
            }

            positions[index] = SiemensExpressions.ValueText(write, word.Value, word.Key);
        }

        return Call(native, positions);
    }

    // NAME(p1,p2,,p4): the positions up to the last with a value, an empty one takes the default of the cycle
    // (controllers siemens.md 7; machine-config 6, signature).
    private static string Call(string native, string?[] positions)
    {
        int last = positions.Length - 1;
        while (last >= 0 && positions[last] is null)
        {
            last--;
        }

        var values = new List<string>();
        for (int index = 0; index <= last; index++)
        {
            values.Add(positions[index] ?? "");
        }

        return native + "(" + string.Join(",", values) + ")";
    }

    private static Value? Parameter(CycleSnapshot cycle, string key)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == key)
            {
                return word.Value;
            }
        }

        return null;
    }

    private static int? Index(CycleEntry entry, string native)
    {
        for (int index = 0; index < entry.Signature.Count; index++)
        {
            if (entry.Signature[index] == native)
            {
                return index;
            }
        }

        return null;
    }

    private static decimal? Number(Value value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    // MCALL as a word of a line, no part of a longer name; names are case-insensitive (controllers siemens.md 1).
    [GeneratedRegex("(?<![A-Za-z0-9_$])MCALL(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex McallWord();
}
