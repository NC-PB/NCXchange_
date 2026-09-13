namespace Ncx.Core.Expander;

/// <summary>
/// What a program rewriter knows about the block it is asked about: the machine name, the channel, the line, and the
/// plugin's own settings, its [plugins.NAME] table of ncx.toml, but no virtual machine (architecture 9; D61, D80,
/// D106). The abstraction lives here with IProgramRewriter; the expander passes a context of its own, and the plugin
/// loader of Ncx.Plugins the concrete one that carries the settings (D106).
/// </summary>
public abstract class RewriteContext
{
    /// <summary>
    /// For the contexts of the expander and of the plugin loader.
    /// </summary>
    protected RewriteContext()
    {
    }

    /// <summary>
    /// The name of the machine, [machine] name of the machine file: "DMU 50".
    /// </summary>
    public abstract string MachineName { get; }

    /// <summary>
    /// The channel of the program the block stands in, 1 or its CHANNEL (language 4.14).
    /// </summary>
    public abstract int Channel { get; }

    /// <summary>
    /// The line of the block in its file (D98).
    /// </summary>
    public abstract int Line { get; }

    /// <summary>
    /// The plugin's own settings, the [plugins.NAME] table of ncx.toml as strings (D80); empty without them.
    /// </summary>
    public abstract IReadOnlyDictionary<string, string> Settings { get; }
}
