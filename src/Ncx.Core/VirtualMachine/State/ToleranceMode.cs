namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// What the control optimizes for under the path tolerance, TOLERANCE_MODE (language 4.1, virtual machine 2.1, D85).
/// </summary>
public enum ToleranceMode
{
    /// <summary>
    /// TOLERANCE_MODE=FINISH: accuracy, the start value.
    /// </summary>
    Finish,

    /// <summary>
    /// TOLERANCE_MODE=ROUGH: speed.
    /// </summary>
    Rough,
}
