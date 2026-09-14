namespace Ncx.Core.Machine;

// The built-in drilling family on the SINUMERIK 840D sl (siemens 7, controller-mapping 5); cycles/siemens.toml writes
// the same entries. A direct CYCLE8x(...) block calls once, MCALL makes the call modal: the call form is a word of its
// own and no entry is modal.
public static partial class DrillingFamily
{
    // SDIS is unsigned and added to the reference plane RFP: CLEARANCE = RFP + SDIS (siemens 7, controller-mapping 5).
    private static readonly string[] s_siemensAbsolute = ["CLEARANCE"];

    // The cycle always returns to RTP, so CYCLE_RETRACT is a rule (controller-mapping 5); so is AXIS, the plane of
    // G17..G19, where no _AXN carries it.
    private static readonly string[] s_siemensRuleWords = ["CYCLE_RETRACT", "AXIS"];
    private static readonly string[] s_siemensAxnRuleWords = ["CYCLE_RETRACT"];

    // The leading parameters of the 840D sl signatures as siemens 7 writes them; the tails after "..." are not in the
    // documents (cycles/siemens.toml).
    private static readonly string[] s_cycle81 =
        ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "_GMODE", "_DMODE", "_AMODE"];

    private static readonly string[] s_cycle82 =
        ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "_GMODE", "_DMODE", "_AMODE", "_VARI"];

    private static readonly string[] s_cycle83 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "FDEP", "FDPR", "_DAM", "DTB", "DTS", "FRF", "VARI", "_AXN", "_MDEP", "_VRT",
        "_DTD", "_DIS1",
    ];

    private static readonly string[] s_cycle84 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "SDAC", "MPIT", "PIT", "POSS", "SST", "SST1", "_AXN", "_PITA",
        "_TECHNO", "_VARI", "_DAM", "_VRT",
    ];

    private static readonly string[] s_cycle85 = ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "FFR", "RFF"];

    private static readonly string[] s_cycle86 =
        ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "SDIR", "RPA", "RPO", "RPAP", "POSS"];

    // DRILL is CYCLE81, DRILL_DWELL CYCLE82, PECK and CHIP_BREAK CYCLE83 with VARI 1 or 0, TAP CYCLE84, REAM CYCLE85,
    // BORE CYCLE86 (controller-mapping 5).
    private static CycleCatalog SiemensFamily()
    {
        return new CycleCatalog
        {
            Controller = Controller.Siemens,
            Entries =
            [
                SiemensEntry("DRILL", "CYCLE81", s_cycle81, SiemensModalFeedParams(), s_siemensRuleWords, null),
                SiemensEntry("DRILL_DWELL", "CYCLE82", s_cycle82, SiemensModalFeedParams(), s_siemensRuleWords, null),
                SiemensEntry("PECK", "CYCLE83", s_cycle83, SiemensPeckParams(), s_siemensAxnRuleWords, 1m),
                SiemensEntry("CHIP_BREAK", "CYCLE83", s_cycle83, SiemensPeckParams(), s_siemensAxnRuleWords, 0m),
                SiemensEntry("TAP", "CYCLE84", s_cycle84, SiemensTapParams(), s_siemensAxnRuleWords, null),
                SiemensEntry("REAM", "CYCLE85", s_cycle85, SiemensReamParams(), s_siemensRuleWords, null),
                SiemensEntry("BORE", "CYCLE86", s_cycle86, SiemensModalFeedParams(), s_siemensRuleWords, null),
            ],
        };
    }

    // One Siemens drilling entry; VARI tells PECK from CHIP_BREAK on CYCLE83 (controller-mapping 5).
    private static CycleEntry SiemensEntry(
        string name,
        string native,
        string[] signature,
        Dictionary<string, string> parameters,
        string[] ruleWords,
        decimal? vari)
    {
        var fixedValues = new Dictionary<string, decimal>();
        if (vari is decimal value)
        {
            fixedValues["VARI"] = value;
        }

        return new CycleEntry
        {
            Name = name,
            Native = native,
            Signature = signature,
            Params = parameters,
            AbsoluteFromSurface = s_siemensAbsolute,
            Fixed = fixedValues,
            RuleWords = ruleWords,
        };
    }

    // RTP retract plane is SAFE, RFP reference plane SURFACE, SDIS the safety distance of CLEARANCE, DP the absolute
    // DEPTH, DTB the dwell (siemens 7, controller-mapping 5).
    private static Dictionary<string, string> SiemensPlanes()
    {
        return new Dictionary<string, string>
        {
            ["SAFE"] = "RTP",
            ["SURFACE"] = "RFP",
            ["CLEARANCE"] = "SDIS",
            ["DEPTH"] = "DP",
            ["CYCLE_DWELL"] = "DTB",
        };
    }

    // "The cycle feed is the modal F" (siemens 7): CYCLE81, CYCLE82 and CYCLE86 use it, and the reader turns it into
    // CYCLE_F, an address word before the call and no position of the signature (controller-mapping 5, machine-config
    // 6; wave-1 question #24).
    private static Dictionary<string, string> SiemensModalFeedParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["CYCLE_F"] = "F";
        return parameters;
    }

    // CYCLE83 uses the modal F as well and carries _AXN, the drilling axis, which is exactly AXIS (controller-mapping
    // 5, D59).
    // TODO(question): how the constant PECK of NCX is written with FDEP, FDPR and _DAM is not in the documents; PECK is
    // not mapped (cycles/siemens.toml; D181).
    private static Dictionary<string, string> SiemensPeckParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["AXIS"] = "_AXN";
        parameters["CYCLE_F"] = "F";
        return parameters;
    }

    // CYCLE84: PIT the pitch and _AXN the drilling axis (controller-mapping 5).
    // TODO(question): whether CYCLE84, rigid tapping with the feed from PIT and SST, takes the modal F is not in the
    // documents; CYCLE_F is not mapped (cycles/siemens.toml; D181).
    private static Dictionary<string, string> SiemensTapParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["PITCH"] = "PIT";
        parameters["AXIS"] = "_AXN";
        return parameters;
    }

    // CYCLE85 has its own feed FFR (controller-mapping 5).
    private static Dictionary<string, string> SiemensReamParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["CYCLE_F"] = "FFR";
        return parameters;
    }
}
