using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

/// <summary>
/// The spindles and axes that the channels of a job share, from its [shared] table (machine-config 8, D20), and who
/// commanded them since two channels last synchronized: two channels commanding the same spindle between two marks is a
/// WARNING (virtual machine 3.7), and the axes of [shared] are checked like the spindles (F24).
/// </summary>
// TODO(question): virtual machine 3.7 warns for two channels commanding "the same spindle" between two marks, while
// machine-config 8 and D20 declare in [shared] the spindles and axes the channels share and let the scheduler check
// those; the spindles and axes of [shared] are checked and no other, until that is answered.
internal sealed class SharedResources
{
    // The spindles of [shared] by resource id, S1 (machine-config 4, 8).
    private readonly HashSet<string> _spindles = new(StringComparer.Ordinal);

    // The axes of [shared] by the NCX name the virtual machine keeps their position under, with the id [shared] names
    // them by: B to B1 (machine-config 4, 8; virtual machine 3.8 rule 3).
    private readonly Dictionary<string, string> _axes = new(StringComparer.Ordinal);

    // The channels of the job.
    private readonly List<int> _channels = [];

    // The last command of each channel on each shared resource, while it stands between marks with another channel.
    private readonly List<SharedCommand> _commands = [];

    /// <summary>
    /// The shared resources of a job.
    /// </summary>
    /// <param name="job">The job manifest with its [shared] table.</param>
    /// <param name="machine">The machine, whose [[axis]] list gives the NCX name of an axis id.</param>
    public SharedResources(JobManifest job, MachineConfig machine)
    {
        foreach (string spindle in job.SharedSpindles)
        {
            _spindles.Add(spindle);
        }

        foreach (string axis in job.SharedAxes)
        {
            _axes[machine.FindAxis(axis)?.NcxName ?? axis] = axis;
        }

        foreach (ChannelProgram channel in job.Channels)
        {
            if (!_channels.Contains(channel.Id))
            {
                _channels.Add(channel.Id);
            }
        }
    }

    /// <summary>
    /// The shared spindles and axes an executed block of a channel commands, each checked against the commands of the
    /// other channels since they last synchronized (virtual machine 3.7).
    /// </summary>
    public void Commanded(ChannelRun channel, Block block)
    {
        if (_spindles.Count == 0 && _axes.Count == 0)
        {
            return;
        }

        foreach (string spindle in channel.Vm.CommandedSpindles())
        {
            if (_spindles.Contains(spindle))
            {
                Command(channel, block, "spindle " + spindle, "Spindle " + spindle,
                    DiagnosticCodes.SpindleSharedBetweenMarks);
            }
        }

        foreach (string axis in channel.Vm.CommandedAxes())
        {
            if (_axes.TryGetValue(axis, out string? name))
            {
                Command(channel, block, "axis " + axis, "Axis " + name, DiagnosticCodes.AxisSharedBetweenMarks);
            }
        }
    }

    /// <summary>
    /// Channels synchronize: they wait at a mark together, one waits for the end of the other, or one starts the other.
    /// What each commanded before is before everything the others command after (virtual machine 3.7).
    /// </summary>
    public void Synchronized(IReadOnlyCollection<int> channels)
    {
        foreach (SharedCommand command in _commands)
        {
            if (channels.Contains(command.Channel))
            {
                command.Unsynchronized.ExceptWith(channels);
                command.Warned.ExceptWith(channels);
            }
        }

        _commands.RemoveAll(command => command.Unsynchronized.Count == 0);
    }

    // Two channels commanding the same shared resource between two marks is a WARNING (virtual machine 3.7), once for
    // the pair until they synchronize. The last command of a channel stands for its earlier ones: it stands between the
    // same marks with every channel an earlier one did.
    private void Command(ChannelRun channel, Block block, string resource, string what, string code)
    {
        SharedCommand? previous = null;
        foreach (SharedCommand command in _commands)
        {
            if (command.Resource == resource && command.Channel == channel.Channel)
            {
                previous = command;
            }
        }

        var warned = new HashSet<int>(previous?.Warned ?? []);
        foreach (SharedCommand earlier in _commands)
        {
            if (earlier.Resource != resource
                || earlier.Channel == channel.Channel
                || !earlier.Unsynchronized.Contains(channel.Channel))
            {
                continue;
            }

            if (!warned.Contains(earlier.Channel))
            {
                channel.Vm.Diagnostics.Warning(block, code, string.Create(CultureInfo.InvariantCulture,
                    $"{what} of [shared] is commanded here by channel {channel.Channel} and by channel "
                    + $"{earlier.Channel} ({earlier.File} line {earlier.Block.Line}) between the same two marks; two "
                    + $"channels commanding one resource between two marks is a WARNING (virtual machine 3.7, "
                    + $"machine-config 8)."));
                earlier.Warned.Add(channel.Channel);
            }

            warned.Add(earlier.Channel);
        }

        if (previous is not null)
        {
            _commands.Remove(previous);
        }

        var unsynchronized = new HashSet<int>(_channels);
        unsynchronized.Remove(channel.Channel);
        _commands.Add(new SharedCommand
        {
            Channel = channel.Channel,
            Resource = resource,
            Block = block,
            File = channel.CurrentFile,
            Unsynchronized = unsynchronized,
            Warned = warned,
        });
    }
}
