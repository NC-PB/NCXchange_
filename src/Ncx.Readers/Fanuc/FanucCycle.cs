using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The active cycle of a Fanuc source with its modal values: the code, the catalog entry that maps it, and the values
/// the control keeps from block to block until G80, the depth, R, Q, P, F and the return level (controllers fanuc.md
/// 6). The reader writes a CYCLE block from them before a call whenever they differ from the one written last, because
/// a new CYCLE in NCX replaces all parameters (language 4.7).
/// </summary>
internal sealed class FanucCycle
{
    /// <summary>
    /// The G code of the cycle, "G81".
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The catalog entry of the cycle; null when the catalog has none and the cycle's blocks stay RAW.
    /// </summary>
    public CycleEntry? Entry { get; init; }

    /// <summary>
    /// True for a simple turning cycle, G90, G92, G94 of system A (language 4.7.1).
    /// </summary>
    public bool IsTurning { get; init; }

    /// <summary>
    /// The drilling axis, Z on a mill in G17 and on the face of a lathe, X on its circumference (language 4.7, AXIS).
    /// </summary>
    public string Axis { get; init; } = "Z";

    /// <summary>
    /// True when the cycle names its drilling axis with AXIS: every cycle of a lathe (controller-mapping 5, D59).
    /// </summary>
    public bool WritesAxis { get; init; }

    /// <summary>
    /// The absolute depth along the drilling axis, DEPTH; null while the source has given none.
    /// </summary>
    public Value? Depth { get; set; }

    /// <summary>
    /// The absolute clearance plane, CLEARANCE; null while the source has given none.
    /// </summary>
    public Value? Clearance { get; set; }

    /// <summary>
    /// The peck depth Q, PECK.
    /// </summary>
    public Value? Peck { get; set; }

    /// <summary>
    /// The dwell at the bottom in seconds, CYCLE_DWELL, from P in milliseconds.
    /// </summary>
    public Value? Dwell { get; set; }

    /// <summary>
    /// The feed of the cycle, CYCLE_F.
    /// </summary>
    public Value? Feed { get; set; }

    /// <summary>
    /// The initial level: where the drilling axis stood when the cycle mode began, SAFE under G98; null when the reader
    /// does not know it.
    /// </summary>
    public decimal? InitialLevel { get; init; }

    /// <summary>
    /// True under G98, the return to the initial level; false under G99, the return to the R level.
    /// </summary>
    public bool ReturnsToInitialLevel { get; set; }

    /// <summary>
    /// The words of the CYCLE block written last, as canonical text; null before the first.
    /// </summary>
    public string? Written { get; set; }

    /// <summary>
    /// A copy with the same values, the cycle a subprogram runs with (virtual machine 3.9).
    /// </summary>
    public FanucCycle Copy()
    {
        return new FanucCycle
        {
            Code = Code,
            Entry = Entry,
            IsTurning = IsTurning,
            Axis = Axis,
            WritesAxis = WritesAxis,
            Depth = Depth,
            Clearance = Clearance,
            Peck = Peck,
            Dwell = Dwell,
            Feed = Feed,
            InitialLevel = InitialLevel,
            ReturnsToInitialLevel = ReturnsToInitialLevel,
            Written = Written,
        };
    }

    /// <summary>
    /// The cycle as text, for comparing the cycles two callers leave active.
    /// </summary>
    public string ToKey()
    {
        return string.Join("|",
            Code,
            Entry?.Name ?? "",
            IsTurning ? "turning" : "drilling",
            Axis,
            WritesAxis ? "axis" : "",
            Depth?.ToCanonical() ?? "",
            Clearance?.ToCanonical() ?? "",
            Peck?.ToCanonical() ?? "",
            Dwell?.ToCanonical() ?? "",
            Feed?.ToCanonical() ?? "",
            InitialLevel?.ToString(CultureInfo.InvariantCulture) ?? "",
            ReturnsToInitialLevel ? "G98" : "G99",
            Written ?? "");
    }
}
