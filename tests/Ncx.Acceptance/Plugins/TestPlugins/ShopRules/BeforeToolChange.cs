namespace ShopRules;

/// <summary>
/// Inserts the block that the setting before_tool of [plugins.ShopRules] in ncx.toml names before every tool change,
/// a retract or a coolant stop the shop wants there (architecture 9: add HOME Z before TOOL; D80). Without the setting
/// it changes nothing.
/// </summary>
public sealed class BeforeToolChange : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        // Only a tool change needs the block, and only a shop that names one in its settings.
        if (!block.Has("TOOL") || !context.Settings.TryGetValue("before_tool", out string? before))
        {
            return RewriteResult.Unchanged;
        }

        return RewriteResult.Surround(before: [before], after: [], reason: "before_tool of the shop");
    }
}
