using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Cli.History;

/// <summary>
/// The listener of ncx annotate: the state variables each block changed at its first walk. A block of a subprogram that
/// STATIC mode walks at several calls gets the values of its first walk (virtual machine 6, D99).
/// </summary>
internal sealed class AnnotationListener : IVmListener
{
    // The values of the first walk of each block that changed something, by the pc of the block.
    private readonly Dictionary<int, List<TraceRow>> _valuesByBlock = [];

    // One entry per walk entered and not yet left, the innermost last: true while it is the first walk of its program
    // or subprogram.
    private readonly List<bool> _walks = [];

    // The subprograms a walk has entered, by name.
    private readonly HashSet<string> _walkedSubs = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void On(VmEvent vmEvent)
    {
        switch (vmEvent)
        {
            // STATIC mode walks every program of the file once (virtual machine 1).
            case ProgramEvent { Phase: EventPhase.Begin }:
                _walks.Add(true);
                break;

            // A subprogram is walked at every CALL, and once when nothing calls it (D99); its first walk is the first
            // SUB_BEGIN of its name. SUB_BEGIN comes before the values of its block and SUB_END after them, and so do
            // PROGRAM_BEGIN and PROGRAM_END (virtual machine 7).
            case SubEvent { Phase: EventPhase.Begin } sub:
                _walks.Add(_walkedSubs.Add(sub.Name));
                break;

            case ProgramEvent { Phase: EventPhase.End } or SubEvent { Phase: EventPhase.End }:
                if (_walks.Count > 0)
                {
                    _walks.RemoveAt(_walks.Count - 1);
                }

                break;

            default:
                RecordFirstWalk(vmEvent);
                break;
        }
    }

    /// <summary>
    /// The values of the first walk of a block, in the order trace writes them; none for a block that never ran or
    /// changed nothing.
    /// </summary>
    /// <param name="blockIndex">The index of the block in the program the virtual machine ran.</param>
    public IReadOnlyList<TraceRow> ValuesOf(int blockIndex)
    {
        return _valuesByBlock.TryGetValue(blockIndex, out List<TraceRow>? values) ? values : [];
    }

    // Annotate writes the values of the first walk only (virtual machine 6).
    private void RecordFirstWalk(VmEvent vmEvent)
    {
        bool firstWalk = _walks.Count == 0 || _walks[_walks.Count - 1];
        if (!firstWalk || TraceRow.Of(vmEvent) is not TraceRow row)
        {
            return;
        }

        if (!_valuesByBlock.TryGetValue(row.BlockIndex, out List<TraceRow>? values))
        {
            values = [];
            _valuesByBlock[row.BlockIndex] = values;
        }

        values.Add(row);
    }
}
