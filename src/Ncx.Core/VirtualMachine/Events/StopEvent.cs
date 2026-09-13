namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// STOP (virtual machine 7 as amended by F18): raised at STOP=PROGRAM, the program stop M0, and at STOP=OPTIONAL, the
/// optional stop M1 (language 4.1).
/// </summary>
public sealed record StopEvent : VmEvent
{
    /// <summary>
    /// True for STOP=OPTIONAL, false for STOP=PROGRAM.
    /// </summary>
    public required bool Optional { get; init; }

    /// <inheritdoc/>
    public override string Kind => "STOP";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return Optional ? "OPTIONAL" : "PROGRAM";
    }
}
