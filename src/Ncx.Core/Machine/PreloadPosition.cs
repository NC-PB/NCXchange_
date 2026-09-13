namespace Ncx.Core.Machine;

/// <summary>
/// [tool_change] preload_position: where the compiler puts the PRELOAD it inserts under auto_preload
/// (machine-config 3).
/// </summary>
public enum PreloadPosition
{
    /// <summary>
    /// preload_position = "after_change": right after the tool change.
    /// </summary>
    AfterChange,

    /// <summary>
    /// preload_position = "before_first_motion": before the first motion after the change.
    /// </summary>
    BeforeFirstMotion,
}
