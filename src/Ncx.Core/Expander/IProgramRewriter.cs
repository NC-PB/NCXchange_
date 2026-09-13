using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// A program rewriter: the rule of a plugin that the configuration cannot express. The expander asks it about every
/// block of the program once, before the virtual machine executes the block, and it answers with NCX text: leave the
/// block, rewrite its words, or insert generated blocks before and after it. It never changes the state of the virtual
/// machine (architecture 5.5, 9; code-guidelines 11; D61, D106).
/// </summary>
public interface IProgramRewriter
{
    /// <summary>
    /// Decides what the machine needs at one block.
    /// </summary>
    /// <param name="block">The block as the expansion rules and the rewriters before this one left it; Block.Has and
    /// Block.Find are the lookups a rule needs (D106).</param>
    /// <param name="context">The machine name, the channel and the line of the block and the plugin's own settings
    /// (D80); no virtual machine (D61).</param>
    /// <returns>RewriteResult.Unchanged, Replace or Surround.</returns>
    RewriteResult Rewrite(Block block, RewriteContext context);
}
