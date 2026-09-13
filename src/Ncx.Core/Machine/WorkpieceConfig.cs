namespace Ncx.Core.Machine;

/// <summary>
/// [workpiece]: the native selection of the holder the turret works on, per role, and how that side is programmed
/// (machine-config 5, D57).
/// </summary>
public sealed record WorkpieceConfig
{
    /// <summary>
    /// The selection template per holder role, MAIN = "G54 M428", SUB = "G59 M427".
    /// </summary>
    public IReadOnlyDictionary<string, string> Templates { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// ROLE_frame per holder role, SUB_frame = "datum": whether the compiler negates Z against a datum or writes the
    /// mirror cycle (D57).
    /// </summary>
    public IReadOnlyDictionary<string, WorkpieceFrame> Frames { get; init; } =
        new Dictionary<string, WorkpieceFrame>();

    /// <summary>
    /// ROLE_mirror per holder role, SUB_mirror = { ON = "G360", OFF = "G361" }: the mirror cycle of that side.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> Mirrors { get; init; } =
        new Dictionary<string, FunctionTable>();
}
