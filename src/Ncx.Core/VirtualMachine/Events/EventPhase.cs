namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The two events of a pair of virtual machine 7: FILE_BEGIN and FILE_END, PROGRAM_BEGIN and PROGRAM_END, SUB_BEGIN and
/// SUB_END, TOOL_BEGIN and TOOL_END.
/// </summary>
public enum EventPhase
{
    /// <summary>
    /// The first of the pair: the file, program or subprogram begins, the tool enters the spindle.
    /// </summary>
    Begin,

    /// <summary>
    /// The second of the pair: the file, program or subprogram ends, the tool leaves the spindle.
    /// </summary>
    End,
}
