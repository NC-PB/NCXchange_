namespace Ncx.Core.Machine;

/// <summary>
/// [tolerance]: the templates of the TOLERANCE words (machine-config 5, D85).
/// </summary>
public sealed record ToleranceTable
{
    /// <summary>
    /// ON: written for TOLERANCE=value, "G5.1 Q1", "CYCLE832({tol},{mode},{rotary})"; null when left out.
    /// </summary>
    public string? On { get; init; }

    /// <summary>
    /// OFF: written for TOLERANCE=OFF, "G5.1 Q0"; null when left out.
    /// </summary>
    public string? Off { get; init; }

    /// <summary>
    /// mode: the value of {mode} per TOLERANCE_MODE, FINISH = "0", ROUGH = "1"; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Mode { get; init; } = new Dictionary<string, string>();
}
