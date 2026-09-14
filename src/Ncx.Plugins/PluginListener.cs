using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Plugins;

/// <summary>
/// Stands in the virtual machine for a listener of a plugin (virtual machine 7, architecture 9): it hands the listener
/// every event, whose Before and After are read-only snapshots (D61, D106), and reports and leaves out a plugin that
/// throws, so that the run goes on without it (implementation 17, P7-01).
/// </summary>
internal sealed class PluginListener : IVmListener
{
    private readonly Plugin _plugin;
    private readonly PluginDiagnostics _diagnostics;

    public PluginListener(Plugin plugin, IVmListener listener, PluginDiagnostics diagnostics)
    {
        _plugin = plugin;
        _diagnostics = diagnostics;
        Listener = listener;
    }

    /// <summary>
    /// The listener of the plugin.
    /// </summary>
    public IVmListener Listener { get; }

    public void On(VmEvent vmEvent)
    {
        // A plugin that failed is asked nothing more in the run (implementation 17, P7-01).
        if (_plugin.IsDropped)
        {
            return;
        }

        // A listener that throws is reported on the block of the event, a generated block with its origin (D98).
        try
        {
            Listener.On(vmEvent);
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            _diagnostics.Failed(_plugin.Name, $"{nameof(On)} threw {PluginFaults.Describe(exception)}",
                vmEvent.Block.Line, vmEvent.Block.OriginLine);
            _plugin.Drop();
        }
    }
}
