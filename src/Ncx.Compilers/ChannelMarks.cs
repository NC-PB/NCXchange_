using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers;

/// <summary>
/// The listener of one channel on the STATIC run of a job (virtual machine 3.7, 7): the marks the channel passes in
/// execution order, the mark every block of its expanded program stands behind, generated blocks among them, and the
/// subprograms its walk enters, until the PROGRAM=END of its program. The job compiler finds the same mark in the
/// program of another channel with it (D56).
/// </summary>
internal sealed class ChannelMarks : IVmListener
{
    private readonly NcxProgram _file;
    private readonly Dictionary<Block, int> _indexOf = new(ReferenceEqualityComparer.Instance);
    private bool _ended;

    /// <summary>
    /// Listens for the channel whose program stands in this file.
    /// </summary>
    /// <param name="file">The expanded file of the channel, whose blocks the run executes.</param>
    public ChannelMarks(NcxProgram file)
    {
        _file = file;
        for (int index = 0; index < file.Blocks.Count; index++)
        {
            _indexOf[file.Blocks[index]] = index;
        }
    }

    /// <summary>
    /// The marks the channel passed, in execution order (virtual machine 3.7).
    /// </summary>
    public List<MarkRelease> Releases { get; } = [];

    /// <summary>
    /// For every block of the program, by its index in the expanded file: how many marks the channel had passed when it
    /// executed the block, 0 for a block before the first mark.
    /// </summary>
    public Dictionary<int, int> MarksBefore { get; } = [];

    /// <summary>
    /// The subprograms of the file that the walk entered by CALL, in the order of the file (D99).
    /// </summary>
    public List<Section> Subs { get; } = [];

    public void On(VmEvent vmEvent)
    {
        // A STATIC job walks the rest of each file after the programs of its channels (virtual machine 1, 3.9); that
        // walk is no part of the channel.
        if (_ended)
        {
            return;
        }

        switch (vmEvent)
        {
            // SYNC_RELEASE: the channel passed a mark, together with the other channels that took part (3.7).
            case SyncEvent { Released: true } sync:
                Releases.Add(new MarkRelease
                {
                    Mark = sync.Mark,
                    Round = sync.Round,
                    Channels = sync.Channels,
                    Block = IndexOf(sync.Block),
                    InSubprogram = sync.After.Program.Section?.Kind == SectionKind.Sub,
                });
                break;

            // BLOCK_WRITE is the last event of every executed block (virtual machine 7).
            case BlockWriteEvent write:
                Executed(write);
                break;
        }
    }

    // A block of the program stands behind the marks passed before it; a block of a subprogram names the subprogram
    // the walk entered.
    private void Executed(BlockWriteEvent write)
    {
        Section? section = write.After.Program.Section;
        if (section is null)
        {
            return;
        }

        if (section.Kind == SectionKind.Sub)
        {
            EnterSub(section);
        }
        else if (IndexOf(write.Block) is int index)
        {
            MarksBefore[index] = Releases.Count;
        }

        _ended = section.Kind == SectionKind.Program && write.Block.Has("PROGRAM", null, "END");
    }

    // The subprogram of the file with the name of the walked one; the subprograms stay in the order of the file.
    private void EnterSub(Section walked)
    {
        foreach (Section sub in _file.Subs)
        {
            if (sub.Name != walked.Name || Subs.Contains(sub))
            {
                continue;
            }

            int position = 0;
            while (position < Subs.Count && Subs[position].FirstBlock < sub.FirstBlock)
            {
                position++;
            }

            Subs.Insert(position, sub);
        }
    }

    // The index of an executed block in the expanded file, whose blocks the run executes, the generated ones among
    // them (language 4.15); null for a block that stands in no block of the file.
    private int? IndexOf(Block executed)
    {
        return _indexOf.TryGetValue(executed, out int index) ? index : null;
    }
}
