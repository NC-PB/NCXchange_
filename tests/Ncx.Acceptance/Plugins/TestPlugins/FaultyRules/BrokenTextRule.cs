namespace FaultyRules;

/// <summary>
/// A program rewriter that inserts a text the parser rejects, FOO=1, before every block with a DWELL word.
/// </summary>
public sealed class BrokenTextRule : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        if (!block.Has("DWELL"))
        {
            return RewriteResult.Unchanged;
        }

        return RewriteResult.Surround(before: ["FOO=1"], after: [], reason: "a word the language does not know");
    }
}
