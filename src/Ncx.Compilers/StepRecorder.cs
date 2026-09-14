using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers;

/// <summary>
/// The compiler's listener on the STATIC run (architecture 8): every block with the events it raised, closed by the
/// BLOCK_WRITE the virtual machine raises last for it, and FILE=BEGIN and FILE=END with their events, in walk order.
/// </summary>
internal sealed class StepRecorder : IVmListener
{
    // The events of the block that runs, until its BLOCK_WRITE closes it.
    private readonly List<VmEvent> _events = [];

    /// <summary>
    /// The steps of the run in walk order.
    /// </summary>
    public List<BlockStep> Steps { get; } = [];

    public void On(VmEvent vmEvent)
    {
        switch (vmEvent)
        {
            // BLOCK_WRITE is the last event of every block the run executes (virtual machine 7, VmOptions).
            case BlockWriteEvent blockWrite:
                Steps.Add(new BlockStep
                {
                    Index = Steps.Count,
                    Block = blockWrite.Block,
                    Before = blockWrite.Before,
                    After = blockWrite.After,
                    Events = new List<VmEvent>(_events),
                    Section = blockWrite.After.Program.Section,
                    BlockWrite = blockWrite,
                });
                _events.Clear();
                break;

            // FILE=BEGIN and FILE=END stand in no program, and no walk executes them (virtual machine 7).
            case FileEvent file:
                Steps.Add(new BlockStep
                {
                    Index = Steps.Count,
                    Block = file.Block,
                    Before = file.Before,
                    After = file.After,
                    Events = [file],
                });
                break;

            default:
                _events.Add(vmEvent);
                break;
        }
    }
}
