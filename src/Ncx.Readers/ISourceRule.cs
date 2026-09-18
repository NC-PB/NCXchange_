using Ncx.Core.Writing;

namespace Ncx.Readers;

/// <summary>
/// A reader rule of a plugin: decides what a machine-specific M code or a sequence of source blocks means on this
/// machine and writes the NCX words for it, the workpiece transfer behind a builder M code that runs a hidden
/// subprogram (architecture 9; virtual machine 7; D40, D66). It lives with its caller, the readers, together with the
/// types its method takes (D106). The configuration tables of the machine are tried first; a rule is asked for what
/// they leave undecided (machine-config 5).
/// </summary>
public interface ISourceRule
{
    // TODO(question): architecture 9, virtual machine 7 and phase 3 (P3-01, "whether the rule claimed the block or
    // sequence") let a rule claim a sequence of source blocks (D66), but no document says how a rule that is called
    // once per source block, with one method, does it. The reader offers a rule only the blocks the tables leave
    // undecided and calls nothing after the last block, so a rule that held blocks back until its sequence is complete
    // would find the blocks the tables decide written before the ones it held, and would lose a block held at the end
    // of the file (D5: nothing is dropped). Until D232 is answered, a claim covers the one block offered.

    /// <summary>
    /// Offers one source block to the rule. A rule that claims it writes its NCX blocks for it with the builder,
    /// Begin(block.Line)...End(), before it returns true; the reader then reads nothing more of the block and keeps its
    /// comment as a comment-only line after the rule's blocks (D5). The reader offers every block the tables leave
    /// undecided once, in the order it reads the blocks, and calls nothing after the last one, so a claim is complete
    /// when Read returns.
    /// </summary>
    /// <param name="block">The source block as the tokenizer read it.</param>
    /// <param name="state">The source-side state after the modal codes of the block, read only.</param>
    /// <param name="builder">The builder of the program being read.</param>
    /// <returns>True when the rule claimed the block.</returns>
    bool Read(SourceBlock block, SourceState state, NcxBuilder builder);
}
