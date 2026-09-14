using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Analytics;

/// <summary>
/// Whether an event belongs to the block range of an analytic: its block stands on an NCX line of the file inside the
/// range (virtual machine 8, D67).
/// </summary>
internal sealed class RangeFilter
{
    private readonly BlockRange _range;

    // The programs and subprograms of the file, from FILE_BEGIN (virtual machine 7); a section of an external program
    // that a CALL loads is none of them.
    private readonly HashSet<Section> _fileSections = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// A filter for one run.
    /// </summary>
    /// <param name="range">--from and --to.</param>
    public RangeFilter(BlockRange range)
    {
        _range = range;
    }

    /// <summary>
    /// Reads the sections of the file from FILE_BEGIN; every other event passes unread.
    /// </summary>
    public void On(VmEvent vmEvent)
    {
        if (vmEvent is not FileEvent { Phase: EventPhase.Begin } file)
        {
            return;
        }

        foreach (Section program in file.Programs)
        {
            _fileSections.Add(program);
        }

        foreach (Section sub in file.Subs)
        {
            _fileSections.Add(sub);
        }
    }

    /// <summary>
    /// True when the block of the event lies in the range: every block without a range; with one, a block on a line
    /// from --from to --to, a generated block by the line of its origin.
    /// </summary>
    public bool Contains(VmEvent vmEvent)
    {
        if (_range.IsWhole)
        {
            return true;
        }

        // The range is NCX line numbers of the file (virtual machine 8, D67): a block of an external program, which
        // an INTERPRETED CALL loads from a file of its own (virtual machine 3.6), has the lines of that file and is
        // outside every range of this one.
        Section? section = vmEvent.After.Program.Section;
        bool ofTheFile = section is null || _fileSections.Count == 0 || _fileSections.Contains(section);
        return ofTheFile && _range.Contains(vmEvent.Block.Line);
    }
}
