namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// A call on the call stack: what CALL entered and where the flow returns to at SUB=END or RETURN (virtual machine
/// 2.7, 3.6). The locals of the caller are kept by the variable store (PushLocals).
/// </summary>
/// <param name="ReturnPc">The index of the block the flow continues at after the return.</param>
/// <param name="Target">The NAME of the called subprogram, or the external program of CALL="...".</param>
public sealed record CallFrame(int ReturnPc, string Target);
