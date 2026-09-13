namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// How the feed of the rotary axes is counted, ROTARY_FEED (language 4.2, virtual machine 2.1, D86).
/// </summary>
public enum RotaryFeed
{
    /// <summary>
    /// ROTARY_FEED=MM_MIN: at the tool tip in length per minute (Heidenhain M116).
    /// </summary>
    MmMin,

    /// <summary>
    /// ROTARY_FEED=DEG_MIN: in degrees per minute (M117), the start value.
    /// </summary>
    DegMin,
}
