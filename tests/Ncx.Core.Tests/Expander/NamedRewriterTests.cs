using Ncx.Core.Expander;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// A program rewriter with a name of its own: the blocks it generates carry that name as their source, and every
/// diagnostic about its text names it, instead of the name of its type. The plugin loader names each rewriter of a
/// plugin after the plugin, so that trace shows the plugin's name (architecture 9; implementation 17, P7-01).
/// </summary>
public sealed class NamedRewriterTests
{
    private static readonly string s_coolant = ExpanderHarness.File(
        "UNITS=MM",
        "SPINDLE:MAIN=CW RPM:MAIN=1500",
        "COOLANT:THROUGH=ON");

    // Architecture 9: trace shows the blocks of a plugin with the plugin's name (virtual machine 3.10).
    [Fact]
    public void INamedRewriter_Surround_GeneratedBlocksCarryItsName()
    {
        var named = new NamedRewriter("MyShopRules", new CoolantClutchRule());

        NcxProgram expanded = ExpanderHarness.Expand(s_coolant, ExpanderMachines.Mill(), named);

        Block stop = ExpanderHarness.Find(expanded, "SPINDLE:MAIN=OFF");
        Assert.Equal("MyShopRules", stop.Generated?.Source);
        Assert.Equal("the coolant clutch needs a standing spindle", stop.Generated?.Reason);
    }

    // D98: a diagnostic about the text of a named rewriter names it, as it names any rewriter by its type.
    [Fact]
    public void INamedRewriter_TextThatDoesNotParse_IsReportedWithItsName()
    {
        var named = new NamedRewriter("MyShopRules", new SurroundingRewriter("TOOL", ["FOO=1"], []));

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"),
            ExpanderMachines.Mill(), named);

        Diagnostic error = Assert.Single(expanded.Diagnostics.Items);
        Assert.StartsWith("MyShopRules \"FOO=1\": ", error.Message, StringComparison.Ordinal);
    }

    // A rewriter that answers for another under a name of its own, as the plugin loader puts one in front of every
    // rewriter of a plugin.
    private sealed class NamedRewriter(string name, IProgramRewriter inner) : INamedRewriter
    {
        public string Name => name;

        public RewriteResult Rewrite(Block block, RewriteContext context)
        {
            return inner.Rewrite(block, context);
        }
    }
}
