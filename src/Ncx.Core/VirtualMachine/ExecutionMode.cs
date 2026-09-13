namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The two execution modes of the virtual machine (virtual machine 1).
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// STATIC: one pass over every program of the file; JUMP and REPEAT recorded, not followed; every CALL of a
    /// subprogram of the file followed with the caller's state; expressions not evaluated. What convert, compile and
    /// check use (D91, D99).
    /// </summary>
    Static,

    /// <summary>
    /// INTERPRETED: the program executed, variables evaluated, jumps and calls followed, with a block cap. What analyze
    /// uses (virtual machine 1), and ncx trace --interpreted.
    /// </summary>
    Interpreted,
}
