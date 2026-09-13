namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// DWELL (virtual machine 7 as amended by F18): raised at DWELL with its seconds, which the runtime estimate charges
/// (language 4.1, virtual machine 8).
/// </summary>
public sealed record DwellEvent : VmEvent
{
    /// <summary>
    /// The dwell in seconds; null for an expression, which STATIC mode does not evaluate (virtual machine 1).
    /// </summary>
    public decimal? Seconds { get; init; }

    /// <inheritdoc/>
    public override string Kind => "DWELL";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return "seconds " + EventText.Number(Seconds);
    }
}
