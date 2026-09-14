namespace Ncx.Core.Expander;

/// <summary>
/// A program rewriter with a name of its own, which the blocks it generates carry as their source and every diagnostic
/// about its text names, instead of the name of its type. The plugin loader of Ncx.Plugins names each rewriter of a
/// plugin after the plugin, so that trace shows the plugin's name (architecture 9, virtual machine 3.10).
/// </summary>
public interface INamedRewriter : IProgramRewriter
{
    /// <summary>
    /// The name the blocks of its answers carry: "MyShopRules".
    /// </summary>
    string Name { get; }
}
