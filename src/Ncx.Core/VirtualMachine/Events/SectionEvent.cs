namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// SECTION (virtual machine 7): raised at SECTION with its text, the structuring comment that the runtime estimate
/// totals by (language 4.1, virtual machine 8).
/// </summary>
public sealed record SectionEvent : VmEvent
{
    /// <summary>
    /// The text of SECTION, without the quotes.
    /// </summary>
    public required string Text { get; init; }

    /// <inheritdoc/>
    public override string Kind => "SECTION";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return EventText.Quoted(Text);
    }
}
