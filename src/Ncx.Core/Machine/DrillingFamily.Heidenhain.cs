namespace Ncx.Core.Machine;

// The built-in drilling family on Heidenhain (heidenhain 5, controller-mapping 5); cycles/heidenhain.toml writes the
// same entries. A definition is called by CYCL CALL, M99 or CYCL CALL PAT and never calls itself: no entry is modal.
public static partial class DrillingFamily
{
    // The reader converts to absolute coordinates along the tool axis: CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201,
    // SAFE = Q203 + Q204 (heidenhain 5, controller-mapping 5).
    private static readonly string[] s_heidenhainAbsolute = ["CLEARANCE", "DEPTH", "SAFE"];

    // The words that rules carry, not a Q parameter (controller-mapping 5): CYCLE_RETRACT is the cycle with or without
    // Q204, AXIS the tool axis of the TOOL CALL.
    private static readonly string[] s_heidenhainRuleWords = ["CYCLE_RETRACT", "AXIS"];

    // The Q parameters in the control's order, as examples/sources/BOHREN.h writes the cycles 200, 203, 207 and 201
    // (heidenhain 8.7).
    private static readonly string[] s_cycle200 = ["Q200", "Q201", "Q206", "Q202", "Q210", "Q203", "Q204", "Q211"];

    private static readonly string[] s_cycle203 =
        ["Q200", "Q201", "Q206", "Q202", "Q210", "Q203", "Q204", "Q212", "Q213", "Q205", "Q211", "Q208", "Q256"];

    private static readonly string[] s_cycle207 = ["Q200", "Q201", "Q239", "Q203", "Q204"];
    private static readonly string[] s_cycle201 = ["Q200", "Q201", "Q206", "Q211", "Q208", "Q203", "Q204"];

    // DRILL and DRILL_DWELL are cycle 200, PECK and CHIP_BREAK cycle 203, TAP 207, REAM 201, BORE 202
    // (controller-mapping 5). The signature of cycle 202 is not in the documents.
    // TODO(question): which Q211 makes a cycle 200 a DRILL and which Q256 or Q213 makes a cycle 203 a CHIP_BREAK is not
    // in the documents (cycles/heidenhain.toml); FindNative takes the first entry (D163).
    private static CycleCatalog HeidenhainFamily()
    {
        return new CycleCatalog
        {
            Controller = Controller.Heidenhain,
            Entries =
            [
                HeidenhainEntry("DRILL", "200", s_cycle200, HeidenhainDrillParams()),
                HeidenhainEntry("DRILL_DWELL", "200", s_cycle200, HeidenhainDrillParams()),
                HeidenhainEntry("PECK", "203", s_cycle203, HeidenhainPeckParams()),
                HeidenhainEntry("CHIP_BREAK", "203", s_cycle203, HeidenhainPeckParams()),
                HeidenhainEntry("TAP", "207", s_cycle207, HeidenhainTapParams()),
                HeidenhainEntry("REAM", "201", s_cycle201, HeidenhainDrillParams()),
                HeidenhainEntry("BORE", "202", [], HeidenhainDrillParams()),
            ],
        };
    }

    // One Heidenhain drilling entry: its signature, its Q parameters, the planes relative to Q203.
    private static CycleEntry HeidenhainEntry(
        string name, string native, string[] signature, Dictionary<string, string> parameters)
    {
        return new CycleEntry
        {
            Name = name,
            Native = native,
            Signature = signature,
            Params = parameters,
            AbsoluteFromSurface = s_heidenhainAbsolute,
            RuleWords = s_heidenhainRuleWords,
        };
    }

    // The planes of every drilling cycle: Q200 safety clearance above the surface, Q201 depth, Q203 surface
    // coordinate, Q204 second clearance (heidenhain 5).
    private static Dictionary<string, string> HeidenhainPlanes()
    {
        return new Dictionary<string, string>
        {
            ["CLEARANCE"] = "Q200",
            ["DEPTH"] = "Q201",
            ["SURFACE"] = "Q203",
            ["SAFE"] = "Q204",
        };
    }

    // Cycles 200, 201 and 202: the planes, Q206 the feed and Q211 the dwell at the bottom (controller-mapping 5).
    private static Dictionary<string, string> HeidenhainDrillParams()
    {
        Dictionary<string, string> parameters = HeidenhainPlanes();
        parameters["CYCLE_F"] = "Q206";
        parameters["CYCLE_DWELL"] = "Q211";
        return parameters;
    }

    // Cycle 203: as cycle 200, with Q202 the infeed depth as PECK (language 4.7).
    private static Dictionary<string, string> HeidenhainPeckParams()
    {
        Dictionary<string, string> parameters = HeidenhainDrillParams();
        parameters["PECK"] = "Q202";
        return parameters;
    }

    // Cycle 207: the planes and Q239 the pitch; BOHREN.h writes it with neither Q206 nor Q211.
    private static Dictionary<string, string> HeidenhainTapParams()
    {
        Dictionary<string, string> parameters = HeidenhainPlanes();
        parameters["PITCH"] = "Q239";
        return parameters;
    }
}
