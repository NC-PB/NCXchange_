namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// A repeat on the repeat stack: REPEAT=label with TIMES re-executes the blocks from the label to the REPEAT block
/// (language 4.9, virtual machine 2.7, 3.6).
/// </summary>
/// <param name="RepeatPc">The index of the REPEAT block.</param>
/// <param name="Label">The label the repeat goes back to.</param>
/// <param name="Remaining">The passes still to run.</param>
public sealed record RepeatFrame(int RepeatPc, string Label, int Remaining);
