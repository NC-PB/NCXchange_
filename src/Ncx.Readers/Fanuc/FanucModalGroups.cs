using Ncx.Core.Machine;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The modal G groups of controllers fanuc.md 3 by the G-code system of the machine: one value per group is active
/// until another G of the same group appears. Mills and the lathes of systems B and C have G90/G91 and G94/G95;
/// system A lathes have neither, G90, G92 and G94 are their turning cycles and G98/G99 their feed mode
/// (machine-config 1, gcode_system).
/// </summary>
internal static class FanucModalGroups
{
    /// <summary>Group 01: G0, G1, G2, G3, G33; on lathes also the drilling and the simple turning cycles.</summary>
    public const int Motion = 1;

    /// <summary>Group 02: G17, G18, G19.</summary>
    public const int Plane = 2;

    /// <summary>Group 03: G90, G91 (mills, systems B and C).</summary>
    public const int Distance = 3;

    /// <summary>Group 05: G94, G95 (mills, systems B and C), G98, G99 (system A).</summary>
    public const int FeedMode = 5;

    /// <summary>Group 06: G20, G21 (G70, G71 in system C).</summary>
    public const int Units = 6;

    /// <summary>Group 07: G40, G41, G42.</summary>
    public const int Compensation = 7;

    /// <summary>Group 08: G43, G44, G49, and G43.4, G43.5 of tool center point control.</summary>
    public const int LengthOffset = 8;

    /// <summary>Group 09: the canned cycles of a mill, G73 to G89, G80 cancels.</summary>
    public const int Cycle = 9;

    /// <summary>Group 10: G98, G99, the return level of a drilling cycle (mills, systems B and C).</summary>
    public const int ReturnLevel = 10;

    /// <summary>Group 12: G54 to G59, G54.1.</summary>
    public const int Origin = 12;

    /// <summary>Group 13: G61 to G64.</summary>
    public const int CuttingMode = 13;

    /// <summary>Group 14: G66, G67.</summary>
    public const int MacroCall = 14;

    /// <summary>Group 16: G68, G69.</summary>
    public const int Rotation = 16;

    /// <summary>Group 17: G15, G16.</summary>
    public const int PolarCoordinates = 17;

    // The drilling cycles of a lathe, along Z on the face and along X on the circumference (controllers fanuc.md 6).
    private static readonly string[] s_latheDrilling = ["G80", "G83", "G84", "G85", "G87", "G88", "G89"];

    private static readonly Dictionary<string, int> s_mill = Mill();
    private static readonly Dictionary<string, int> s_systemA = SystemA();
    private static readonly Dictionary<string, int> s_systemB = SystemBOrC(units: ["G20", "G21"]);

    // TODO(question): fanuc 3 gives system C its turning cycles as G20, G21, G24, while controller-mapping 5 and
    // language 4.7.1 give G77, G78, G79 in B and C (wave-1 question #16); system C takes G77 to G79 as B does.
    private static readonly Dictionary<string, int> s_systemC = SystemBOrC(units: ["G70", "G71"]);

    /// <summary>
    /// The group of every modal code of the G-code system, by the code without leading zeros (D105).
    /// </summary>
    /// <param name="system">The gcode_system of the machine; null for a mill (machine-config 1).</param>
    public static IReadOnlyDictionary<string, int> For(GcodeSystem? system)
    {
        return system switch
        {
            GcodeSystem.A => s_systemA,
            GcodeSystem.B => s_systemB,
            GcodeSystem.C => s_systemC,
            _ => s_mill,
        };
    }

    // The mill: the cycles belong to group 09, G98/G99 are the return level (controllers fanuc.md 3).
    private static Dictionary<string, int> Mill()
    {
        Dictionary<string, int> groups = Common();
        Add(groups, Distance, "G90", "G91");
        Add(groups, FeedMode, "G94", "G95");
        Add(groups, Units, "G20", "G21");
        Add(groups, LengthOffset, "G43", "G44", "G49", "G43.4", "G43.5");
        Add(groups, Cycle, "G73", "G74", "G76", "G80", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89");
        Add(groups, ReturnLevel, "G98", "G99");
        return groups;
    }

    // System A: no G90/G91, U W H V are the incremental addresses; G90, G92, G94 turning cycles; G98/G99 feed per
    // minute and per revolution (controllers fanuc.md 3).
    private static Dictionary<string, int> SystemA()
    {
        Dictionary<string, int> groups = Common();
        Add(groups, Motion, "G90", "G92", "G94");
        Add(groups, Motion, s_latheDrilling);
        Add(groups, FeedMode, "G98", "G99");
        Add(groups, Units, "G20", "G21");
        return groups;
    }

    // Systems B and C: G90/G91, G94/G95, G98/G99 as the return level, turning cycles G77, G78, G79 (controllers
    // fanuc.md 3).
    private static Dictionary<string, int> SystemBOrC(string[] units)
    {
        Dictionary<string, int> groups = Common();
        Add(groups, Motion, "G77", "G78", "G79");
        Add(groups, Motion, s_latheDrilling);
        Add(groups, Distance, "G90", "G91");
        Add(groups, FeedMode, "G94", "G95");
        Add(groups, Units, units);
        Add(groups, ReturnLevel, "G98", "G99");
        return groups;
    }

    // The groups every system shares (controllers fanuc.md 3).
    private static Dictionary<string, int> Common()
    {
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        Add(groups, Motion, "G0", "G1", "G2", "G3", "G33");
        Add(groups, Plane, "G17", "G18", "G19");
        Add(groups, Compensation, "G40", "G41", "G42");
        Add(groups, Origin, "G54", "G55", "G56", "G57", "G58", "G59", "G54.1");
        Add(groups, CuttingMode, "G61", "G62", "G63", "G64");
        Add(groups, MacroCall, "G66", "G67");
        Add(groups, Rotation, "G68", "G69");
        Add(groups, PolarCoordinates, "G15", "G16");
        return groups;
    }

    private static void Add(Dictionary<string, int> groups, int group, params string[] codes)
    {
        foreach (string code in codes)
        {
            groups[code] = group;
        }
    }
}
