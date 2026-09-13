using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Maps an M code to the word of the machine's function tables, codes compared by number (machine-config 5, D105):
/// [spindle.ROLE] to SPINDLE:role, [spindle_mode.ROLE] to SPINDLE_MODE:role, [spindle_sync] to SPINDLE_SYNC, [coolant]
/// to COOLANT:channel and [func] to FUNC:name. The address of the default spindle and of the default coolant channel is
/// left out, because a word without a role address targets the default resource (language 4.6, 4.10).
/// </summary>
internal static class FanucFunctions
{
    // The states of [spindle.ROLE] that SPINDLE takes as its value (machine-config 5, language 4.5).
    private static readonly string[] s_spindleStates = ["CW", "CCW", "OFF"];

    /// <summary>
    /// The word of an M code; null when no table names it, or names it in a state NCX writes otherwise.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="code">The M word.</param>
    /// <param name="parameter">The P word that belongs to the M code, M3 P11 on the Doosan; null when none
    /// does.</param>
    public static FanucFunction? Of(FanucBlock block, SourceWord code, out SourceWord? parameter)
    {
        // A table state may carry a P with its M code, M3 P11 on the Doosan (controller-mapping 4); the pair is tried
        // before the code alone, both compared by number (D105).
        parameter = null;
        string? native = NativeCode.Of(code);
        if (native is null)
        {
            return null;
        }

        string? state = null;
        SourceWord? pWord = block.Find("P");
        if (pWord?.Number is not null)
        {
            state = block.Templates.FindFunctionByCode(native + " P" + pWord.Text);
            parameter = state is null ? null : pWord;
        }

        state ??= block.Templates.FindFunctionByCode(native);
        return state is null ? null : WordOf(block, state);
    }

    /// <summary>
    /// Tells whether an M code is the PHASE of the machine's [spindle_sync], Nakamura M92 (machine-config 5;
    /// controller-mapping 4, SPINDLE_SYNC and PHASE).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="code">The M word.</param>
    public static bool IsPhaseCode(FanucBlock block, SourceWord code)
    {
        return NativeCode.Of(code) is string native
            && block.Templates.FindFunctionByCode(native) == "SPINDLE_SYNC=PHASE";
    }

    /// <summary>
    /// The role address a spindle word carries: none for the default spindle of the machine (language 4.10, virtual
    /// machine 3.8 rule 2), the role otherwise.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="role">The spindle role; null for the default spindle.</param>
    public static string? SpindleAddress(FanucBlock block, string? role)
    {
        if (role is null)
        {
            return null;
        }

        ResourceDef? defaultSpindle = block.Machine.ResolveDefaultSpindle();
        ResourceDef? spindle = block.Machine.ResolveRole(role);
        return defaultSpindle is not null && spindle?.Id == defaultSpindle.Id ? null : role;
    }

    // The state names the word of its table with the key of the state as its value, SPINDLE:TOOL=CW; the language word
    // is made from it (wave-1 question #62).
    private static FanucFunction? WordOf(FanucBlock block, string state)
    {
        string[] keyAndValue = state.Split('=');
        string[] keyAndAddr = keyAndValue[0].Split(':');
        string key = keyAndAddr[0];
        string? addr = keyAndAddr.Length > 1 ? keyAndAddr[1] : null;
        string value = keyAndValue[1];
        switch (key)
        {
            case "SPINDLE" when s_spindleStates.Contains(value):
                return new FanucFunction(key, SpindleAddress(block, addr), new IdentValue(value), addr);
            case "SPINDLE" when value == "ORIENT":
                // TODO(question): ORIENT takes the angle as its value (language 4.5) and a bare M19 gives none
                // (wave-1 question #62); the reader writes ORIENT=0, the reference angle of the spindle.
                return new FanucFunction("ORIENT", SpindleAddress(block, addr), new IntegerValue(0, "0"), addr);
            case "SPINDLE_MODE":
                return new FanucFunction(key, addr, new IdentValue(value), null);
            case "SPINDLE_SYNC":
                return SyncOf(block, value);
            case "COOLANT":
                // The channel STANDARD is the default channel that a bare COOLANT addresses (language 4.6, F29).
                return new FanucFunction(key, addr == "STANDARD" ? null : addr, new IdentValue(value), null);
            case "FUNC":
                return new FanucFunction(key, addr, new IdentValue(value), null);
            default:
                return null;
        }
    }

    // SPINDLE_SYNC takes the two spindles it couples or OFF (language 4.5); [spindle_sync] names the codes and not the
    // spindles.
    // TODO(question): the documents do not say which spindles the ON code of [spindle_sync] couples (wave-1 question
    // #62); the reader takes the two work spindles of the machine in the order of [roles], MAIN,SUB, and writes the
    // code as MFUNC when the machine has not two. PHASE needs the angle as its value, which the code does not carry
    // (FanucBuilder keeps the PHASE code as RAW).
    private static FanucFunction? SyncOf(FanucBlock block, string value)
    {
        if (value == "OFF")
        {
            return new FanucFunction("SPINDLE_SYNC", null, new IdentValue(value), null);
        }

        if (value != "ON")
        {
            return null;
        }

        var workSpindles = new List<string>();
        foreach (KeyValuePair<string, string> role in block.Machine.Roles)
        {
            if (block.Machine.FindResource(role.Value)?.Type == ResourceType.WorkSpindle)
            {
                workSpindles.Add(role.Key);
            }
        }

        return workSpindles.Count == 2
            ? new FanucFunction("SPINDLE_SYNC", null, new ListValue(workSpindles), null)
            : null;
    }
}
