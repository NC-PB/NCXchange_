using Ncx.Core.Expander;

namespace Ncx.Plugins;

/// <summary>
/// The context a program rewriter of a plugin receives for one block (architecture 9; D61, D80, D106): the name of the
/// machine, the channel of the program the block stands in, the line of the block, and the plugin's own settings, its
/// [plugins.&lt;name&gt;] section of ncx.toml as strings. Nothing else of ncx.toml is visible to it, and no virtual
/// machine. The plugin loader makes one for every block; the test of a plugin makes the one it needs.
/// </summary>
public sealed class PluginRewriteContext : RewriteContext
{
    /// <summary>
    /// A context for one block.
    /// </summary>
    /// <param name="machineName">The name of the machine, [machine] name of the machine file: "DMU 50".</param>
    /// <param name="channel">The channel of the program the block stands in, 1 or its CHANNEL (language 4.14).</param>
    /// <param name="line">The line of the block in its file (D98).</param>
    /// <param name="settings">The plugin's own settings, its [plugins.&lt;name&gt;] section (D80); empty without
    /// one.</param>
    public PluginRewriteContext(string machineName, int channel, int line,
        IReadOnlyDictionary<string, string> settings)
    {
        MachineName = machineName;
        Channel = channel;
        Line = line;
        Settings = settings;
    }

    /// <inheritdoc/>
    public override string MachineName { get; }

    /// <inheritdoc/>
    public override int Channel { get; }

    /// <inheritdoc/>
    public override int Line { get; }

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, string> Settings { get; }
}
