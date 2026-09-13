using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// PRELOAD (virtual machine 3.5, 7): raised at every PRELOAD with the tool and the holder; tool 0 is the PRELOAD=0 that
/// clears the preload.
/// </summary>
public sealed record PreloadEvent : VmEvent
{
    /// <summary>
    /// The tool of PRELOAD, a number or a name (language 4.4); 0 clears.
    /// </summary>
    public required ToolRef Tool { get; init; }

    /// <summary>
    /// The resource id of the holder (virtual machine 2.3, 3.8 rule 2).
    /// </summary>
    public required string Holder { get; init; }

    /// <inheritdoc/>
    public override string Kind => "PRELOAD";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return $"tool {Tool}, holder {Holder}";
    }
}
