using Ncx.Core.Expander;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// A rewriter that surrounds every block with a word of this key by these NCX texts (architecture 9, Surround).
/// </summary>
internal sealed class SurroundingRewriter(string key, IReadOnlyList<string> before, IReadOnlyList<string> after)
    : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        return block.Has(key) ? RewriteResult.Surround(before, after, "around " + key) : RewriteResult.Unchanged;
    }
}
