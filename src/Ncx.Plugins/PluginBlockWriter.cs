using Ncx.Compilers;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Plugins;

/// <summary>
/// Stands in a compiler for a block writer of a plugin (virtual machine 7, BLOCK_WRITE; architecture 8, 9): it hands
/// the writer the lines of every block to edit, and when the writer fails halfway it puts the lines back as the
/// writer found them, reports the plugin and leaves it out, so that the compile goes on without it (implementation 17,
/// P7-01).
/// </summary>
internal sealed class PluginBlockWriter : IBlockWriter
{
    private readonly Plugin _plugin;
    private readonly PluginDiagnostics _diagnostics;

    public PluginBlockWriter(Plugin plugin, IBlockWriter writer, PluginDiagnostics diagnostics)
    {
        _plugin = plugin;
        _diagnostics = diagnostics;
        Writer = writer;
    }

    /// <summary>
    /// The block writer of the plugin.
    /// </summary>
    public IBlockWriter Writer { get; }

    public void Write(BlockWriteEvent blockWrite)
    {
        // A plugin that failed is asked nothing more in the run (implementation 17, P7-01).
        if (_plugin.IsDropped)
        {
            return;
        }

        // The lines as the compiler and the writers before this one left them, which stay when this one fails.
        var linesBefore = new List<string>(blockWrite.OutputLines);
        string? failure = null;
        try
        {
            Writer.Write(blockWrite);
            if (HasLineWithoutText(blockWrite.OutputLines))
            {
                failure = $"{nameof(Write)} left a line without text";
            }
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            failure = $"{nameof(Write)} threw {PluginFaults.Describe(exception)}";
        }

        if (failure is not null)
        {
            blockWrite.OutputLines.Clear();
            blockWrite.OutputLines.AddRange(linesBefore);
            _diagnostics.Failed(_plugin.Name, failure, blockWrite.Block.Line, blockWrite.Block.OriginLine);
            _plugin.Drop();
        }
    }

    // An output line is text; a writer that put null in the list has failed as well.
    private static bool HasLineWithoutText(List<string> lines)
    {
        foreach (string? line in lines)
        {
            if (line is null)
            {
                return true;
            }
        }

        return false;
    }
}
