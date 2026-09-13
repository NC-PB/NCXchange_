using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Cli.History;

/// <summary>
/// The listener of ncx trace: every state variable every executed block changed, in the order the virtual machine
/// raised them, so that a block of a subprogram that STATIC mode walks at several calls appears once per walk (virtual
/// machine 6, 7; D99).
/// </summary>
internal sealed class TraceListener : IVmListener
{
    private readonly List<TraceRow> _rows = [];

    /// <summary>
    /// The rows of the run so far.
    /// </summary>
    public IReadOnlyList<TraceRow> Rows => _rows;

    /// <inheritdoc/>
    public void On(VmEvent vmEvent)
    {
        if (TraceRow.Of(vmEvent) is TraceRow row)
        {
            _rows.Add(row);
        }
    }
}
