namespace Ncx.Core.Machine;

/// <summary>
/// [[resource]] type: the kinds of resource of version 1.0 (machine-config 4, D41).
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// type = "work_spindle": a spindle that holds the workpiece, MAIN, SUB.
    /// </summary>
    WorkSpindle,

    /// <summary>
    /// type = "tool_spindle": a spindle that turns the tool.
    /// </summary>
    ToolSpindle,

    /// <summary>
    /// type = "tool_holder": a turret or a tool carrier that takes TOOL and PRELOAD.
    /// </summary>
    ToolHolder,

    /// <summary>
    /// type = "table": the machine table that holds the workpiece of a mill.
    /// </summary>
    Table,
}
