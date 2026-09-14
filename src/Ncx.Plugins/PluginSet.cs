using Ncx.Compilers;
using Ncx.Core.Expander;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Readers;

namespace Ncx.Plugins;

/// <summary>
/// The plugins of a run by the place they act in (virtual machine 7, architecture 9; implementation 17, P7-01): the
/// program rewriters for the expander, the reader rules for the readers, the listeners for the virtual machine and the
/// block writers for the compilers, each in load order. Every entry stands in front of a class of a plugin: it hands
/// the class what its place gives it, reports with the plugin's name what the class throws, and asks nothing more of
/// a plugin that failed, so that the run goes on without it (D61, D106).
/// </summary>
public sealed class PluginSet
{
    private readonly List<IProgramRewriter> _rewriters = [];
    private readonly List<ISourceRule> _sourceRules = [];
    private readonly List<IVmListener> _listeners = [];
    private readonly List<IBlockWriter> _blockWriters = [];

    /// <summary>
    /// A set without plugins, which the loader fills.
    /// </summary>
    internal PluginSet()
    {
    }

    /// <summary>
    /// The program rewriters of the plugins in load order, for the expander (architecture 5.5).
    /// </summary>
    public IReadOnlyList<IProgramRewriter> Rewriters => _rewriters;

    /// <summary>
    /// The reader rules of the plugins in load order, for the options of a reader (architecture 7; D40, D66).
    /// </summary>
    public IReadOnlyList<ISourceRule> SourceRules => _sourceRules;

    /// <summary>
    /// The listeners of the plugins in load order, to subscribe to the virtual machine (virtual machine 7).
    /// </summary>
    public IReadOnlyList<IVmListener> Listeners => _listeners;

    /// <summary>
    /// The block writers of the plugins in load order, for the options of a compiler (virtual machine 7, BLOCK_WRITE).
    /// </summary>
    public IReadOnlyList<IBlockWriter> BlockWriters => _blockWriters;

    // A class of a plugin acts in every place whose interface it implements, as one object in all of them.
    internal void Add(Plugin plugin, object instance, PluginDiagnostics diagnostics)
    {
        if (instance is IProgramRewriter rewriter)
        {
            _rewriters.Add(new PluginRewriter(plugin, rewriter, diagnostics));
        }

        if (instance is ISourceRule rule)
        {
            _sourceRules.Add(new PluginSourceRule(plugin, rule, diagnostics));
        }

        if (instance is IVmListener listener)
        {
            _listeners.Add(new PluginListener(plugin, listener, diagnostics));
        }

        if (instance is IBlockWriter writer)
        {
            _blockWriters.Add(new PluginBlockWriter(plugin, writer, diagnostics));
        }
    }
}
