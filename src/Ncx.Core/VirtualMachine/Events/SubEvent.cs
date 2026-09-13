using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// SUB_BEGIN and SUB_END (virtual machine 7): raised at SUB=BEGIN and at SUB=END, where the walk leaves the
/// subprogram, with its name and its caller.
/// </summary>
public sealed record SubEvent : VmEvent
{
    /// <summary>
    /// SUB_BEGIN or SUB_END.
    /// </summary>
    public required EventPhase Phase { get; init; }

    /// <summary>
    /// The NAME of the subprogram as CALL finds it (language 4.9).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The program or subprogram whose CALL entered the subprogram; null for a subprogram that no program of the file
    /// calls, which STATIC mode walks once on its own (virtual machine 3.9, D99).
    /// </summary>
    public Section? Caller { get; init; }

    /// <inheritdoc/>
    public override string Kind => Phase == EventPhase.Begin ? "SUB_BEGIN" : "SUB_END";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        string caller = Caller is null ? "none" : EventText.SectionName(Caller);
        return $"name {Name}, caller {caller}";
    }
}
