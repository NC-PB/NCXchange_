namespace Ncx.Core.Machine;

/// <summary>
/// [dynamics] path_mode: how the control runs through corners, for the runtime estimate (machine-config 4, D64).
/// </summary>
public enum PathMode
{
    /// <summary>
    /// path_mode = "continuous": corner_speed is carried through a corner.
    /// </summary>
    Continuous,

    /// <summary>
    /// path_mode = "exact_stop": every block ends at standstill.
    /// </summary>
    ExactStop,
}
