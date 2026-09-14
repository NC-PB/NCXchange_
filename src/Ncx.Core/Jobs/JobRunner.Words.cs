using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

// The words of language 4.8 as the scheduler applies them after the block that carries them (virtual machine 3.7): SYNC
// with WITH, WAIT_CHANNEL and START_CHANNEL; and the channel the job runs a program on against the CHANNEL of its
// header (language 4.14). The catalog gives SYNC, WAIT_CHANNEL and START_CHANNEL an integer and WITH a list of them.
public sealed partial class JobRunner
{
    // The job words of an executed block, in the order of language 4.8; false after an ERROR, which stops the job.
    private bool ApplyJobWords(ChannelRun channel, Block block)
    {
        if (block.Find("SYNC") is Word sync && !WaitAtMark(channel, block, sync))
        {
            return false;
        }

        if (block.Find("WAIT_CHANNEL") is Word waitChannel && !WaitForChannel(channel, block, waitChannel))
        {
            return false;
        }

        return block.Find("START_CHANNEL") is not Word startChannel || StartChannel(channel, block, startChannel);
    }

    // SYNC=m marks the channel waiting at m with the channels of WITH, all channels of the job without it (virtual
    // machine 3.7, language 4.8), and raises SYNC_WAIT in the round. In a job of one channel nothing waits; the
    // pre-pass of the file warns (3.7, ChannelValidation).
    private bool WaitAtMark(ChannelRun channel, Block block, Word sync)
    {
        if (_channelCount < 2 || sync.Value is not IntegerValue mark)
        {
            return true;
        }

        if (Participants(channel, block) is not List<int> participants)
        {
            return false;
        }

        channel.Participants = participants;
        channel.MarkBlock = block;
        int markNumber = NumberOf(mark);
        _timeline.Add(channel.Vm.ChangeWait(block, markNumber, markNumber, participants, _round));
        return true;
    }

    // WAIT_CHANNEL=c waits until channel c has finished (virtual machine 3.7, language 4.8).
    private bool WaitForChannel(ChannelRun channel, Block block, Word waitChannel)
    {
        if (ChannelNamed(channel, block, waitChannel, waitChannel.Value) is not ChannelRun awaited)
        {
            return false;
        }

        channel.WaitingFor = awaited.Channel;
        channel.ChannelWaitBlock = block;
        return true;
    }

    // START_CHANNEL=c starts the program of channel c, the program its NAME selects or else the program of the
    // channel's [[channel]] (language 4.8). The started channel executes its first block in the next round, and what
    // the starting channel did before the block is before everything the started channel does. A channel that runs or
    // has run already is not started again (the TODO(question) of StartChannels).
    private bool StartChannel(ChannelRun channel, Block block, Word startChannel)
    {
        if (ChannelNamed(channel, block, startChannel, startChannel.Value) is not ChannelRun started)
        {
            return false;
        }

        if (!started.AwaitsStart)
        {
            channel.Vm.Diagnostics.Warning(block, DiagnosticCodes.ChannelAlreadyStarted, string.Create(
                CultureInfo.InvariantCulture,
                $"{startChannel.ToCanonical()}: channel {started.Channel} runs or has run already, and nothing starts "
                + $"(language 4.8, virtual machine 3.7)."));
            return true;
        }

        started.AwaitsStart = false;
        string? programName = block.Find("NAME")?.Value is StringValue name ? name.Content : started.ManifestProgram;
        Begin(started, programName);
        _shared.Synchronized([channel.Channel, started.Channel]);
        return true;
    }

    // WITH=1,2 names the channels that take part in the SYNC of its block, all channels of the job without it (language
    // 4.8). The channel of the SYNC waits at the mark itself, so it always takes part: "the channel waits until every
    // participating channel has reached" the mark (4.8). Null after the ERROR for a channel the job does not run.
    private List<int>? Participants(ChannelRun channel, Block block)
    {
        var participants = new List<int>();
        if (block.Find("WITH") is Word with)
        {
            foreach (Value item in ItemsOf(with.Value))
            {
                if (ChannelNamed(channel, block, with, item) is not ChannelRun named)
                {
                    return null;
                }

                AddOnce(participants, named.Channel);
            }
        }
        else
        {
            foreach (ChannelRun run in _channels)
            {
                AddOnce(participants, run.Channel);
            }
        }

        AddOnce(participants, channel.Channel);
        participants.Sort();
        return participants;
    }

    // The channel that a WITH, WAIT_CHANNEL or START_CHANNEL names: a channel of the job (language 4.8).
    // TODO(question): language 4.8 and virtual machine 3.7 do not say what a WITH, WAIT_CHANNEL or START_CHANNEL does
    // that names a channel the job does not run; it is an ERROR, as a missing call target is (virtual machine 3.6, 5),
    // since the SYNC could never be released and that channel never waited for or started, until that is answered.
    private ChannelRun? ChannelNamed(ChannelRun channel, Block block, Word word, Value value)
    {
        if (value is IntegerValue number && FindChannel(NumberOf(number)) is ChannelRun named)
        {
            return named;
        }

        var ids = new List<string>();
        foreach (ChannelRun run in _channels)
        {
            ids.Add(run.Channel.ToString(CultureInfo.InvariantCulture));
        }

        channel.Vm.Diagnostics.Error(block, DiagnosticCodes.ChannelNotInJob,
            $"{word.ToCanonical()} names a channel the job does not run; the channels of the job are "
            + $"{string.Join(", ", ids)} (language 4.8, machine-config 8).");
        return null;
    }

    // The job runs a program on the channel of its [[channel]] (machine-config 8, D15), also a program whose header
    // names no CHANNEL, which language 4.1 puts on channel 1 (the recommendation of D223).
    // TODO(question): language 4.14 and virtual machine 2.8 take the channel of a program from the CHANNEL of its
    // header, the job manifest from the id of its [[channel]], and no document says which applies when the two differ;
    // the channel of the job applies, with a WARNING on the header, until that is answered.
    private static void CheckHeaderChannel(ChannelRun channel)
    {
        if (channel.Section is not Section running)
        {
            return;
        }

        Block header = channel.Program.Blocks[running.FirstBlock];
        if (header.Find("CHANNEL") is Word word
            && word.Value is IntegerValue number
            && NumberOf(number) != channel.Channel)
        {
            channel.Diagnostics.Warning(header, DiagnosticCodes.ChannelOtherThanHeader, string.Create(
                CultureInfo.InvariantCulture,
                $"{word.ToCanonical()} in the header of a program that the job runs on channel {channel.Channel}; the "
                + $"channel of the job applies (machine-config 8, virtual machine 2.8)."));
        }
    }

    // The channels that a START_CHANNEL in a file of the job names (language 4.8).
    private HashSet<int> ChannelsStartedByWord()
    {
        var started = new HashSet<int>();
        var files = new HashSet<NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (ChannelRun channel in _channels)
        {
            if (!files.Add(channel.Program))
            {
                continue;
            }

            foreach (Block block in channel.Program.Blocks)
            {
                if (block.Find("START_CHANNEL")?.Value is IntegerValue number)
                {
                    started.Add(NumberOf(number));
                }
            }
        }

        return started;
    }

    // The channel numbers of WITH: one integer, or a list of them (language 3, list; 4.8).
    private static List<Value> ItemsOf(Value value)
    {
        var items = new List<Value>();
        if (value is not ListValue list)
        {
            items.Add(value);
            return items;
        }

        foreach (string item in list.Items)
        {
            items.Add(long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out long number)
                ? new IntegerValue(number, item)
                : new IdentValue(item));
        }

        return items;
    }

    // A channel number or a mark, within the range of an int.
    private static int NumberOf(IntegerValue value)
    {
        return (int)Math.Clamp(value.Number, int.MinValue, int.MaxValue);
    }

    private static void AddOnce(List<int> numbers, int number)
    {
        if (!numbers.Contains(number))
        {
            numbers.Add(number);
        }
    }
}
