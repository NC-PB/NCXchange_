namespace Ncx.Core.Expander;

/// <summary>
/// The context the expander gives a rewriter for one block: the name of the machine, the channel of the program the
/// block stands in, the line of the block, and no settings; the plugin loader of Ncx.Plugins adds those (D80, D106).
/// </summary>
internal sealed class BlockRewriteContext : RewriteContext
{
    private static readonly IReadOnlyDictionary<string, string> s_noSettings = new Dictionary<string, string>();

    public BlockRewriteContext(string machineName, int channel, int line)
    {
        MachineName = machineName;
        Channel = channel;
        Line = line;
    }

    public override string MachineName { get; }

    public override int Channel { get; }

    public override int Line { get; }

    public override IReadOnlyDictionary<string, string> Settings => s_noSettings;
}
