using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers;

// The wait marks of a job (machine-config 5, [sync]; language 4.8; controller-mapping 7): every SYNC within the range
// of its channels, start_mark as the first block of every channel program, and the mark of a SYNC the job compiler
// generates.
public sealed partial class JobCompiler
{
    // Every SYNC of a channel program lies within the range of its channels: the range of the first group of [sync]
    // groups whose channels hold every channel of the SYNC, mark_range for a SYNC that no group holds (machine-config
    // 5; controller-mapping 7: the WTW waits with M800..M849 between its R and L units).
    // TODO(question): machine-config 5 gives groups "a second wait group between other units" without saying which
    // marks a SYNC may use whose channels no group holds, or whose channels two groups hold; the first group that holds
    // them all gives the range, mark_range the range of every other SYNC, until that is answered.
    private static void CheckMarks(List<JobChannel> channels, MachineConfig machine)
    {
        if (machine.Sync is not SyncConfig sync)
        {
            return;
        }

        List<int> all = ChannelsOf(channels);
        foreach (JobChannel channel in channels)
        {
            foreach (Section section in SectionsOf(channel))
            {
                for (int index = section.FirstBlock; index <= section.LastBlock; index++)
                {
                    CheckMark(channel, channel.File.Blocks[index], sync, all);
                }
            }
        }
    }

    private static void CheckMark(JobChannel channel, Block block, SyncConfig sync, List<int> all)
    {
        if (block.Find("SYNC")?.Value is not IntegerValue mark)
        {
            return;
        }

        List<int> participants = ParticipantsOf(block, channel.Channel, all);
        if (RangeOf(sync, participants) is MarkRange range && (mark.Number < range.First || mark.Number > range.Last))
        {
            channel.Diagnostics.Error(block, DiagnosticCodes.MarkOutsideItsRange, string.Create(
                CultureInfo.InvariantCulture,
                $"SYNC={mark.Text} of the channels {string.Join(",", participants)} lies outside their marks "
                + $"{range.First} to {range.Last} (machine-config 5, [sync] mark_range and groups)."));
        }
    }

    // start_mark: the mark written as the first block of every channel program, directly after its header
    // (machine-config 5; language 4.8: a SYNC as the first block after the header is the program start
    // synchronization, Nakamura M199).
    // TODO(question): machine-config 5 writes start_mark "as the first block of every channel program", while the WY
    // pair of the Nakamura synchronizes at M199 after the blocks that set the machine up; the start mark is written
    // only when not every channel program passes it as its first mark with every channel of the job, so that no program
    // waits at it twice, until that is answered.
    private static void InsertStartMark(List<JobChannel> channels, MachineConfig machine)
    {
        if (machine.Sync?.StartMark is not int startMark || channels.Count < 2 || StartsAtMark(channels, startMark))
        {
            return;
        }

        foreach (JobChannel channel in channels)
        {
            int header = channel.Program.FirstBlock;
            Block origin = channel.File.Blocks[header];
            channel.InsertAfter(header, SyncBlock(startMark, origin, origin.Line, "[sync] start_mark",
                GeneratedPlacement.After));
        }
    }

    // Whether every channel passes the mark first, all channels of the job together.
    private static bool StartsAtMark(List<JobChannel> channels, int mark)
    {
        List<int> all = ChannelsOf(channels);
        foreach (JobChannel channel in channels)
        {
            if (channel.Marks.Releases.Count == 0
                || channel.Marks.Releases[0].Mark != mark
                || !channel.Marks.Releases[0].Channels.SequenceEqual(all))
            {
                return false;
            }
        }

        return true;
    }

    // The mark of the SYNC the job compiler generates behind which a word bound to every channel stands (D56): the
    // first mark of the range of all channels of the job that no channel program of the job uses, the first of the
    // range when every mark is used, since marks are matched in execution order and may be reused (virtual machine
    // 3.7). Null when [sync] gives no wait template or no range.
    // TODO(question): D56 has the job compiler insert "the SYNC it needs" without saying which mark it takes; it takes
    // the first mark of the range that the job does not use, until that is answered.
    private static int? GeneratedMark(List<JobChannel> channels, MachineConfig machine)
    {
        if (machine.Sync is not SyncConfig { Wait: not null } sync
            || RangeOf(sync, ChannelsOf(channels)) is not MarkRange range)
        {
            return null;
        }

        var used = new HashSet<long>();
        foreach (JobChannel channel in channels)
        {
            foreach (Section section in SectionsOf(channel))
            {
                for (int index = section.FirstBlock; index <= section.LastBlock; index++)
                {
                    if (channel.File.Blocks[index].Find("SYNC")?.Value is IntegerValue mark)
                    {
                        used.Add(mark.Number);
                    }
                }
            }
        }

        for (int mark = range.First; mark <= range.Last; mark++)
        {
            if (!used.Contains(mark) && mark != sync.StartMark)
            {
                return mark;
            }
        }

        return range.First;
    }

    // The range of the marks of a SYNC between these channels (machine-config 5): the first group that holds them all,
    // else mark_range; null when [sync] gives neither.
    private static MarkRange? RangeOf(SyncConfig sync, List<int> participants)
    {
        foreach (SyncGroup group in sync.Groups)
        {
            if (group.Range is MarkRange range && HoldsAll(group.Channels, participants))
            {
                return range;
            }
        }

        return sync.MarkRange;
    }

    private static bool HoldsAll(IReadOnlyList<int> group, List<int> channels)
    {
        foreach (int channel in channels)
        {
            if (!group.Contains(channel))
            {
                return false;
            }
        }

        return true;
    }

    // The channels of a SYNC: those of WITH and its own channel, every channel of the job without WITH (language 4.8).
    private static List<int> ParticipantsOf(Block block, int channel, List<int> all)
    {
        var participants = new List<int>();
        Value? with = block.Find("WITH")?.Value;
        if (with is null)
        {
            participants.AddRange(all);
        }
        else if (with is IntegerValue single)
        {
            participants.Add((int)single.Number);
        }
        else if (with is ListValue list)
        {
            foreach (string item in list.Items)
            {
                if (int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                {
                    participants.Add(number);
                }
            }
        }

        if (!participants.Contains(channel))
        {
            participants.Add(channel);
        }

        participants.Sort();
        return participants;
    }

    // The program of a channel and the subprograms it calls.
    private static List<Section> SectionsOf(JobChannel channel)
    {
        var sections = new List<Section> { channel.Program };
        sections.AddRange(channel.Marks.Subs);
        return sections;
    }

    // A SYNC the job compiler generates (language 4.15): it names the block it was generated for and carries the SKIP
    // of that block (SkipOf), so that it is skipped on the same switch as the words it stands before, and no program
    // skips the wait and runs the code (virtual machine 3.8 rule 2a).
    private static Block SyncBlock(int mark, Block origin, int line, string reason, GeneratedPlacement placement)
    {
        var text = new List<string>();
        foreach (Word skip in SkipOf(origin))
        {
            text.Add(skip.ToCanonical());
        }

        text.Add("SYNC=" + mark.ToString(CultureInfo.InvariantCulture));
        Block block = Parser.ParseBlock(string.Join(" ", text), line, new ParserOptions(), new Diagnostics(""))
            ?? throw new InvalidOperationException("SKIP=n SYNC=m is one block of NCX.");
        return Generated(block, origin, reason, placement);
    }

    // The SKIP word of the block a generated block stands for, bare or with the number of its switch, as its origin
    // writes it; empty for a block without SKIP.
    // TODO(question): D201, no document says whether a generated block is skipped with the SKIP block it was generated
    // for; a block the job compiler generates carries the SKIP word of its origin, SKIP=2 as SKIP=2, as the
    // recommendation of D201 and the expander (GeneratedText.Mark) do, until that is answered.
    private static List<Word> SkipOf(Block origin)
    {
        return origin.Find("SKIP") is Word skip ? [skip] : [];
    }

    // A block the job compiler generates for a block of a channel program (language 4.15, D98): a diagnostic on it
    // names the line it stands on and the line of that block.
    private static Block Generated(Block block, Block origin, string reason, GeneratedPlacement placement)
    {
        return block with
        {
            IsGenerated = true,
            OriginLine = origin.Line,
            Generated = new GeneratedBlock
            {
                Origin = origin,
                Source = Writer,
                Reason = reason,
                Placement = placement,
            },
        };
    }
}
