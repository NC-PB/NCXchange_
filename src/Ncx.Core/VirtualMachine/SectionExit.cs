namespace Ncx.Core.VirtualMachine;

/// <summary>
/// How INTERPRETED mode leaves a program or a subprogram it runs (virtual machine 3.6).
/// </summary>
internal enum SectionExit
{
    /// <summary>
    /// SUB=END or RETURN returned to the caller; a called external program returns at its PROGRAM=END.
    /// </summary>
    Returned,

    /// <summary>
    /// JUMP=END from inside a subprogram: the calls unwind, and the program continues at its PROGRAM=END.
    /// </summary>
    JumpedToEnd,

    /// <summary>
    /// PROGRAM=END ended the program: the channel is finished.
    /// </summary>
    ProgramEnded,

    /// <summary>
    /// An ERROR stopped the run (virtual machine 2.9).
    /// </summary>
    Stopped,
}
