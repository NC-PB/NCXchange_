namespace Ncx.Core.Machine;

/// <summary>
/// [setpos]: the template written for SETPOS (machine-config 3). Heidenhain has none; the compiler folds SETPOS into
/// SHIFT there.
/// </summary>
public sealed record SetposConfig
{
    /// <summary>
    /// template: "G50 {axes}" on Fanuc system A, "PRESETON({axis},{value})" on Siemens; null when left out.
    /// </summary>
    public string? Template { get; init; }
}
