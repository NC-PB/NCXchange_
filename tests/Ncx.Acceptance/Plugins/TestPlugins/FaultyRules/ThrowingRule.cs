namespace FaultyRules;

/// <summary>
/// A program rewriter that throws on every block with a COOLANT word, as a plugin does whose own data is missing.
/// </summary>
public sealed class ThrowingRule : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        if (block.Has("COOLANT"))
        {
            throw new InvalidOperationException("the shop table is missing");
        }

        return RewriteResult.Unchanged;
    }
}
