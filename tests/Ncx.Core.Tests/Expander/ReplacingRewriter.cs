using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// A rewriter that replaces the block with this canonical text by another NCX text (architecture 9, Replace).
/// </summary>
internal sealed class ReplacingRewriter(string blockText, string replacement, string reason) : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        return NcxWriter.WriteBlock(block) == blockText
            ? RewriteResult.Replace(replacement, reason)
            : RewriteResult.Unchanged;
    }
}
