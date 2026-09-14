namespace Ncx.Core.Machine;

// The built-in drilling family on a Fanuc machining center (fanuc 6, controller-mapping 5); cycles/fanuc.toml writes
// the same entries. A lathe drills with G83/G84/G85 on the face and G87/G88/G89 on the circumference and overrides
// these entries in its machine file (controller-mapping 5, D59).
public static partial class DrillingFamily
{
    // The words that rules of the Fanuc reader and compiler carry, not an address (controller-mapping 5): SURFACE is R
    // minus the clearance of the machine file or 0, SAFE the initial level of G98, CYCLE_RETRACT the G98 or G99 of the
    // cycle block, AXIS the tool axis of G17..G19.
    private static readonly string[] s_fanucRuleWords = ["SURFACE", "SAFE", "CYCLE_RETRACT", "AXIS"];

    // G84 has no address for the pitch either: it is F / S in the per-minute mode and F in the per-revolution mode
    // (controller-mapping 5).
    private static readonly string[] s_fanucTapRuleWords = ["SURFACE", "SAFE", "CYCLE_RETRACT", "AXIS", "PITCH"];

    // The cycle block defines and calls at its own position, every following block with a position calls again, G80
    // cancels: every entry is modal (fanuc 6, 9.5).
    private static CycleCatalog FanucFamily()
    {
        return new CycleCatalog
        {
            Controller = Controller.Fanuc,
            Entries =
            [
                FanucEntry("DRILL", "G81", peck: false, s_fanucRuleWords),        // G81 G99 Z-21.732 R5. F565
                FanucEntry("DRILL_DWELL", "G82", peck: false, s_fanucRuleWords),  // G82 adds a dwell P
                FanucEntry("PECK", "G83", peck: true, s_fanucRuleWords),          // G83 pecks with Q, full retract
                FanucEntry("CHIP_BREAK", "G73", peck: true, s_fanucRuleWords),    // G73 chip-breaks
                FanucEntry("TAP", "G84", peck: false, s_fanucTapRuleWords),       // G84 G99 Z-20. R5. P0 F750
                FanucEntry("REAM", "G85", peck: false, s_fanucRuleWords),         // G85 G99 Z-20. R5. F420
                FanucEntry("BORE", "G86", peck: false, s_fanucRuleWords),         // G86 bores
            ],
        };
    }

    // Z and R of the cycle block are the absolute DEPTH and CLEARANCE, Q the peck of G83 and G73, F and P the cycle's
    // own feed and dwell, which never touch the modal F of the program (fanuc 6, controller-mapping 5, D29).
    // TODO(question): fanuc 6 names the dwell P for G82 only; P stands on every entry as controller-mapping 5 writes
    // "F and P on the G8x block", as cycles/fanuc.toml does (D177).
    private static CycleEntry FanucEntry(string name, string native, bool peck, string[] ruleWords)
    {
        var parameters = new Dictionary<string, string>
        {
            ["DEPTH"] = "Z",
            ["CLEARANCE"] = "R",
        };
        if (peck)
        {
            parameters["PECK"] = "Q";
        }

        parameters["CYCLE_DWELL"] = "P";
        parameters["CYCLE_F"] = "F";
        return new CycleEntry
        {
            Name = name,
            Native = native,
            Modal = true,
            Params = parameters,
            RuleWords = ruleWords,
        };
    }
}
