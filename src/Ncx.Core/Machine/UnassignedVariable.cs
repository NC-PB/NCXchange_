namespace Ncx.Core.Machine;

/// <summary>
/// [variables] unassigned: what reading a variable that was never assigned does (machine-config 7, virtual machine
/// 3.6, D38).
/// </summary>
public enum UnassignedVariable
{
    /// <summary>
    /// unassigned = "error": an ERROR; the default.
    /// </summary>
    Error,

    /// <summary>
    /// unassigned = 0: the variable reads as 0.
    /// </summary>
    Zero,
}
