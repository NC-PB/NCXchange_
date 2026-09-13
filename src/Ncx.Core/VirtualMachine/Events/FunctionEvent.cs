using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// FUNCTION (virtual machine 7 as amended by F18): raised at every FUNC, MFUNC and COOLANT word, the machine functions
/// the compiler writes; MFUNC sets no state and is only this event (language 4.6, virtual machine 2.5).
/// </summary>
public sealed record FunctionEvent : VmEvent
{
    /// <summary>
    /// The word as written: FUNC:SUB_CHUCK=OPEN, MFUNC=136, COOLANT=ON.
    /// </summary>
    public required Word Word { get; init; }

    /// <summary>
    /// The function of FUNC or the coolant channel of COOLANT, STANDARD for a bare COOLANT (virtual machine 2.5, F29);
    /// null for MFUNC.
    /// </summary>
    public string? Name { get; init; }

    /// <inheritdoc/>
    public override string Kind => "FUNCTION";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return Name is null ? Word.ToCanonical() : Word.ToCanonical() + " on " + Name;
    }
}
