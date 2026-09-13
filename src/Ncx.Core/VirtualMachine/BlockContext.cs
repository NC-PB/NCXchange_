using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// One block while the virtual machine executes it (virtual machine 3): the block, the channel state it changes, where
/// its diagnostics go, and what step 2 resolved, the resource each word addresses and the axis each axis word names
/// (3.8). The word handlers of step 3 and the frame and home rules of steps 4 and 5 read it.
/// </summary>
internal sealed class BlockContext
{
    /// <summary>
    /// The block that runs.
    /// </summary>
    public required Block Block { get; init; }

    /// <summary>
    /// The channel state the block changes.
    /// </summary>
    public required ChannelState State { get; init; }

    /// <summary>
    /// The machine file, or the built-in default machine of D103.
    /// </summary>
    public required MachineConfig Machine { get; init; }

    /// <summary>
    /// STATIC or INTERPRETED (virtual machine 1).
    /// </summary>
    public required ExecutionMode Mode { get; init; }

    /// <summary>
    /// The resolution of roles, axes, functions and coolant channels of the run (virtual machine 3.8).
    /// </summary>
    public required ResourceResolver Resources { get; init; }

    /// <summary>
    /// Where every diagnostic of the block goes (virtual machine 2.9).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// Where the diagnostics of the rules that depend on a caller's state go: the diagnostics of the run, or, inside a
    /// subprogram that no program of the file calls, a list nobody reads, because the state those rules check belongs
    /// to a caller that does not exist (virtual machine 3.9, 5, D99).
    /// </summary>
    public required Diagnostics CallerRuleDiagnostics { get; init; }

    /// <summary>
    /// The resource id each word addresses after step 2: the spindle of SPINDLE and RPM, the holder of TOOL, PRELOAD and
    /// OFFSET, the holder of WORKPIECE, the function of FUNC, the channel of COOLANT. A word that could not be resolved
    /// is missing and changes nothing (virtual machine 3.8).
    /// </summary>
    public Dictionary<Word, string> ResourceOf { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The key of the position store each axis word names after step 2: C of the current workpiece holder, Z2 of
    /// [[axis]] (virtual machine 3.8 rule 3). An axis word that could not be resolved is missing.
    /// </summary>
    public Dictionary<Word, string> AxisOf { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The two work spindles of SPINDLE_SYNC=a,b after step 2, the first leading and the second following (language
    /// 4.5); empty when the block has none or they could not be resolved.
    /// </summary>
    public List<string> SyncSpindles { get; } = [];

    /// <summary>
    /// The state key of a variable in ChannelState.Unknown: the key that sets it and, for a variable per resource or
    /// per axis, the resource id or axis name as its address, after the state keys of virtual machine 3.10: F, RPM:S1.
    /// </summary>
    /// <param name="key">The key of the word that sets the variable: RPM.</param>
    /// <param name="address">The resource id or axis name; null for a variable of the channel.</param>
    public static string StateKey(string key, string? address)
    {
        return address is null ? key : key + ":" + address;
    }

    /// <summary>
    /// The number of a word for the state variable it sets. A number is known; an expression is not evaluated in
    /// STATIC mode and the variable it sets becomes UNKNOWN (virtual machine 1): it is named in ChannelState.Unknown
    /// and keeps its value until a known one replaces it.
    /// </summary>
    /// <param name="word">A word whose value is a number or an expression.</param>
    /// <param name="stateKey">The state key of the variable the word sets.</param>
    /// <param name="number">The number; 0 when there is none.</param>
    /// <returns>True for a known number.</returns>
    public bool TryNumber(Word word, string stateKey, out decimal number)
    {
        switch (word.Value)
        {
            case IntegerValue integer:
                number = integer.Number;
                State.Unknown.Remove(stateKey);
                return true;
            case DecimalValue value:
                number = value.Number;
                State.Unknown.Remove(stateKey);
                return true;
            case ExprValue:
                // Expressions are not evaluated in STATIC mode; a state variable set from one becomes UNKNOWN (virtual
                // machine 1).
                // TODO: INTERPRETED mode evaluates the expression here (virtual machine 3.6, P4-01).
                State.Unknown.Add(stateKey);
                number = 0m;
                return false;
            default:
                number = 0m;
                return false;
        }
    }

    /// <summary>
    /// The identifier of a word, CW of SPINDLE=CW; null for a word whose value is no identifier.
    /// </summary>
    public static string? IdentOf(Word word)
    {
        return word.Value is IdentValue ident ? ident.Name : null;
    }

    /// <summary>
    /// An integer of the word as a register or tool number, 4 of OFFSET:LEN=4; null for a word whose value is no
    /// integer. A number beyond the range of an int is held at its limit: no control holds such a register.
    /// </summary>
    public static int? IntegerOf(Word word)
    {
        if (word.Value is not IntegerValue integer)
        {
            return null;
        }

        return (int)Math.Clamp(integer.Number, int.MinValue, int.MaxValue);
    }
}
