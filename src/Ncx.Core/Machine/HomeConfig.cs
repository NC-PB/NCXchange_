namespace Ncx.Core.Machine;

/// <summary>
/// [home]: the templates written for HOME (machine-config 3). A machine without them (Heidenhain) gets a machine-frame
/// move to the home of each [[axis]] from the compiler.
/// </summary>
public sealed record HomeConfig
{
    /// <summary>
    /// template: reference point 1, "G28 {axes}"; null when left out.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// point: the second and further reference points, "G30 P{point} {axes}"; null when left out.
    /// </summary>
    public string? Point { get; init; }
}
