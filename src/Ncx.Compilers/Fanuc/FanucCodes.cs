using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The G codes the Fanuc compiler writes, by the modal group of controllers fanuc.md 3, in the G-code system of the
/// machine: the codes of the plane, the feed mode, the units, the compensation and the distance mode, the keys under
/// which the target state keeps them, and the place of a group in a written block.
/// </summary>
internal static class FanucCodes
{
    /// <summary>
    /// Group 01, the motion (controllers fanuc.md 3).
    /// </summary>
    public const string Motion = "G01";

    /// <summary>
    /// Group 02, the plane.
    /// </summary>
    public const string Plane = "G02";

    /// <summary>
    /// Group 03, absolute or incremental.
    /// </summary>
    public const string Distance = "G03";

    /// <summary>
    /// Group 05, the feed mode.
    /// </summary>
    public const string FeedMode = "G05";

    /// <summary>
    /// Group 06, inch or metric.
    /// </summary>
    public const string Units = "G06";

    /// <summary>
    /// Group 07, the cutter radius compensation.
    /// </summary>
    public const string Comp = "G07";

    /// <summary>
    /// Group 08, the tool length offset.
    /// </summary>
    public const string Length = "G08";

    /// <summary>
    /// Group 09, the canned cycles of a mill; the lathe cycles as well, whatever their group.
    /// </summary>
    public const string Cycle = "G09";

    /// <summary>
    /// Group 10, the return level of a mill cycle.
    /// </summary>
    public const string ReturnLevel = "G10";

    /// <summary>
    /// Group 12, the workpiece coordinate system.
    /// </summary>
    public const string Origin = "G12";

    /// <summary>
    /// The one-shot codes of group 00, G28, G53, G52, G92: after every modal code of a block.
    /// </summary>
    public const int OneShot = 100;

    // The codes of the groups a start block of the machine may write, by the key of their group (controllers fanuc.md
    // 3): G98 and G99 are the return level on a mill and in systems B and C, the feed mode in system A.
    private static readonly Dictionary<string, string> s_groupOfCode = new(StringComparer.Ordinal)
    {
        ["G0"] = Motion,
        ["G1"] = Motion,
        ["G2"] = Motion,
        ["G3"] = Motion,
        ["G17"] = Plane,
        ["G18"] = Plane,
        ["G19"] = Plane,
        ["G90"] = Distance,
        ["G91"] = Distance,
        ["G94"] = FeedMode,
        ["G95"] = FeedMode,
        ["G20"] = Units,
        ["G21"] = Units,
        ["G40"] = Comp,
        ["G41"] = Comp,
        ["G42"] = Comp,
        ["G43"] = Length,
        ["G49"] = Length,
        ["G80"] = Cycle,
        ["G98"] = ReturnLevel,
        ["G99"] = ReturnLevel,
        ["G54"] = Origin,
        ["G55"] = Origin,
        ["G56"] = Origin,
        ["G57"] = Origin,
        ["G58"] = Origin,
        ["G59"] = Origin,
    };

    /// <summary>
    /// The key of the modal group of a code the compiler knows, in the G-code system of the machine; null for any
    /// other code (controllers fanuc.md 3).
    /// </summary>
    /// <param name="code">The code as written, "G94".</param>
    /// <param name="system">The G-code system of a lathe; null for a mill.</param>
    public static string? GroupOf(string code, GcodeSystem? system)
    {
        // System A: G98 and G99 are the feed mode, G90 and G91 no codes, U W H V the incremental addresses; system C:
        // G70 and G71 are inch and metric (controllers fanuc.md 3).
        if (system == GcodeSystem.A)
        {
            if (code is "G98" or "G99")
            {
                return FeedMode;
            }

            if (code is "G90" or "G91" or "G94" or "G95")
            {
                return null;
            }
        }

        if (system == GcodeSystem.C && code is "G70" or "G71")
        {
            return Units;
        }

        if (system == GcodeSystem.C && code is "G20" or "G21")
        {
            return null;
        }

        return s_groupOfCode.TryGetValue(code, out string? group) ? group : null;
    }

    /// <summary>
    /// The place of a modal group in a written block: the motion first, then the plane, the distance mode and the other
    /// groups by their number, the one-shot codes last, as the sources write G0 G17 X50.4 and G91 G28 Z0.
    /// </summary>
    /// <param name="group">The key of the group, "G01".</param>
    public static int RankOf(string group)
    {
        return int.Parse(group.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// G17, G18, G19 for the working plane (language 4.2, WORKPLANE).
    /// </summary>
    public static string PlaneCode(Workplane plane)
    {
        return plane switch
        {
            Workplane.ZX => "G18",
            Workplane.YZ => "G19",
            _ => "G17",
        };
    }

    /// <summary>
    /// The code of the feed mode: G94 and G95, G98 and G99 in system A (controllers fanuc.md 3, controller-mapping 2).
    /// </summary>
    public static string FeedModeCode(FeedMode mode, GcodeSystem? system)
    {
        bool perRevolution = mode == Ncx.Core.VirtualMachine.State.FeedMode.PerRev;
        if (system == GcodeSystem.A)
        {
            return perRevolution ? "G99" : "G98";
        }

        return perRevolution ? "G95" : "G94";
    }

    /// <summary>
    /// The code of the units: G20 and G21, G70 and G71 in system C (controllers fanuc.md 3); null while they are
    /// unknown.
    /// </summary>
    public static string? UnitsCode(Units units, GcodeSystem? system)
    {
        bool systemC = system == GcodeSystem.C;
        return units switch
        {
            Ncx.Core.VirtualMachine.State.Units.Mm => systemC ? "G71" : "G21",
            Ncx.Core.VirtualMachine.State.Units.Inch => systemC ? "G70" : "G20",
            _ => null,
        };
    }

    /// <summary>
    /// G40, G41, G42 for the compensation (controller-mapping 2, COMP).
    /// </summary>
    public static string CompCode(Compensation comp)
    {
        return comp switch
        {
            Compensation.Left => "G41",
            Compensation.Right => "G42",
            _ => "G40",
        };
    }
}
