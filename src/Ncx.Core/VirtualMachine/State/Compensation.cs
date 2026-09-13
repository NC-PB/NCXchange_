namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The cutter radius compensation of COMP (language 4.4, virtual machine 2.2).
/// </summary>
public enum Compensation
{
    /// <summary>
    /// COMP=OFF (G40, R0), the start value.
    /// </summary>
    Off,

    /// <summary>
    /// COMP=LEFT (G41, RL).
    /// </summary>
    Left,

    /// <summary>
    /// COMP=RIGHT (G42, RR).
    /// </summary>
    Right,
}
