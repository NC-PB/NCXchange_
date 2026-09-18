using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The canned cycles of a Fanuc block (controllers fanuc.md 6, 9 rule 5; controller-mapping 5; language 4.7, 4.7.1;
/// machine-config 6): G73 to G89 define the cycle and call it at the position of their block, every following block
/// with a position calls it again, G80 cancels; R and Z become the absolute CLEARANCE and DEPTH, G98/G99 CYCLE_RETRACT,
/// K repeats expand into CYCLE_CALL blocks; on a lathe G83 to G85 drill along Z and G87 to G89 along X (AXIS), the
/// simple turning cycles of system A come from the catalog, and G70 to G76 are the catalog cycles with the contour
/// that follows them as a SUB section, CONTOUR=name (D65).
/// </summary>
internal static class FanucCycles
{
    // The most repeats K the reader expands, four digits.
    private const int MaxRepeats = 9999;

    // The canned cycles of a mill, group 09 (controllers fanuc.md 3, 6).
    private static readonly string[] s_millCycles =
        ["G73", "G74", "G76", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89"];

    // The drilling cycles of a lathe, along Z on the face and along X on the circumference (controllers fanuc.md 6).
    private static readonly string[] s_latheDrilling = ["G83", "G84", "G85", "G87", "G88", "G89"];

    // The simple turning cycles: G90, G92, G94 in system A, G77, G78, G79 in systems B and C (controllers fanuc.md 3).
    private static readonly string[] s_turningOfSystemA = ["G90", "G92", "G94"];
    private static readonly string[] s_turningOfSystemsBAndC = ["G77", "G78", "G79"];

    // The multiple repetitive cycles of a lathe, one-shot with the contour range P Q (controllers fanuc.md 6).
    private static readonly string[] s_repetitive = ["G70", "G71", "G72", "G73", "G74", "G75", "G76"];

    private static readonly string[] s_motion = ["G0", "G1", "G2", "G3"];
    private static readonly IntegerValue s_zero = new(0, "0");

    /// <summary>
    /// Reads the cycle words of a block: the end of a cycle, a new cycle, its modal values and its calls.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw || ReadRepetitiveCycle(block))
        {
            return;
        }

        bool returnLevel = ReadReturnLevel(block);
        SourceWord? code = CycleCode(block);

        // The active cycle of the caller of a subprogram, or the one a called subprogram left, may be unknown to the
        // reader (virtual machine 3.9; FanucCallerState): a cycle code, G80 or a code that ends every cycle decides it,
        // and a block with a position before that, which calls the cycle where one is active, stays RAW.
        bool unknownEnds = false;
        if (block.Fanuc.Unknowns.Cycle)
        {
            if (code is null && !block.HasCode("G80") && !EndsEveryCycle(block))
            {
                if (FanucAxes.Unread(block).Count > 0)
                {
                    block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                        "the active cycle, which every following block with a position calls "
                        + "(controllers fanuc.md 6)"));
                }

                return;
            }

            block.Fanuc.Unknowns.Cycle = false;
            unknownEnds = code is null;
        }

        bool cancels = block.TakeCode("G80");
        if (cancels || unknownEnds || Ends(block, code))
        {
            End(block);
        }

        if (code is not null)
        {
            Start(block, code);
        }

        FanucCycle? cycle = block.Fanuc.Cycle;
        if (cycle is null)
        {
            return;
        }

        if (returnLevel)
        {
            cycle.ReturnsToInitialLevel = block.State.ActiveCode(FanucModalGroups.ReturnLevel) == "G98";
        }

        if (cycle.Entry is null)
        {
            if (code is not null || Positions(block, cycle).Count > 0)
            {
                block.Draft.KeepAsRaw(
                    $"{cycle.Code} has no cycle in the catalog of the machine (machine-config 6); its blocks stay RAW");
            }

            return;
        }

        if (cycle.IsTurning)
        {
            ReadTurning(block, cycle, code is not null);
        }
        else
        {
            ReadDrilling(block, cycle, code is not null);
        }
    }

    /// <summary>
    /// The contour a G70 to G76 block of a lathe names with the two contour words of its catalog entry, P and Q, the
    /// labels of the first and the last block of the contour (machine-config 6, contour; language 4.7.1; D65), when
    /// the reader reads the block whole: its code, the two labels, and the words its entry maps as numbers; null
    /// otherwise, with the reason.
    /// </summary>
    /// <param name="block">A source block.</param>
    /// <param name="machine">The machine with its G-code system and its cycle catalog.</param>
    /// <param name="problem">Why a G70 to G76 block names no contour the reader reads; null for any other
    /// block.</param>
    public static SourceContour? ContourOf(SourceBlock block, MachineConfig machine, out string? problem)
    {
        problem = null;
        SourceWord? code = null;
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "G" && NativeCode.Of(word) is string native && s_repetitive.Contains(native))
            {
                code = word;
                break;
            }
        }

        if (code is null || machine.Machine.GcodeSystem is not (GcodeSystem.A or GcodeSystem.B))
        {
            return null;
        }

        string cycle = NativeCode.Of(code)!;
        if (ContourEntry(machine.CycleCatalog, cycle) is not CycleEntry entry)
        {
            problem = $"{cycle} has no cycle with a contour in the catalog of the machine (machine-config 6)";
            return null;
        }

        SourceWord? first = block.Find(entry.Contour[0]);
        SourceWord? last = block.Find(entry.Contour[1]);
        if (first is null || last is null || FanucMacro.WholeNumber(first) is not long from
            || FanucMacro.WholeNumber(last) is not long to)
        {
            problem = $"{cycle} names its contour with {entry.Contour[0]} and {entry.Contour[1]}, the labels of its "
                + "first and its last block";
            return null;
        }

        // TODO(question): the other words of G70 to G76, depth of cut, allowances, retract and thread data, have no
        // NCX word (wave-1 question #18); a block with a word its catalog entry does not map stays RAW with its
        // contour.
        foreach (SourceWord word in block.Words)
        {
            bool mapped = entry.Params.Values.Contains(word.Address) && word.Number is not null
                && word.Expression is null;
            bool own = ReferenceEquals(word, code) || ReferenceEquals(word, first) || ReferenceEquals(word, last);
            if (word.Address != "N" && !own && !mapped)
            {
                problem = $"{word.Address}{word.Text} of {cycle} has no NCX word in the catalog entry {entry.Name}";
                return null;
            }
        }

        return new SourceContour(from.ToString(CultureInfo.InvariantCulture),
            to.ToString(CultureInfo.InvariantCulture));
    }

    // G70 to G76 of a lathe are one-shot blocks whose P and Q name the contour that follows them (controllers fanuc.md
    // 6; language 4.7.1): the structure pass turns the contour into a SUB section of the file, and the cycle block is
    // the catalog cycle with CONTOUR=name (language 4.7, CONTOUR; machine-config 6, modal and contour; D65). A cycle
    // whose contour the structure pass made no section of stays RAW with the blocks of its contour, as the control
    // reads them (FanucReader.MarkContour; D5).
    // TODO(question): the catalog gives G75 (GROOVE) and G76 (COMPOUND_THREAD) a contour as the documents say, which
    // wave-1 questions #19 and #29 doubt; the reader follows the entry of the catalog.
    private static bool ReadRepetitiveCycle(FanucBlock block)
    {
        if (block.System is not (GcodeSystem.A or GcodeSystem.B))
        {
            return false;
        }

        foreach (string code in s_repetitive)
        {
            if (block.FindCode(code) is not SourceWord word)
            {
                continue;
            }

            block.MarkRead(word);
            if (block.Fanuc.Contours.TryGetValue(block.Line, out string? contour)
                && ContourEntry(block.Machine.CycleCatalog, code) is CycleEntry entry)
            {
                ReadContourCycle(block, entry, contour);
            }
            else
            {
                ContourOf(block.Source, block.Machine, out string? problem);
                block.Draft.KeepAsRaw((problem ?? $"the contour of {code} is no range of plain blocks that follows it "
                    + "(language 4.7.1)") + "; the cycle stays RAW with its contour");
            }

            return true;
        }

        return false;
    }

    // The one-shot entry of a native cycle whose contour two words name, the first and the last block of the contour
    // (machine-config 6, modal and contour).
    private static CycleEntry? ContourEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == native && !entry.Modal && entry.Contour.Count == 2)
            {
                return entry;
            }
        }

        return null;
    }

    // CYCLE=name CONTOUR=section with the words the entry maps, and a CYCLE_CALL at the current position: the one-shot
    // block runs once where it stands (machine-config 6, modal; controller-mapping 5, CYCLE_CALL, the block that calls
    // once at the current position; language 4.7). A modal cycle still active is written again before its next call,
    // since the one-shot CYCLE replaced it in NCX (language 4.7); where the tool stands afterwards the reader does not
    // follow.
    // TODO(question): a one-shot entry runs once where it stands (machine-config 6), while the virtual machine keeps a
    // CYCLE until CYCLE=OFF, the next CYCLE, TOOL (with a WARNING) or PROGRAM=END (virtual machine 2.6, 4); the
    // documents do not say whether a reader closes a one-shot cycle with CYCLE=OFF, so it writes none, as the direct
    // Siemens call of controller-mapping 5 reads, until D224 is answered.
    private static void ReadContourCycle(FanucBlock block, CycleEntry entry, string contour)
    {
        block.Take(entry.Contour[0]);
        block.Take(entry.Contour[1]);
        var definition = new DraftBlock()
            .Add("CYCLE", new IdentValue(entry.Name))
            .Add("CONTOUR", new IdentValue(contour));
        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (block.Take(parameter.Value) is SourceWord word && FanucMacro.ValueOf(block, word) is Value value)
            {
                definition.Add(parameter.Key, value);
            }
        }

        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.Add(new DraftBlock().WithVerb("CYCLE_CALL"));
        block.Fanuc.Record("CYCLE", entry.Name);
        if (block.Fanuc.Cycle is FanucCycle active)
        {
            active.Written = null;
        }

        block.Fanuc.ForgetPositions();
    }

    // G98 returns to the initial level and G99 to the R level (mills, systems B and C; controllers fanuc.md 3, group
    // 10; controller-mapping 5, CYCLE_RETRACT); in system A the two codes are the feed mode.
    private static bool ReadReturnLevel(FanucBlock block)
    {
        if (block.System == GcodeSystem.A)
        {
            return false;
        }

        bool initial = block.TakeCode("G98");
        bool clearance = block.TakeCode("G99");
        return initial || clearance;
    }

    // The code of a cycle in the block, of the cycles of the machine's G-code system.
    private static SourceWord? CycleCode(FanucBlock block)
    {
        string[] codes = CodesOf(block);
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "G" && NativeCode.Of(word) is string code && codes.Contains(code))
            {
                return word;
            }
        }

        return null;
    }

    private static string[] CodesOf(FanucBlock block)
    {
        return CodesOf(block.System);
    }

    private static string[] CodesOf(GcodeSystem? system)
    {
        return system switch
        {
            null => s_millCycles,
            GcodeSystem.A => [.. s_latheDrilling, .. s_turningOfSystemA],
            _ => [.. s_latheDrilling, .. s_turningOfSystemsBAndC],
        };
    }

    /// <summary>
    /// Tells whether a code starts a cycle in the G-code system: a drilling or turning cycle of the system, and on a
    /// lathe of systems A and B G70 to G76 (controllers fanuc.md 3, 6).
    /// </summary>
    /// <param name="code">The code without leading zeros, "G81".</param>
    /// <param name="system">The G-code system of the machine; null for a mill.</param>
    public static bool StartsACycle(string code, GcodeSystem? system)
    {
        return CodesOf(system).Contains(code)
            || (system is GcodeSystem.A or GcodeSystem.B && s_repetitive.Contains(code));
    }

    // A code that ends any cycle: on a mill G0 to G3 (Ends), on a lathe, whose cycles belong to group 01, every other
    // code of that group (controllers fanuc.md 3, 6).
    private static bool EndsEveryCycle(FanucBlock block)
    {
        if (!block.IsLathe)
        {
            foreach (string motion in s_motion)
            {
                if (block.HasCode(motion))
                {
                    return true;
                }
            }

            return false;
        }

        IReadOnlyDictionary<string, int> groups = FanucModalGroups.For(block.System);
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "G" && NativeCode.Of(word) is string code
                && groups.TryGetValue(code, out int group) && group == FanucModalGroups.Motion)
            {
                return true;
            }
        }

        return false;
    }

    // A cycle ends with G80; on a lathe, where the cycles belong to group 01, with the next code of that group
    // (controllers fanuc.md 3, 6).
    // TODO(question): fanuc 6 says G80 cancels, "or a G0/G1 on some controls"; the reader ends the cycle of a mill at a
    // G0 to G3 as well, so that such a block moves and does not drill, until D247 is answered.
    private static bool Ends(FanucBlock block, SourceWord? code)
    {
        if (block.Fanuc.Cycle is null || code is not null)
        {
            return false;
        }

        if (!block.IsLathe)
        {
            foreach (string motion in s_motion)
            {
                if (block.HasCode(motion))
                {
                    return true;
                }
            }

            return false;
        }

        string? active = block.State.ActiveCode(FanucModalGroups.Motion);
        return active is null || !CodesOf(block).Contains(active);
    }

    // CYCLE=OFF cancels (controller-mapping 5), written where an NCX cycle is active.
    private static void End(FanucBlock block)
    {
        block.Fanuc.Cycle = null;
        if (block.Fanuc.TakeChange("CYCLE", "OFF"))
        {
            block.Draft.AddState("CYCLE", null, new IdentValue("OFF"));
        }
    }

    // A cycle code starts the cycle, or replaces the code of the active one; the control keeps the depth, R, Q, P and F
    // of the active cycle and the initial level, where the drilling axis stood when the cycle mode began (controllers
    // fanuc.md 6).
    private static void Start(FanucBlock block, SourceWord word)
    {
        block.MarkRead(word);
        string code = NativeCode.Of(word)!;
        FanucCycle? previous = block.Fanuc.Cycle;
        CycleCatalog catalog = block.Machine.CycleCatalog;
        if (s_turningOfSystemA.Contains(code) || s_turningOfSystemsBAndC.Contains(code))
        {
            // TODO(question): cycles/fanuc.toml writes the turning cycles of system A only, since the documents
            // disagree on the codes of system C (wave-1 question #16); G77 to G79 stay RAW.
            block.Fanuc.Cycle = new FanucCycle
            {
                Code = code,
                Entry = block.System == GcodeSystem.A ? ModalEntry(catalog, code) : null,
                IsTurning = true,
                Feed = block.Fanuc.ControlFeed,
            };
            return;
        }

        // A new drilling cycle takes the return level of G98 or G99 and, on a mill, its drilling axis from the plane;
        // where that is the unknown state of a caller (FanucCallerState), the cycle is not known and the block stays
        // RAW.
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        if (unknown.Groups.Contains(FanucModalGroups.ReturnLevel)
            || (!block.IsLathe && unknown.Groups.Contains(FanucModalGroups.Plane)))
        {
            block.Fanuc.Cycle = null;
            unknown.Cycle = true;
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                "G98 or G99, or the plane of G17 to G19, which a new cycle takes"));
            return;
        }

        string axis = block.IsLathe ? (code is "G87" or "G88" or "G89" ? "X" : "Z") : ToolAxis(block);
        string native = block.IsLathe ? LatheNative(block, code, previous) : code;
        decimal? initial = previous?.InitialLevel;
        if (initial is null && block.State.Positions.TryGetValue(axis, out decimal level))
        {
            initial = level;
        }

        // The cycle feeds with the modal F of the control, which a new CYCLE of NCX does not inherit and the reader
        // writes as CYCLE_F (language 2 rule 4, 4.7; D29).
        var cycle = new FanucCycle
        {
            Code = code,
            Entry = ModalEntry(catalog, native),
            Axis = axis,
            WritesAxis = block.IsLathe,
            InitialLevel = initial,
            ReturnsToInitialLevel = block.State.ActiveCode(FanucModalGroups.ReturnLevel) == "G98",
            Feed = block.Fanuc.ControlFeed,
        };
        if (previous is not null && !previous.IsTurning && previous.Axis == axis)
        {
            cycle.Depth = previous.Depth;
            cycle.Clearance = previous.Clearance;
            cycle.Peck = previous.Peck;
            cycle.Dwell = previous.Dwell;
        }

        block.Fanuc.Cycle = cycle;
    }

    // The entry of a native cycle that stays active after its block, as the drilling cycles and the simple turning
    // cycles do (machine-config 6, modal); a one-shot entry of the same code, the lathe's G76 on a mill, is none of
    // them.
    private static CycleEntry? ModalEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == native && entry.Modal)
            {
                return entry;
            }
        }

        return null;
    }

    // On a mill the drilling axis is the tool axis of G17 to G19 (controller-mapping 5, AXIS).
    private static string ToolAxis(FanucBlock block)
    {
        return block.State.ActiveCode(FanucModalGroups.Plane) switch
        {
            "G18" => "Y",
            "G19" => "X",
            _ => "Z",
        };
    }

    // TODO(question): the catalog holds no drilling entry of a lathe, and the documents do not say which NCX cycle a
    // lathe G85 and G89 is (wave-1 question #15); G83 and G87 read as PECK with a peck depth Q and as DRILL without
    // one, G84 and G88 as TAP, G85 and G89 as the entry of G85, along Z and along X (D59).
    private static string LatheNative(FanucBlock block, string code, FanucCycle? previous)
    {
        string drilling = code switch
        {
            "G87" => "G83",
            "G88" => "G84",
            "G89" => "G85",
            _ => code,
        };
        return drilling == "G83" && block.Find("Q") is null && previous?.Peck is null ? "G81" : drilling;
    }

    // The axis words that position the cycle: every axis but the drilling axis, every axis of a turning cycle.
    private static List<FanucAxisWord> Positions(FanucBlock block, FanucCycle cycle)
    {
        var positions = new List<FanucAxisWord>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            if (cycle.IsTurning || axis.Axis != cycle.Axis)
            {
                positions.Add(axis);
            }
        }

        return positions;
    }

    // R before the depth: under G91 R is the distance from the initial level and Z the distance from the R level; Q is
    // the peck, P the dwell in milliseconds, F the feed of the cycle and also the modal F of the control, K the number
    // of repeats (controllers fanuc.md 6; controller-mapping 5; D29).
    // TODO(question): on a lathe the documents do not say whether R of a drilling cycle is a level or the distance from
    // the initial level; system A has no G91, and R is read as the level, as on a mill under G90, until D248 is
    // answered.
    private static void ReadDrilling(FanucBlock block, FanucCycle cycle, bool defines)
    {
        // Under G90 or G91 of a caller the reader does not know (FanucCallerState), R, the depth and the positions of
        // absolute addresses are neither levels nor points it knows.
        if (block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance)
            && (block.Find("R") is not null || FanucAxes.Unread(block).Exists(axis => !axis.Incremental)))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
            return;
        }

        SourceWord? clearance = block.Take("R");
        if (clearance is not null)
        {
            cycle.Clearance = Level(block, clearance, cycle.InitialLevel, block.Incremental);
        }

        FanucAxisWord? depth = FanucAxes.Find(block, cycle.Axis);
        if (depth is not null)
        {
            block.MarkRead(depth.Word);
            cycle.Depth = Level(block, depth.Word, FanucNumbers.NumberOf(cycle.Clearance), depth.Incremental);
        }

        SourceWord? peck = block.Take("Q");
        if (peck is not null)
        {
            cycle.Peck = FanucMacro.ValueOf(block, peck);
        }

        SourceWord? dwell = block.Take("P");
        if (dwell?.Number is decimal milliseconds && dwell.Expression is null)
        {
            cycle.Dwell = FanucNumbers.Of(milliseconds / 1000m);
        }
        else if (dwell is not null)
        {
            block.Draft.KeepAsRaw("the dwell P of the cycle is no number");
        }

        SourceWord? feed = block.Take("F");
        if (feed is not null && FanucMacro.ValueOf(block, feed) is Value value)
        {
            cycle.Feed = value;
            block.Fanuc.ControlFeed = value;
        }

        SourceWord? repeats = block.Take("K");
        List<FanucAxisWord> positions = Positions(block, cycle);
        if (block.Draft.IsRaw || (!defines && positions.Count == 0))
        {
            return;
        }

        int count = 1;
        if (repeats is not null)
        {
            if (repeats.Number is not decimal times || repeats.Expression is not null || times < 0
                || times > MaxRepeats || times != decimal.Truncate(times))
            {
                block.Draft.KeepAsRaw("K of the cycle is no number of repeats");
                return;
            }

            count = decimal.ToInt32(times);
        }

        foreach (FanucAxisWord position in positions)
        {
            block.MarkRead(position.Word);
        }

        // Zero repeats drill no hole.
        if (count > 0 && WriteDefinition(block, cycle))
        {
            Call(block, cycle, positions, count);
        }
    }

    // An absolute level as written, or under G91 the distance from a known level (controllers fanuc.md 6).
    private static Value? Level(FanucBlock block, SourceWord word, decimal? from, bool incremental)
    {
        if (!incremental)
        {
            return FanucMacro.ValueOf(block, word);
        }

        if (from is decimal origin && word.Number is decimal distance && word.Expression is null)
        {
            return FanucNumbers.Of(origin + distance);
        }

        block.Draft.KeepAsRaw(
            $"{word.Address}{word.Text} under G91 needs the level it starts from, which the reader does not know");
        return null;
    }

    // The CYCLE block: the name of the catalog entry, AXIS on a lathe, SURFACE, the absolute CLEARANCE and DEPTH,
    // CYCLE_RETRACT with SAFE under G98, PECK, CYCLE_F, CYCLE_DWELL and the PITCH of a tapping cycle
    // (controller-mapping 5; language 4.7), written before a call whenever it differs from the one written last, since
    // a new CYCLE replaces all parameters (language 4.7).
    private static bool WriteDefinition(FanucBlock block, FanucCycle cycle)
    {
        CycleEntry entry = cycle.Entry!;
        if (cycle.Depth is null || cycle.Clearance is null)
        {
            block.Draft.KeepAsRaw(
                $"{cycle.Code} needs its depth and its R level, DEPTH and CLEARANCE (virtual machine 3.3)");
            return false;
        }

        var definition = new DraftBlock().Add("CYCLE", new IdentValue(entry.Name));
        if (cycle.WritesAxis)
        {
            definition.Add("AXIS", new IdentValue(cycle.Axis));
        }

        // TODO(question): SURFACE is R minus the clearance of the machine file or 0 (controller-mapping 5), and no key
        // of the machine file carries that clearance (wave-1 question #14); the reader writes SURFACE=0, as language 6
        // writes the drilling of G81, and none on a cycle along X, whose 0 is the axis of the part.
        if (cycle.Axis != "X")
        {
            definition.Add("SURFACE", s_zero);
        }

        definition.Add("CLEARANCE", cycle.Clearance).Add("DEPTH", cycle.Depth);
        bool safeUnknown = false;
        if (cycle.ReturnsToInitialLevel)
        {
            definition.Add("CYCLE_RETRACT", new IdentValue("SAFE"));
            if (cycle.InitialLevel is decimal initial)
            {
                definition.Add("SAFE", FanucNumbers.Of(initial));
            }
            else
            {
                safeUnknown = true;
            }
        }
        else
        {
            definition.Add("CYCLE_RETRACT", new IdentValue("CLEARANCE"));
        }

        AddParameters(block, cycle, entry, definition);
        if (Write(block, cycle, definition) && safeUnknown)
        {
            block.Draft.Warnings.Add((DiagnosticCodes.FanucInitialLevelUnknown,
                $"{cycle.Code} under G98 returns to the initial level, and where the drilling axis stood is not known; "
                + "CYCLE_RETRACT=SAFE is written without SAFE (controller-mapping 5)."));
        }

        return true;
    }

    // PECK from Q, CYCLE_F from F, CYCLE_DWELL from P where the entry maps them, and the pitch of a tapping cycle, F /
    // S in the per-minute mode and F in the per-revolution mode (controller-mapping 5, TAP; language 4.7, PITCH).
    private static void AddParameters(FanucBlock block, FanucCycle cycle, CycleEntry entry, DraftBlock definition)
    {
        if (entry.NativeOf("PECK") is not null && cycle.Peck is Value peck)
        {
            definition.Add("PECK", peck);
        }

        if (entry.CycleF is not null && cycle.Feed is Value feed)
        {
            definition.Add("CYCLE_F", feed);
        }

        if (entry.CycleDwell is not null && cycle.Dwell is Value dwell)
        {
            definition.Add("CYCLE_DWELL", dwell);
        }

        if (!entry.RuleWords.Contains("PITCH") || FanucNumbers.NumberOf(cycle.Feed) is not decimal perMinute)
        {
            return;
        }

        // The feed mode and the speed are the caller's in a subprogram and those its subprogram left in a caller
        // (virtual machine 3.9); where the reader does not know them, the pitch is not known (FanucCallerState).
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        string? mode = block.State.ActiveCode(FanucModalGroups.FeedMode);
        if (unknown.Groups.Contains(FanucModalGroups.FeedMode))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                "the feed mode, which makes F the pitch or the feed per minute"));
        }
        else if (mode == (block.System == GcodeSystem.A ? "G99" : "G95"))
        {
            definition.Add("PITCH", cycle.Feed!);
        }
        else if (unknown.Spindle || (unknown.Speeds && !block.Fanuc.Rpm.ContainsKey(block.State.LastSpindle ?? "")))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("the speed of the spindle, which gives the pitch F / S"));
        }
        else if (block.Fanuc.Rpm.TryGetValue(block.State.LastSpindle ?? "", out decimal speed) && speed != 0)
        {
            // TODO(question): no document says how many decimals a quotient the reader computes keeps (wave-1
            // question #46); the pitch keeps six.
            definition.Add("PITCH", FanucNumbers.Of(Math.Round(perMinute / speed, 6, MidpointRounding.AwayFromZero)));
        }
    }

    // The definition goes into the main block when it differs from the one written last; true when it was written.
    private static bool Write(FanucBlock block, FanucCycle cycle, DraftBlock definition)
    {
        var words = new List<string>();
        foreach (Word word in definition.Words)
        {
            words.Add(word.ToCanonical());
        }

        string written = string.Join(" ", words);
        if (written == cycle.Written)
        {
            return false;
        }

        cycle.Written = written;
        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Fanuc.Record("CYCLE", definition.Words[0].Value.ToCanonical());
        return true;
    }

    // TODO(question): language 6 writes the call of a defining block without a position, G81 G99 Z-21.732 R5. F565, as
    // CYCLE_CALL X=10 Y=10, the point the tool stands at, while language 4.7 calls a CYCLE_CALL without axis words at
    // the current position; the reader writes the axis words the block has, none there, as the CYCL CALL of Klartext
    // reads, until D220 is answered. A CYCLE_CALL at the position of the block, K times; under G91 each repeat moves by
    // the incremental words (controllers fanuc.md 6; controller-mapping 5, CYCLE_CALL and repeats; language 4.7). The
    // plane axes stand at the call point afterwards and the drilling axis at the level the cycle returns to (virtual
    // machine 3.3).
    private static void Call(FanucBlock block, FanucCycle cycle, List<FanucAxisWord> positions, int count)
    {
        var values = new List<Value>();
        foreach (FanucAxisWord position in positions)
        {
            if (FanucMacro.ValueOf(block, position.Word) is not Value value)
            {
                return;
            }

            values.Add(value);
        }

        bool mainIsFree = block.Draft.Main.Verb is null && !block.Draft.Main.Has("CYCLE", null);
        for (int repeat = 0; repeat < count; repeat++)
        {
            DraftBlock call = repeat == 0 && mainIsFree ? block.Draft.Main : new DraftBlock();
            if (!ReferenceEquals(call, block.Draft.Main))
            {
                block.Draft.After.Add(call);
            }

            call.WithVerb("CYCLE_CALL");
            for (int index = 0; index < positions.Count; index++)
            {
                call.Add(positions[index].Key, values[index]);
                FanucMotion.Move(block, positions[index], values[index]);
            }

            if (!cycle.IsTurning)
            {
                decimal? level = cycle.ReturnsToInitialLevel
                    ? cycle.InitialLevel
                    : FanucNumbers.NumberOf(cycle.Clearance);
                if (level is decimal known)
                {
                    block.State.SetPosition(cycle.Axis, known);
                }
                else
                {
                    block.State.ForgetPosition(cycle.Axis);
                }
            }
        }
    }

    // The simple turning cycles of system A, G90 (OD), G92 (thread) and G94 (face), are modal like the drilling cycles:
    // CYCLE= and one CYCLE_CALL per block with X and Z or U and W (language 4.7.1; controller-mapping 5, turning
    // cycles; cycles/fanuc.toml).
    // TODO(question): the taper R of G90 and G94 and the F of the thread G92 have no NCX word (wave-1 question #18); a
    // block with R, and a G92 with F, stay RAW.
    private static void ReadTurning(FanucBlock block, FanucCycle cycle, bool defines)
    {
        CycleEntry entry = cycle.Entry!;
        if (block.Find("R") is not null)
        {
            block.Draft.KeepAsRaw($"R, the taper of {cycle.Code}, has no NCX word");
            return;
        }

        SourceWord? feed = block.Take("F");
        if (feed is not null)
        {
            if (entry.CycleF is null)
            {
                block.Draft.KeepAsRaw($"F of {cycle.Code} has no NCX word");
                return;
            }

            if (FanucMacro.ValueOf(block, feed) is Value value)
            {
                cycle.Feed = value;
                block.Fanuc.ControlFeed = value;
            }
        }

        List<FanucAxisWord> positions = Positions(block, cycle);
        if (block.Draft.IsRaw || (!defines && positions.Count == 0))
        {
            return;
        }

        foreach (FanucAxisWord position in positions)
        {
            block.MarkRead(position.Word);
        }

        var definition = new DraftBlock().Add("CYCLE", new IdentValue(entry.Name));
        if (entry.CycleF is not null && cycle.Feed is Value cycleFeed)
        {
            definition.Add("CYCLE_F", cycleFeed);
        }

        Write(block, cycle, definition);
        Call(block, cycle, positions, 1);
    }
}
