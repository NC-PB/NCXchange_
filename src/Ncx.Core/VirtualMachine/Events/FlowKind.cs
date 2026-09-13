namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The flow words that raise a flow event, the row JUMP, CALL, RETURN, REPEAT of virtual machine 7 (language 4.9).
/// </summary>
public enum FlowKind
{
    /// <summary>
    /// JUMP=label, JUMP=END: continue at a label or at PROGRAM=END.
    /// </summary>
    Jump,

    /// <summary>
    /// CALL=name: enter a subprogram of the file or an external program.
    /// </summary>
    Call,

    /// <summary>
    /// RETURN: return to the caller before SUB=END.
    /// </summary>
    Return,

    /// <summary>
    /// REPEAT=label: repeat the blocks from the label TIMES more times.
    /// </summary>
    Repeat,
}
