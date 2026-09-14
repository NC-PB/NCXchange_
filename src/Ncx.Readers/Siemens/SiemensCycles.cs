using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The cycles of a SINUMERIK program (controllers siemens.md 7, 11 rule 5; controller-mapping 5; language 4.7;
/// machine-config 6): a cycle call with its positional parameters through the entry of the cycle catalog into a CYCLE
/// block with the coordinates along the drilling axis absolute, SAFE the RTP it returns to, _AXN as AXIS, the modal F
/// as CYCLE_F; MCALL CYCLE81(...) makes it modal and every following block with a position calls it, CYCLE_CALL, MCALL
/// alone is CYCLE=OFF; a direct CYCLE81(...) is CYCLE= and one CYCLE_CALL at the current position; the patterns HOLES1
/// and HOLES2 are SiemensPatterns.
/// </summary>
internal static class SiemensCycles
{
    // The trailing mode integers the 840D sl added; omitted they mean the old behaviour (controllers siemens.md 7).
    private static readonly string[] s_modes = ["_GMODE", "_DMODE", "_AMODE"];

    /// <summary>
    /// Reads MCALL and the cycle calls of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (block.Take("MCALL") is not null)
        {
            ReadModalCall(block);
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (!word.Text.StartsWith('('))
            {
                continue;
            }

            if (word.Address is "HOLES1" or "HOLES2")
            {
                SiemensPatterns.Read(block, word);
                return;
            }

            if (word.Address is "CYCLE801" or "CYCLE802")
            {
                // TODO(question): siemens 7 names CYCLE801 and CYCLE802 without their signatures; they stay RAW.
                block.MarkRead(word);
                block.Draft.KeepAsRaw($"{word.Address} is a pattern whose parameters the documents do not give "
                    + "(controllers siemens.md 7)");
                return;
            }

            if (block.Machine.CycleCatalog.FindNative(word.Address) is not null)
            {
                ReadDirectCall(block, word);
                return;
            }
        }
    }

    /// <summary>
    /// A block with a position under MCALL calls the modal cycle there: CYCLE_CALL with its axis words (controllers
    /// siemens.md 7; controller-mapping 5, CYCLE_CALL).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axes">The axis words of the block.</param>
    public static void CallAt(SiemensBlock block, List<SourceWord> axes)
    {
        SiemensCycle cycle = block.Facts.ModalCycle!;
        if (block.MachineFrame is not null)
        {
            block.Draft.KeepAsRaw("a position in the machine frame under MCALL is not read as a call of the cycle");
            return;
        }

        SiemensDraftBlock main = block.Draft.Main.WithVerb("CYCLE_CALL");
        foreach (SourceWord word in axes)
        {
            if (!SiemensMotion.AddAxis(block, main, word))
            {
                return;
            }
        }

        // The F of a position block is the feed of the control, which the cycle and the next feed motion use
        // (controller-mapping 5, CYCLE_F).
        if (block.Take("F") is SourceWord feed && SiemensExpression.ValueOf(block, feed.Text, out _) is Value value)
        {
            block.Facts.ControlFeed = value;
        }

        Returned(block, cycle);
    }

    /// <summary>
    /// True when the next block defines a cycle that takes the modal F as its feed, MCALL CYCLE81(...) or a direct
    /// call: the F before it is the CYCLE_F (controller-mapping 5, CYCLE_F).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static bool DefinesNext(SiemensBlock block)
    {
        SourceBlock? next = block.Siemens.Units.NextInUnit(block.Source);
        if (next is null)
        {
            return false;
        }

        foreach (SourceWord word in next.Words)
        {
            if (word.Text.StartsWith('(') && block.Machine.CycleCatalog.FindNative(word.Address) is CycleEntry entry
                && entry.CycleF == "F" && entry.IsAddressWord("CYCLE_F"))
            {
                return true;
            }
        }

        return false;
    }

    // MCALL CYCLE81(...) makes the cycle modal, MCALL alone ends it; MCALL with another subprogram has no NCX word
    // (controllers siemens.md 7; controller-mapping 5, CYCLE_CALL and CYCLE=OFF).
    private static void ReadModalCall(SiemensBlock block)
    {
        SourceWord? call = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address is not ("N" or ":"))
            {
                call = word;
                break;
            }
        }

        SiemensFacts facts = block.Facts;
        if (call is null)
        {
            block.Draft.AddState("CYCLE", null, new IdentValue("OFF"));
            facts.ModalCycle = null;
            facts.Unknown.Remove(SiemensFacts.Cycle);
            return;
        }

        block.MarkRead(call);
        if (!call.Text.StartsWith('(') || block.Machine.CycleCatalog.FindNative(call.Address) is null)
        {
            block.Draft.KeepAsRaw($"MCALL {call.Address} makes a subprogram modal, which NCX has no word for; "
                + "CYCLE_CALL calls a cycle (controllers siemens.md 7)");
            return;
        }

        if (Define(block, call) is not SiemensCycle cycle)
        {
            return;
        }

        foreach (Word word in cycle.Words)
        {
            block.Draft.Main.Add(word.Key, word.Addr, word.Value);
        }

        facts.ModalCycle = cycle;
        facts.Unknown.Remove(SiemensFacts.Cycle);
    }

    // CYCLE81(...) alone calls the cycle once at the current position: CYCLE= and a CYCLE_CALL (controller-mapping 5,
    // CYCLE_CALL).
    // TODO(question): wave-2 question #74, whether a cycle called once is closed with CYCLE=OFF; as controller-mapping
    // 5 writes the direct call, it is CYCLE= and one CYCLE_CALL, without CYCLE=OFF.
    private static void ReadDirectCall(SiemensBlock block, SourceWord call)
    {
        block.MarkRead(call);
        if (Define(block, call) is not SiemensCycle cycle)
        {
            return;
        }

        foreach (Word word in cycle.Words)
        {
            block.Draft.Main.Add(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.Add(new SiemensDraftBlock().WithVerb("CYCLE_CALL"));
        block.Facts.ModalCycle = null;
        Returned(block, cycle);
    }

    /// <summary>
    /// The CYCLE block of a call through its catalog entry: CYCLE=name, the words its parameters map, CLEARANCE = RFP
    /// + SDIS, DEPTH = DP or RFP + DPR, SAFE = RTP with CYCLE_RETRACT=SAFE, AXIS from _AXN, CYCLE_F from the modal F;
    /// null, with the block kept RAW, where the reader cannot read the call (controller-mapping 5; machine-config 6).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="call">The call word with its arguments.</param>
    public static SiemensCycle? Define(SiemensBlock block, SourceWord call)
    {
        CycleCatalog catalog = block.Machine.CycleCatalog;
        List<string> arguments = SiemensArguments.Split(call.Text);
        IReadOnlyList<string> signature = catalog.FindNative(call.Address)!.Signature;
        var numbers = new Dictionary<string, decimal>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count && index < signature.Count; index++)
        {
            if (SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) is decimal number)
            {
                numbers[signature[index]] = number;
            }
        }

        // TODO(question): D181, the default of a position the source leaves empty, VARI of CYCLE83 among them, and the
        // meaning of the trailing mode integers; a call that leaves a fixed value empty, or sets a mode, stays RAW.
        CycleEntry? entry = catalog.FindNative(call.Address, numbers);
        string? problem = entry is null
            ? $"{call.Address} leaves a value empty that tells its NCX cycle, VARI of CYCLE83 (D181)"
            : arguments.Count > entry.Signature.Count
                ? $"{call.Address} has more parameters than the signature of its catalog entry"
                : ModeSet(entry, arguments);
        if (problem is not null)
        {
            block.Draft.KeepAsRaw(problem);
            return null;
        }

        var reading = new CycleReading(block, entry!, arguments);
        return reading.Words();
    }

    // The drilling axis stands at the plane the cycle returns to after a call (virtual machine 3.3).
    private static void Returned(SiemensBlock block, SiemensCycle cycle)
    {
        if (cycle.ReturnPlane is decimal plane)
        {
            block.State.SetPosition(cycle.Axis, plane);
        }
        else
        {
            block.State.ForgetPosition(cycle.Axis);
        }

        block.Facts.Tangent = null;
    }

    private static string? ModeSet(CycleEntry entry, List<string> arguments)
    {
        foreach (string mode in s_modes)
        {
            int index = IndexOf(entry.Signature, mode);
            if (index >= 0 && index < arguments.Count && arguments[index].Length > 0
                && SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) != 0)
            {
                return $"{mode} of {entry.Native} sets a mode whose meaning the documents do not give (D181)";
            }
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<string> list, string name)
    {
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index] == name)
            {
                return index;
            }
        }

        return -1;
    }

    // The reading of one call against its entry.
    private sealed class CycleReading(SiemensBlock block, CycleEntry entry, List<string> arguments)
    {
        private readonly List<Word> _words = [new() { Key = "CYCLE", Value = new IdentValue(entry.Name) }];
        private readonly HashSet<string> _used = new(StringComparer.Ordinal);

        public SiemensCycle? Words()
        {
            Value? surface = Argument("SURFACE");
            if (surface is null)
            {
                return Raw($"{entry.Native} gives no reference plane RFP, which its coordinates count from");
            }

            foreach (KeyValuePair<string, string> parameter in entry.Params)
            {
                if (!Add(parameter.Key, parameter.Value, surface))
                {
                    return null;
                }
            }

            // The cycle always returns to RTP: CYCLE_RETRACT=SAFE with SAFE = RTP (controller-mapping 5).
            // TODO(question): D157, which catalog entries carry the rule words; an entry that maps SAFE to RTP carries
            // CYCLE_RETRACT, as D157 recommends.
            if (entry.NativeOf("SAFE") == "RTP")
            {
                _words.Add(new Word { Key = "CYCLE_RETRACT", Value = new IdentValue("SAFE") });
            }

            WarnNotCarried();
            string axis = AxisName() ?? SiemensPlane.Of(block.Facts.WorkingPlane).Tool;
            return new SiemensCycle
            {
                Native = entry.Native,
                Words = _words,
                Axis = axis,
                ReturnPlane = SiemensNumbers.NumberOf(Find("SAFE")),
            };
        }

        private bool Add(string word, string native, Value surface)
        {
            _used.Add(native);
            if (word == "CYCLE_F" && entry.IsAddressWord(word))
            {
                // The modal F before the call is the feed of the cycle (controller-mapping 5, CYCLE_F; D29).
                if (block.Facts.ControlFeed is Value feed)
                {
                    _words.Add(new Word { Key = word, Value = feed });
                }

                return true;
            }

            Value? value = word == "SURFACE" ? surface : Argument(word);
            if (word == "DEPTH" && value is null)
            {
                value = Relative(surface);
            }

            if (value is null)
            {
                if (word is "CLEARANCE" or "SAFE" or "DEPTH")
                {
                    Raw($"{entry.Native} leaves {native} empty, whose default the documents do not give (D181)");
                    return false;
                }

                return true;
            }

            if (entry.AbsoluteFromSurface.Contains(word))
            {
                value = Sum(surface, value);
            }
            else if (word == "AXIS")
            {
                value = AxisValue(value);
            }
            else if (word == "CYCLE_DWELL")
            {
                value = Dwell(value);
            }

            if (value is null)
            {
                return false;
            }

            _words.Add(new Word { Key = word, Value = value });
            return true;
        }

        // DEPTH is DP, or RFP + DPR where the source gives the incremental DPR (controller-mapping 5).
        // TODO(question): controller-mapping 5 writes DEPTH = RFP + DPR, while DPR is given without a sign on the
        // control for a hole below the reference plane (to confirm with the programming manual, as D181); the reader
        // follows controller-mapping 5.
        private Value? Relative(Value surface)
        {
            int index = IndexOf(entry.Signature, "DPR");
            if (index < 0 || index >= arguments.Count || arguments[index].Length == 0)
            {
                return null;
            }

            _used.Add("DPR");
            Value? relative = SiemensExpression.ValueOf(block, arguments[index], out _);
            return relative is null ? null : Sum(surface, relative);
        }

        // _AXN 1, 2, 3 are the first, second and third geometry axis of the plane (controller-mapping 5, AXIS; D59).
        private IdentValue? AxisValue(Value value)
        {
            var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
            string? axis = SiemensNumbers.NumberOf(value) switch
            {
                1 => plane.First,
                2 => plane.Second,
                3 => plane.Tool,
                _ => null,
            };
            if (axis is null)
            {
                Raw($"_AXN of {entry.Native} names no geometry axis 1, 2 or 3 (controller-mapping 5, AXIS)");
                return null;
            }

            return new IdentValue(axis);
        }

        // DTB is positive seconds, negative revolutions, which the reader converts with the speed it knows
        // (controller-mapping 5, CYCLE_DWELL).
        private Value? Dwell(Value value)
        {
            if (SiemensNumbers.NumberOf(value) is not decimal dwell || dwell >= 0)
            {
                return value;
            }

            string? role = SiemensSpindles.MasterRole(block);
            if (role is null || !block.Facts.Rpm.TryGetValue(role, out decimal rpm) || rpm <= 0)
            {
                Raw($"DTB of {entry.Native} dwells by revolutions of a spindle whose speed the reader does not know");
                return null;
            }

            return SiemensNumbers.Of(Math.Round(-dwell * 60m / rpm, 3, MidpointRounding.AwayFromZero));
        }

        private string? AxisName()
        {
            return Find("AXIS") is IdentValue axis ? axis.Name : null;
        }

        private Value? Find(string key)
        {
            foreach (Word word in _words)
            {
                if (word.Key == key)
                {
                    return word.Value;
                }
            }

            return null;
        }

        // The value of the position a word maps; null for an empty or missing position.
        private Value? Argument(string word)
        {
            string? native = entry.NativeOf(word);
            int index = native is null ? -1 : IndexOf(entry.Signature, native);
            if (index < 0 || index >= arguments.Count || arguments[index].Length == 0)
            {
                return null;
            }

            Value? value = SiemensExpression.ValueOf(block, arguments[index], out string? problem);
            if (value is null)
            {
                Raw($"{native}={arguments[index]} of {entry.Native} has no NCX value: {problem}");
            }

            return value;
        }

        private Value? Sum(Value surface, Value relative)
        {
            if (SiemensNumbers.NumberOf(surface) is decimal from && SiemensNumbers.NumberOf(relative) is decimal by)
            {
                return SiemensNumbers.Of(from + by);
            }

            return SiemensExpression.Parse(block, "(" + TextOf(surface) + ") + (" + TextOf(relative) + ")", out _);
        }

        // A position no word of the entry carries is not written, with a WARNING (D164; wave-2 question #79).
        private void WarnNotCarried()
        {
            var unmapped = new List<string>();
            for (int index = 0; index < arguments.Count && index < entry.Signature.Count; index++)
            {
                string native = entry.Signature[index];
                if (arguments[index].Length > 0 && !_used.Contains(native) && !entry.Fixed.ContainsKey(native)
                    && Array.IndexOf(s_modes, native) < 0)
                {
                    unmapped.Add(native);
                }
            }

            if (unmapped.Count > 0)
            {
                block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensValueNotCarried,
                    $"{string.Join(", ", unmapped)} of {entry.Native} {(unmapped.Count == 1 ? "has" : "have")} no NCX "
                    + $"word in the catalog entry {entry.Name} and {(unmapped.Count == 1 ? "is" : "are")} not written "
                    + "(controllers siemens.md 7, machine-config 6)."));
            }
        }

        private SiemensCycle? Raw(string reason)
        {
            block.Draft.KeepAsRaw(reason);
            return null;
        }

        private static string TextOf(Value value)
        {
            return value is ExprValue expression ? expression.Text : value.ToCanonical();
        }
    }
}
