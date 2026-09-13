using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// A value on the restore stack: @SAVE pushed the state variable its state key names, and @RESTORE re-applies the
/// words as if the program had written them again (virtual machine 3.10, D95).
/// </summary>
/// <param name="Key">The state key of @SAVE: SPINDLE:MAIN, COOLANT, F.</param>
/// <param name="Words">The words that set the saved value again: SPINDLE:MAIN=CW RPM:MAIN=1500.</param>
public sealed record RestoreEntry(StateKeyValue Key, IReadOnlyList<Word> Words)
{
    /// <summary>
    /// The state variable the entry belongs to, by the key that sets it and the resource id, coolant channel or
    /// function its state key names: SPINDLE:S1, COOLANT:STANDARD, F. @RESTORE pops the newest entry of its own
    /// variable, one stack per state variable (virtual machine 3.10).
    /// </summary>
    public string Variable { get; init; } = Key.ToCanonical();

    /// <summary>
    /// The state keys that were UNKNOWN when @SAVE saved them, RPM:S1: no word sets them again, and @RESTORE leaves
    /// them UNKNOWN (virtual machine 1).
    /// </summary>
    public IReadOnlyList<string> UnknownKeys { get; init; } = [];
}
