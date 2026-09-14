using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Plugins;

/// <summary>
/// Stands in a reader for a reader rule of a plugin (architecture 7, 9; D40, D66): it offers the rule the source block,
/// and hands the rule's blocks to the program only when the rule claimed the block without failing; a plugin that
/// throws is reported and left out, and the reader reads the block as it would without the plugin (implementation 17,
/// P7-01).
/// </summary>
internal sealed class PluginSourceRule : ISourceRule
{
    private readonly Plugin _plugin;
    private readonly PluginDiagnostics _diagnostics;

    public PluginSourceRule(Plugin plugin, ISourceRule rule, PluginDiagnostics diagnostics)
    {
        _plugin = plugin;
        _diagnostics = diagnostics;
        Rule = rule;
    }

    /// <summary>
    /// The reader rule of the plugin.
    /// </summary>
    public ISourceRule Rule { get; }

    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        // A plugin that failed is asked nothing more in the run (implementation 17, P7-01).
        if (_plugin.IsDropped)
        {
            return false;
        }

        // The rule writes into a builder of its own. Only a claim made without failing reaches the program, so that a
        // rule that fails halfway, a block begun and not ended among it, leaves nothing behind (D5: the reader then
        // reads the block, and nothing is lost).
        var ownBuilder = new NcxBuilder(new Diagnostics(_diagnostics.File));
        NcxProgram claimed;
        try
        {
            if (!Rule.Read(block, state, ownBuilder))
            {
                return false;
            }

            claimed = ownBuilder.Build();
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            _diagnostics.Failed(_plugin.Name, $"{nameof(Read)} threw {PluginFaults.Describe(exception)}", block.Line,
                null);
            _plugin.Drop();
            return false;
        }

        ClaimedBlocks.CopyInto(claimed, builder);
        return true;
    }
}
