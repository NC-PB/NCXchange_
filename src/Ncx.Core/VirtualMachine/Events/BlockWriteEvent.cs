using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// BLOCK_WRITE (virtual machine 7): raised by a compiler before a block is written, with the block's words and the
/// output lines, both open to change, so that a plugin can split Z onto its own line or strip umlauts from a comment.
/// The one event with parts to edit (architecture 5.3).
/// </summary>
/// <remarks>
/// Declared with the other events; the compilers of phase 3 (P3-03) raise it through IBlockWriter.
/// </remarks>
public sealed record BlockWriteEvent : VmEvent
{
    /// <summary>
    /// The words of the block the compiler writes.
    /// </summary>
    public required List<Word> Words { get; init; }

    /// <summary>
    /// The lines the compiler writes for the block, in the syntax of the target controller.
    /// </summary>
    public required List<string> OutputLines { get; init; }

    /// <inheritdoc/>
    public override string Kind => "BLOCK_WRITE";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return EventText.Words(Words) + " -> " + string.Join(" | ", OutputLines);
    }
}
