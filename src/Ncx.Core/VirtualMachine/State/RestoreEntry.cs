using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// A value on the restore stack: @SAVE pushed the state variable its state key names, and @RESTORE re-applies the
/// words as if the program had written them again (virtual machine 3.10, D95).
/// </summary>
/// <param name="Key">The state key of @SAVE: SPINDLE:MAIN, COOLANT, F.</param>
/// <param name="Words">The words that set the saved value again: SPINDLE:MAIN=CW RPM:MAIN=1500.</param>
public sealed record RestoreEntry(StateKeyValue Key, IReadOnlyList<Word> Words);
