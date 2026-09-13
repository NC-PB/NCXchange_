namespace Ncx.Core.Machine;

/// <summary>
/// [transform]: the templates of the transformations, tilted planes and rotary axis options (machine-config 5). An
/// empty template is kept as empty: the target cannot write the word (D82).
/// </summary>
public sealed record TransformTable
{
    /// <summary>
    /// CYLINDER_ON: written for CYLINDER=n, "G7.1 C{r}" with {r} the reference radius (D96); null when left out.
    /// </summary>
    public string? CylinderOn { get; init; }

    /// <summary>
    /// CYLINDER_OFF: written for CYLINDER=OFF, "G7.1 C0"; null when left out.
    /// </summary>
    public string? CylinderOff { get; init; }

    /// <summary>
    /// POLAR_ON: written for POLAR=ON, "G12.1", "TRANSMIT"; null when left out.
    /// </summary>
    public string? PolarOn { get; init; }

    /// <summary>
    /// POLAR_OFF: written for POLAR=OFF, "G13.1"; null when left out.
    /// </summary>
    public string? PolarOff { get; init; }

    /// <summary>
    /// TCPM_ON: written for TCPM=ON, "G43.4 H{offset}", "TRAORI"; null when left out.
    /// </summary>
    public string? TcpmOn { get; init; }

    /// <summary>
    /// TCPM_OFF: written for TCPM=OFF, "G49"; null when left out.
    /// </summary>
    public string? TcpmOff { get; init; }

    /// <summary>
    /// TILT_ON: written for TILT, "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"; null when left out.
    /// </summary>
    public string? TiltOn { get; init; }

    /// <summary>
    /// TILT_OFF: written for TILT=RESET, "G69"; null when left out.
    /// </summary>
    public string? TiltOff { get; init; }

    /// <summary>
    /// TILT_AXIS_ON: written for TILT_AXIS, "PLANE AXIAL A{a} B{b} C{c} {move}"; empty when the target needs the
    /// kinematics module (D82); null when left out.
    /// </summary>
    public string? TiltAxisOn { get; init; }

    /// <summary>
    /// TILT_TURN: written after TILT_ON for MOVE=TURN where the control positions the axes in a second block, "G53.1";
    /// null when left out.
    /// </summary>
    public string? TiltTurn { get; init; }

    /// <summary>
    /// ROTARY_PATH_SHORTEST: written for ROTARY_PATH=SHORTEST, "M126" (D86); null when left out.
    /// </summary>
    public string? RotaryPathShortest { get; init; }

    /// <summary>
    /// ROTARY_PATH_FULL: written for ROTARY_PATH=FULL, "M127" (D86); null when left out.
    /// </summary>
    public string? RotaryPathFull { get; init; }

    /// <summary>
    /// ROTARY_FEED_MM_MIN: written for ROTARY_FEED=MM_MIN, "M116" (D86); null when left out.
    /// </summary>
    public string? RotaryFeedMmMin { get; init; }

    /// <summary>
    /// ROTARY_FEED_DEG_MIN: written for ROTARY_FEED=DEG_MIN, "M117" (D86); null when left out.
    /// </summary>
    public string? RotaryFeedDegMin { get; init; }

    /// <summary>
    /// move: the value of {move} per MOVE option, TURN = "TURN FMAX", STAY = "STAY" (Heidenhain); empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Move { get; init; } = new Dictionary<string, string>();
}
