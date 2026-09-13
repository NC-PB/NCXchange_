namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The way the rotary axes take to their target, ROTARY_PATH (language 4.2, virtual machine 2.1, D86).
/// </summary>
public enum RotaryPath
{
    /// <summary>
    /// ROTARY_PATH=SHORTEST: the shortest way (Heidenhain M126).
    /// </summary>
    Shortest,

    /// <summary>
    /// ROTARY_PATH=FULL: exactly as programmed, also the long way round (M127), the start value.
    /// </summary>
    Full,
}
