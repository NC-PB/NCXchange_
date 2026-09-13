namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The tool change state of a holder (architecture 5.2): Empty, Loaded or Pending.
/// </summary>
public enum ToolChangeState
{
    /// <summary>
    /// No tool in the spindle and nothing preloaded: a holder as it is created, and after TOOL=0.
    /// </summary>
    Empty,

    /// <summary>
    /// A tool in the spindle and nothing preloaded.
    /// </summary>
    Loaded,

    /// <summary>
    /// A tool preloaded, with or without a tool in the spindle.
    /// </summary>
    Pending,
}
