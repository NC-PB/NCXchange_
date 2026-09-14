using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

// The waits of a job after each round (virtual machine 3.7, architecture 5.4): the channels whose marks and channels
// have come are released; when no channel can go on, the deadlock ERROR names what each channel waits for.
public sealed partial class JobRunner
{
    // After the round the waits that have come are released (virtual machine 3.7): WAIT_CHANNEL=c once channel c has
    // finished, and SYNC=m once every participant waits at m, all participants together, each with SYNC_RELEASE.
    private void ReleaseWaits()
    {
        foreach (ChannelRun channel in _channels)
        {
            if (channel.WaitingFor is int awaited && FindChannel(awaited) is { Finished: true })
            {
                channel.WaitingFor = null;
                channel.ChannelWaitBlock = null;
                _shared.Synchronized([channel.Channel, awaited]);
            }
        }

        foreach (ChannelRun channel in _channels)
        {
            if (channel.WaitingAt is int mark && MarkReleasable(channel, mark))
            {
                Release(channel.Participants, mark);
            }
        }
    }

    // Marks are matched in execution order, not by uniqueness: the participants are released when each of them waits at
    // the mark now, however often it has waited at that mark before; two channels that wait at different marks with the
    // same participants release nothing (virtual machine 3.7).
    // TODO(question): virtual machine 3.7 releases "all participants (WITH, default all)" without saying whose WITH
    // counts when the channels waiting at one mark name different participants; they are released only when every
    // participant waits at the mark with the same participants, and otherwise wait on, which ends in the deadlock that
    // names them, until that is answered.
    private bool MarkReleasable(ChannelRun channel, int mark)
    {
        foreach (int participant in channel.Participants)
        {
            if (FindChannel(participant) is not ChannelRun other
                || other.WaitingAt != mark
                || !other.Participants.SequenceEqual(channel.Participants))
            {
                return false;
            }
        }

        return true;
    }

    // All participants are released together, each with its SYNC_RELEASE in the round (virtual machine 3.7, 7); what
    // each did before the mark is before everything the others do after it.
    private void Release(IReadOnlyList<int> participants, int mark)
    {
        foreach (int participant in participants)
        {
            ChannelRun released = FindChannel(participant)
                ?? throw new InvalidOperationException("A participant of a SYNC is a channel of the job.");
            Block block = released.MarkBlock
                ?? throw new InvalidOperationException("A channel that waits at a mark stands at its SYNC block.");
            released.MarkBlock = null;
            _timeline.Add(released.Vm.ChangeWait(block, waitingAt: null, mark, participants, _round));
        }

        _shared.Synchronized(participants);
    }

    // No channel can execute a block and not all have finished: the deadlock ERROR, naming what every channel waits
    // for, the marks among them (virtual machine 3.7, architecture 5.4). It stands on the block the first waiting
    // channel waits at, in the file of that block.
    // TODO(question): virtual machine 3.7 reports the deadlock when all channels wait, and architecture 5.4 finishes
    // the job when not every channel waits; neither says what a channel is that waits at a mark with a channel that
    // has finished, or for a START_CHANNEL that no channel can give, and so can never go on. It is the deadlock as
    // well, the finished channels named in it, until that is answered.
    private void ReportDeadlock()
    {
        var waits = new List<string>();
        ChannelRun? first = null;
        foreach (ChannelRun channel in _channels)
        {
            waits.Add(WaitOf(channel));
            if (first is null && (channel.MarkBlock ?? channel.ChannelWaitBlock) is not null)
            {
                first = channel;
            }
        }

        string message = $"Deadlock: {string.Join("; ", waits)}; no channel can go on and no mark can be released "
            + "(virtual machine 3.7).";
        if (first is not null && (first.MarkBlock ?? first.ChannelWaitBlock) is Block block)
        {
            first.Vm.Diagnostics.Error(block, DiagnosticCodes.SyncDeadlock, message);
            return;
        }

        _diagnostics.Error(1, DiagnosticCodes.SyncDeadlock, message);
    }

    // What a channel waits for, in the words of the job: "channel 1 waits at SYNC=100 with 1,2 (ch1.ncx line 3)".
    private static string WaitOf(ChannelRun channel)
    {
        string name = string.Create(CultureInfo.InvariantCulture, $"channel {channel.Channel}");
        if (channel.Finished)
        {
            return name + " has finished";
        }

        if (channel.AwaitsStart)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"{name} waits to be started by START_CHANNEL={channel.Channel}");
        }

        var waits = new List<string>();
        if (channel.WaitingAt is int mark && channel.MarkBlock is Block markBlock)
        {
            waits.Add(string.Create(CultureInfo.InvariantCulture,
                $"at SYNC={mark} with {string.Join(",", channel.Participants)} "
                + $"({channel.CurrentFile} line {markBlock.Line})"));
        }

        if (channel.WaitingFor is int awaited && channel.ChannelWaitBlock is Block waitBlock)
        {
            waits.Add(string.Create(CultureInfo.InvariantCulture,
                $"for channel {awaited} to finish ({channel.CurrentFile} line {waitBlock.Line})"));
        }

        return name + " waits " + string.Join(" and ", waits);
    }
}
