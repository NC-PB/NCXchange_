using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The canned cycles of a Fanuc block (controllers fanuc.md 6; controller-mapping 5; language 4.7, 4.7.1;
/// machine-config 6): a cycle block defines and calls at its position, so the CYCLE definition of NCX is written with
/// its first CYCLE_CALL, the native code of the catalog with G98 or G99 from CYCLE_RETRACT, R from CLEARANCE, Z from
/// DEPTH, Q, P and F; every later call writes its position alone; CYCLE=OFF is G80. On a lathe G83 to G85 drill along Z
/// and G87 to G89 along X, and the simple turning cycles are modal as the drilling cycles are.
/// </summary>
internal static class FanucCycles
{
    /// <summary>
    /// The key of the definition the control has active in the target state, DefinitionOf.
    /// </summary>
    public const string Definition = "CYCLE:DEF";

    // The words of a CYCLE block (language 4.7).
    private static readonly string[] s_cycleWords =
    [
        "CYCLE", "AXIS", "SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_RETRACT", "PECK", "CYCLE_F", "CYCLE_DWELL",
        "PITCH", "CONTOUR",
    ];

    // The simple turning cycles of system A and their codes in system B (controllers fanuc.md 3, 6; D174).
    private static readonly Dictionary<string, string> s_turningOfSystemB = new(StringComparer.Ordinal)
    {
        ["G90"] = "G77",
        ["G92"] = "G78",
        ["G94"] = "G79",
    };

    /// <summary>
    /// The tool axis of a working plane, the drilling axis of a mill (controller-mapping 5, AXIS).
    /// </summary>
    public static string ToolAxisOf(Workplane plane)
    {
        return plane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
    }

    /// <summary>
    /// The active cycle of NCX as the target state keeps its definition: the name, the drilling axis and the
    /// parameters (language 4.7); null where no cycle is active.
    /// </summary>
    public static string? DefinitionOf(CycleSnapshot cycle)
    {
        return cycle.Name is string name
            ? name + " " + cycle.Axis + " " + string.Join(" ", ParameterTexts(cycle))
            : null;
    }

    /// <summary>
    /// A canned cycle the control has active, or may have active, ends with G80 before a tool change (virtual machine
    /// 4, cycle row).
    /// </summary>
    public static void CancelBeforeToolChange(FanucBlock write)
    {
        Cancel(write);
    }

    /// <summary>
    /// A canned cycle the control has active, or may have active, ends with G80 before a motion that is no call, since
    /// a position block under an active G8x drills (controllers fanuc.md 6).
    /// </summary>
    public static void CancelBeforeMotion(FanucBlock write)
    {
        // TODO(question): D247: whether G0 to G3 end a canned cycle is open; G80 stands before the motion, which ends
        // it on every control, until D247 is answered.
        Cancel(write);
    }

    /// <summary>
    /// The cycle words of the block: CYCLE=OFF as G80 where the control has a cycle active, a definition that waits for
    /// its call, and CYCLE_CALL (controllers fanuc.md 6; controller-mapping 5).
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        if (block.Find("CYCLE") is Word cycle)
        {
            // A CYCLE block defines the cycle of NCX on every path from here on (language 4.7).
            write.StatesForLater(Definition, known: true);
            string name = cycle.Value.ToCanonical();
            CheckCarried(write, name == "OFF" ? null : write.Machine.CycleCatalog.Find(name));
            if (name == "OFF" && write.Target.ActiveOf(FanucCodes.Cycle) != "G80")
            {
                write.Main.Code(FanucCodes.RankOf(FanucCodes.Cycle), "G80");
                Ended(write);
            }
        }

        if (block.Verb?.Key == "CYCLE_CALL")
        {
            WriteCall(write);
        }
    }

    // A word of a CYCLE block stands in the cycle block through the address its catalog entry maps, or through a rule
    // of the Fanuc compiler that the entry carries: SURFACE, SAFE and CYCLE_RETRACT of a drilling entry, AXIS, and the
    // PITCH of TAP, which is F (machine-config 6; controller-mapping 5; CycleEntry.Words). Any other word of the block
    // is CMP385, never dropped (language 2 rule 8): PITCH of THREAD, whose G92 has no address for it in the catalog,
    // and DEPTH and CLEARANCE of TURN_OD, whose Z and R are no planes of G90. A cycle that the catalog does not name
    // is CMP380 at its call.
    // TODO(question): D157: which words a rule carries is the recommendation of D157, which the built-in drilling
    // family follows (CycleEntry.RuleWords); a word of an entry of another name is written only through its params
    // until D157 is answered. D175: the lead of G92 and G76 and the taper R of G90, G92 and G94 have no NCX word.
    private static void CheckCarried(FanucBlock write, CycleEntry? entry)
    {
        write.Written("CYCLE");
        foreach (string key in s_cycleWords)
        {
            if (key == "CYCLE" || write.Block.Find(key) is not Word word)
            {
                continue;
            }

            write.Written(word);
            bool carried = entry is null
                || (entry.Words.Contains(key) && (key != "PITCH" || entry.CycleF is not null));
            if (!carried)
            {
                write.Error(DiagnosticCodes.FanucCycleWordNotMapped,
                    $"{word.ToCanonical()} of CYCLE={entry!.Name} maps to no address of {entry.Native} in the catalog, "
                    + "and no rule of the Fanuc compiler carries it, so the cycle cannot be written without losing it "
                    + "(machine-config 6; controller-mapping 5; language 2 rule 8).");
            }
        }
    }

    // G80 in a line of its own where the control has a canned cycle active, or where the target state does not know
    // whether it has one: in a subprogram, which is written from an unknown target state (D99), and after a label that
    // a jump reaches; a caller of the subprogram may have left a cycle active, and in NCX a RAPID, LINE, ARC or HOME
    // never calls one (language 4.7, CYCLE_CALL). G80 without an active cycle changes nothing.
    private static void Cancel(FanucBlock write)
    {
        if (write.Target.ActiveOf(FanucCodes.Cycle) != "G80")
        {
            write.Write("G80");
            Ended(write);
        }
    }

    // After G80 no cycle is active; the cycles stand in group 01 (controllers fanuc.md 3), so the next motion writes
    // its code, and so does the caller's next motion after the return of a subprogram (D99). The cycle of NCX stays
    // active through a RAPID, LINE or ARC (language 4.7), so a definition that differs by path still does
    // (FanucPaths).
    private static void Ended(FanucBlock write)
    {
        write.Target.Set(FanucCodes.Cycle, "G80");
        if (!write.DependsOnThePath(Definition))
        {
            write.Target.Forget(Definition);
        }

        write.MakeUnknown(FanucCodes.Motion);
    }

    // A call writes the definition where the control has another cycle or another definition active, or where it has no
    // position, since a block with a position calls the active cycle and a block without one does not (controllers
    // fanuc.md 6); otherwise the position alone.
    private static void WriteCall(FanucBlock write)
    {
        write.Written("CYCLE_CALL");
        CycleSnapshot cycle = write.After.Cycle;
        List<FanucAxisWord> positions = FanucAxes.Of(write.Block);
        foreach (FanucAxisWord position in positions)
        {
            write.Written(position.Word);
        }

        if (cycle.Name is not string name)
        {
            return;
        }

        if (positions.Exists(position => position.Axis == cycle.Axis) && cycle.Controller is null)
        {
            // TODO(question): D189: a word on the drilling axis in a CYCLE_CALL block has two meanings, the depth on
            // Fanuc and a pre-positioning after Heidenhain M99; the block is an ERROR until D189 is answered.
            write.Error(DiagnosticCodes.FanucCycleWordNotMapped,
                $"CYCLE_CALL names the drilling axis {cycle.Axis}, which a Fanuc cycle block takes as its depth "
                + "(language 4.7, CYCLE_CALL; D189).");
            return;
        }

        // The call executes the cycle of NCX of the path the control came along (language 4.7: CYCLE is modal), and
        // after a label that a jump reaches or a skipped block that cycle may differ by path, while the control holds
        // none of them, since Fanuc defines a cycle with its first call: no definition of the STATIC walk is right on
        // every path (virtual machine 1; D53).
        if (write.DependsOnThePath(Definition))
        {
            write.ReportNotHeld("CYCLE");
            return;
        }

        CycleEntry? entry = cycle.Controller is null ? write.Machine.CycleCatalog.Find(name) : null;
        string? code = cycle.Controller is null ? CodeOf(write, entry, cycle) : NativeCode(name);
        if (code is null)
        {
            return;
        }

        string definition = DefinitionOf(cycle) ?? name;
        bool defines = write.Target.ActiveOf(FanucCodes.Cycle) != code
            || write.Target.ActiveOf(Definition) != definition || positions.Count == 0;
        // A call drills along the tool axis, so a length offset that waits for it stands in the call's block (language
        // 4.4; controllers fanuc.md 6).
        if (!defines)
        {
            if (AddPositions(write, positions))
            {
                FanucToolWords.AddOffsets(write, movesToolAxis: true);
            }

            return;
        }

        FanucMotion.WritePlane(write);
        if (write.System != GcodeSystem.A)
        {
            FanucMotion.Change(write, FanucCodes.Distance, "G90");
        }

        write.Main.Code(FanucCodes.RankOf(FanucCodes.Cycle), code);
        if (entry is null)
        {
            AddPositions(write, positions);
            AddNativeParameters(write, cycle);
            FanucToolWords.AddOffsets(write, movesToolAxis: true);
        }
        else if (!AddDefinition(write, entry, cycle, positions))
        {
            return;
        }

        write.Target.Set(FanucCodes.Cycle, code);
        write.Target.Set(Definition, definition);
        write.MakeUnknown(FanucCodes.Motion);
    }

    // The native code of a catalog entry for the machine (machine-config 6): on a mill the drilling entries along the
    // tool axis; on a lathe the drilling codes by AXIS and the simple turning cycles in the numbers of the G-code
    // system.
    private static string? CodeOf(FanucBlock write, CycleEntry? entry, CycleSnapshot cycle)
    {
        if (entry is null)
        {
            write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
                $"CYCLE={cycle.Name} is no cycle of the catalog of the machine (machine-config 6, language 4.7.1).");
            return null;
        }

        // TODO(question): G70 to G76 name their contour by the block numbers P and Q of its first and its last block,
        // and machine-config 6 has the compiler write "the block numbers of that section"; the reader writes the
        // section without labels, and no document gives the numbers, nor where a contour that G71 and G70 share
        // stands in the program; the cycle is CMP384, until that is answered (controllers fanuc.md 6; D65).
        if (!entry.Modal || entry.Contour.Count > 0)
        {
            write.Error(DiagnosticCodes.FanucContourCycleNotWritten,
                $"CYCLE={entry.Name} runs once with the contour P to Q in the program, which the Fanuc compiler does "
                + "not write yet (controllers fanuc.md 6; language 4.7.1; D65).");
            return null;
        }

        bool turning = s_turningOfSystemB.ContainsKey(entry.Native);
        if (turning)
        {
            return TurningCode(write, entry);
        }

        if (write.IsLathe)
        {
            return LatheDrillingCode(write, entry, cycle);
        }

        if (cycle.Axis != ToolAxisOf(write.After.Frame.Workplane))
        {
            write.Error(DiagnosticCodes.FanucCycleAxisNotTheToolAxis,
                $"CYCLE={entry.Name} drills along {cycle.Axis}, and a mill drills along the tool axis of its plane "
                + "(controller-mapping 5, AXIS).");
            return null;
        }

        return entry.Native;
    }

    // G90, G92, G94 of system A are G77, G78, G79 in system B (controllers fanuc.md 3; language 4.7.1).
    // TODO(question): D174: the codes of system C are G20/G21/G24 in fanuc 3 and G77/G78/G79 in controller-mapping 5
    // and language 4.7.1; a turning cycle of system C is an ERROR until D174 is answered.
    private static string? TurningCode(FanucBlock write, CycleEntry entry)
    {
        string? code = write.System switch
        {
            GcodeSystem.A => entry.Native,
            GcodeSystem.B => s_turningOfSystemB[entry.Native],
            _ => null,
        };
        if (code is null)
        {
            write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
                $"CYCLE={entry.Name} is a turning cycle of a Fanuc lathe, and the machine is a mill or a lathe of "
                + "G-code system C, whose codes D174 asks for (controllers fanuc.md 3, 6).");
        }

        return code;
    }

    // Lathes drill along Z with G83 to G85 and along X with G87 to G89 (controllers fanuc.md 6; controller-mapping 5,
    // AXIS; D59).
    // TODO(question): D160: the catalog holds no lathe drilling entry; DRILL and PECK are G83 or G87, without and with
    // Q, TAP G84 or G88 and REAM G85 or G89, as D160 recommends, and the other cycles have no lathe code, until D160 is
    // answered.
    private static string? LatheDrillingCode(FanucBlock write, CycleEntry entry, CycleSnapshot cycle)
    {
        bool alongX = cycle.Axis == "X";
        string? code = entry.Name switch
        {
            "DRILL" or "PECK" => alongX ? "G87" : "G83",
            "TAP" => alongX ? "G88" : "G84",
            "REAM" => alongX ? "G89" : "G85",
            _ => null,
        };
        if (code is null)
        {
            write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
                $"CYCLE={entry.Name} has no drilling code of a Fanuc lathe (controllers fanuc.md 6; D160).");
        }

        return code;
    }

    // The definition after the code (controllers fanuc.md 6; controller-mapping 5): G98 or G99 from CYCLE_RETRACT, the
    // position, Z from DEPTH, R from CLEARANCE, Q from PECK, P from CYCLE_DWELL in milliseconds, F from CYCLE_F or the
    // pitch of TAP where the control's feed differs (D218), each where the entry maps it; a word it does not map is
    // CMP385 at the CYCLE block (CheckCarried). SURFACE has no Fanuc address (D158).
    private static bool AddDefinition(FanucBlock write, CycleEntry entry, CycleSnapshot cycle,
        List<FanucAxisWord> positions)
    {
        Value? depth = entry.NativeOf("DEPTH") is null ? null : Parameter(cycle, "DEPTH");
        Value? clearance = entry.NativeOf("CLEARANCE") is null ? null : Parameter(cycle, "CLEARANCE");
        if (entry.NativeOf("CLEARANCE") is not null && !AddReturnLevel(write, cycle))
        {
            return false;
        }

        if (!AddPositions(write, positions))
        {
            return false;
        }

        decimal factor = FanucAxes.WordFactor(write, cycle.Axis);
        string address = FanucMotion.DecimalsAddress(write, cycle.Axis);
        if (depth is not null && FanucExpressions.WordValue(write, address, depth, factor) is string z)
        {
            write.Main.Word((FanucAxes.LetterOf(write, cycle.Axis, incremental: false) ?? cycle.Axis) + z);
        }

        // TODO(question): D248: on a lathe the documents do not say whether R is a level or the distance from the
        // initial level; R is written as the level CLEARANCE, as the reader reads it, until D248 is answered.
        if (clearance is not null && FanucExpressions.WordValue(write, address, clearance, factor) is string r)
        {
            write.Main.Word("R" + r);
        }

        AddMapped(write, entry, cycle, "PECK", "Q", address, 1m);

        // TODO(question): D177: whether the control dwells with the P of G81, G83, G73, G85 and G86 is open; P stands
        // where the entry maps CYCLE_DWELL, as the catalog follows controller-mapping 5, until D177 is answered.
        AddMapped(write, entry, cycle, "CYCLE_DWELL", "P", "P", 1000m);

        // The definition calls at its position and drills along the tool axis: H stands before F, as the sources
        // write G43 Z2. H1 (language 4.4; the TODO(question) of FanucLine).
        FanucToolWords.AddOffsets(write, movesToolAxis: true);
        AddFeed(write, entry, cycle);
        return true;
    }

    // G98 returns to the initial level, the drilling-axis position before the cycle, G99 to R (controllers fanuc.md 3,
    // group 10; controller-mapping 5, CYCLE_RETRACT); in system A the two codes are the feed mode. The word of every
    // CYCLE block is written with the definition (language 4.7: a new CYCLE replaces all parameters).
    private static bool AddReturnLevel(FanucBlock write, CycleSnapshot cycle)
    {
        bool safe = Parameter(cycle, "CYCLE_RETRACT")?.ToCanonical() == "SAFE";
        if (write.System == GcodeSystem.A)
        {
            if (safe)
            {
                write.Error(DiagnosticCodes.FanucSafeRetractOnSystemA,
                    "CYCLE_RETRACT=SAFE has no code on a lathe of G-code system A, whose G98 and G99 are the feed mode "
                    + "(controllers fanuc.md 3).");
                return false;
            }

            return true;
        }

        string code = safe ? "G98" : "G99";
        write.Main.Code(FanucCodes.RankOf(FanucCodes.ReturnLevel), code);
        write.Target.Set(FanucCodes.ReturnLevel, code);
        if (safe && FanucFunctions.NumberOf(Parameter(cycle, "SAFE") ?? NoValue.Instance) is decimal plane
            && write.Before.Motion.Position.TryGetValue(cycle.Axis, out AxisPosition initial) && initial.Known
            && initial.Value != plane)
        {
            write.Warning(DiagnosticCodes.FanucSafeIsNotTheInitialLevel,
                $"G98 returns to the initial level {cycle.Axis}{initial.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"where the drilling axis stands before the cycle, and SAFE is "
                + $"{plane.ToString(CultureInfo.InvariantCulture)} (controller-mapping 5).");
        }

        return true;
    }

    // The positions of a call, absolute: an incremental position is the point the virtual machine reached, since the
    // planes of the cycle are absolute (controllers fanuc.md 6); in system A U and W as written.
    private static bool AddPositions(FanucBlock write, List<FanucAxisWord> positions)
    {
        foreach (FanucAxisWord position in positions)
        {
            string? word = position.Incremental && write.System != GcodeSystem.A
                ? FanucMotion.AbsoluteWord(write, position)
                : FanucMotion.Word(write, position);
            if (word is null)
            {
                return false;
            }

            write.Main.Word(word);
        }

        return true;
    }

    // A word of the definition through the native address its entry maps (machine-config 6); a word the entry does not
    // map has no Fanuc form and is CMP385 at the CYCLE block (CheckCarried).
    private static void AddMapped(FanucBlock write, CycleEntry entry, CycleSnapshot cycle, string word, string address,
        string decimalsAddress, decimal factor)
    {
        if (Parameter(cycle, word) is not Value value || entry.NativeOf(word) is null)
        {
            return;
        }

        if (FanucExpressions.WordValue(write, decimalsAddress, value, factor) is string text)
        {
            write.Main.Word(address + text);
        }
    }

    // F of the cycle block is CYCLE_F, and it is the control's modal feed as well (D29); a tapping cycle without
    // CYCLE_F feeds with the pitch, F = PITCH x S per minute or PITCH per revolution (controller-mapping 5, TAP). It is
    // not the F of NCX, so the next LINE or ARC writes that again where it differs (FanucBlock.WroteAnotherValue); one
    // from an expression stands every time, since its value may have changed since the last one (controllers fanuc.md
    // 7).
    // TODO(question): D218: the F of the cycle block is written only where the control's feed differs, as D218
    // recommends, until D218 is answered.
    private static void AddFeed(FanucBlock write, CycleEntry entry, CycleSnapshot cycle)
    {
        if (entry.CycleF is null)
        {
            return;
        }

        string? feed = null;
        bool known = true;
        if (Parameter(cycle, "CYCLE_F") is Value cycleFeed)
        {
            feed = FanucExpressions.WordValue(write, "F", cycleFeed, 1m);
            known = cycleFeed is not ExprValue;
        }
        else if (entry.Words.Contains("PITCH") && Parameter(cycle, "PITCH") is Value pitch)
        {
            feed = PitchFeed(write, pitch);
            known = pitch is not ExprValue;
        }

        if (feed is null)
        {
            return;
        }

        if (!known || write.DependsOnThePath("F") || write.Target.ActiveOf("F") != feed)
        {
            write.Main.Word("F" + feed);
        }

        write.WroteAnotherValue("F", known ? feed : null);
    }

    // The feed of the pitch: PITCH per revolution, PITCH x S per minute (controller-mapping 5, TAP), an expression
    // with its factor as a constant (controllers fanuc.md 7). S is the speed of the spindle that turns the tap, the
    // spindle of the current tool holder, the default spindle where the holder has none (virtual machine 2.3, 5), the
    // driven tool TOOL of a turret. A spindle that is off, a positioning C axis or under constant surface speed, or
    // whose speed is not known, an RPM from an expression in STATIC mode (virtual machine 1), gives no F, and G84 would
    // tap with the feed the control has: an ERROR, never a pitch dropped silently (language 2 rule 8).
    private static string? PitchFeed(FanucBlock write, Value pitch)
    {
        decimal factor = 1m;
        if (write.After.Motion.FeedMode != FeedMode.PerRev)
        {
            string? id = TapSpindleOf(write);
            string? why = id is null ? "the machine has no spindle" : SpeedMissing(write, id);
            if (id is null || why is not null)
            {
                write.Error(DiagnosticCodes.FanucTapFeedNotKnown,
                    $"PITCH={pitch.ToCanonical()} without CYCLE_F feeds with PITCH x S per minute, S the speed of the "
                    + $"spindle that turns the tap, the spindle of the current tool holder, and {why}, so the F of the "
                    + "cycle cannot be written (controller-mapping 5, TAP; virtual machine 2.3).");
                return null;
            }

            factor = write.After.Spindles[id].Rpm;
        }

        return FanucFunctions.NumberOf(pitch) is decimal number
            ? write.FormatComputed("F", number * factor)
            : FanucExpressions.WordValue(write, "F", pitch, factor);
    }

    // The spindle of the current tool holder, the holder of the last TOOL, else the default spindle (virtual machine
    // 2.3, 5; 3.8 rule 2).
    private static string? TapSpindleOf(FanucBlock write)
    {
        string? holder = write.After.LastHolder;
        string? spindle = holder is null ? null : write.Machine.FindResource(holder)?.Spindle;
        return spindle ?? write.Machine.ResolveDefaultSpindle()?.Id;
    }

    // Why the spindle gives no speed for PITCH x S, in words of the spindle by its role (language 4.10); null where it
    // runs at a known speed.
    private static string? SpeedMissing(FanucBlock write, string id)
    {
        string name = id;
        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == id)
            {
                name = role.Key;
            }
        }

        if (!write.After.Spindles.TryGetValue(id, out SpindleSnapshot? spindle))
        {
            return $"{name} is no spindle of the machine";
        }

        if (spindle.Mode != SpindleMode.Spindle)
        {
            return $"{name} is a positioning axis (SPINDLE_MODE=AXIS)";
        }

        if (spindle.Direction == SpindleDirection.Off)
        {
            return $"{name} is off";
        }

        if (spindle.Css)
        {
            return $"{name} runs under CSS, whose S is the cutting speed";
        }

        return spindle.Rpm <= 0 || write.After.Unknown.Contains("RPM:" + id)
            ? $"the speed of {name} is not known here"
            : null;
    }

    // TODO(question): D144: the form of n in CYCLE:FANUC=n and the parameters of a native Fanuc cycle are open; n is
    // written as the G code (G71, or G81 for 81) and every native parameter as its address with its value in source
    // order, until D144 is answered.
    private static string NativeCode(string name)
    {
        return name.Length > 0 && char.IsAsciiDigit(name[0]) ? "G" + name : name;
    }

    private static void AddNativeParameters(FanucBlock write, CycleSnapshot cycle)
    {
        foreach (Word parameter in cycle.Parameters)
        {
            if (parameter.Definition is null
                && FanucExpressions.WordValue(write, parameter.Key, parameter.Value, 1m) is string value)
            {
                write.Main.Word(parameter.Key + value);
            }
        }
    }

    private static Value? Parameter(CycleSnapshot cycle, string key)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word.Value;
            }
        }

        return null;
    }

    private static List<string> ParameterTexts(CycleSnapshot cycle)
    {
        var texts = new List<string>();
        foreach (Word word in cycle.Parameters)
        {
            texts.Add(word.ToCanonical());
        }

        return texts;
    }
}
