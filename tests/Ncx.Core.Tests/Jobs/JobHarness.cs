using System.Globalization;
using Ncx.Core.Jobs;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Tests.VirtualMachine;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// A job run for the tests (virtual machine 3.7): the files of its channels parsed as user files, one virtual machine
/// per channel, a listener on every channel that records the events of all channels in the order they were raised,
/// and the result, the diagnostics and the timeline of the marks to assert on.
/// </summary>
internal sealed class JobHarness : IVmListener
{
    private JobHarness(JobResult result, Diagnostics jobDiagnostics, List<VmEvent> events)
    {
        Result = result;
        JobDiagnostics = jobDiagnostics;
        Events = events;
    }

    private JobHarness()
    {
        Result = new JobResult { Channels = [], Timeline = [], Rounds = 0, Stopped = false };
        JobDiagnostics = new Diagnostics("test.ncxjob.toml");
    }

    public JobResult Result { get; private set; }

    /// <summary>
    /// What the job reported about itself, on the manifest.
    /// </summary>
    public Diagnostics JobDiagnostics { get; private set; }

    /// <summary>
    /// The events of every channel in the order they were raised.
    /// </summary>
    public List<VmEvent> Events { get; } = [];

    /// <summary>
    /// A file of one program named P with these lines between PROGRAM=BEGIN, line 2, and FILE=END; the last line is
    /// PROGRAM=END unless the test gives more sections.
    /// </summary>
    public static string File(params string[] lines)
    {
        return "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"P\"\n" + string.Join("\n", lines) + "\nFILE=END\n";
    }

    /// <summary>
    /// Runs the files as the channels 1, 2, ... of one job, INTERPRETED.
    /// </summary>
    public static JobHarness Run(params string[] files)
    {
        return Run(new JobSetup { Files = files });
    }

    /// <summary>
    /// Runs a job; every file must parse without an ERROR.
    /// </summary>
    public static JobHarness Run(JobSetup setup)
    {
        var harness = new JobHarness();
        MachineConfig machine = setup.Machine ?? VmMachines.Default();
        var programs = new Dictionary<string, NcxProgram>(StringComparer.Ordinal);
        var channels = new List<ChannelProgram>();
        for (int index = 0; index < setup.Files.Count; index++)
        {
            string name = FileName(index + 1);
            NcxProgram program = Parser.Parse(setup.Files[index], name, new ParserOptions());
            Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
            programs[name] = program;
            channels.Add(new ChannelProgram { Id = index + 1, File = name });
        }

        var job = new JobManifest
        {
            Machine = "test.toml",
            Channels = setup.Channels ?? channels,
            SharedSpindles = setup.SharedSpindles,
            SharedAxes = setup.SharedAxes,
        };
        var runner = new JobRunner(job, machine, VmOptions.ForMachine(machine), setup.Mode, harness.JobDiagnostics);
        foreach (ChannelProgram channel in job.Channels)
        {
            ChannelRun run = runner.Add(channel, programs[channel.File]);
            run.Vm.Subscribe(harness);
        }

        harness.Result = runner.Run();
        return harness;
    }

    public void On(VmEvent vmEvent)
    {
        Events.Add(vmEvent);
    }

    /// <summary>
    /// The releases of the job in their order, one per mark and round: "SYNC=100 round 4".
    /// </summary>
    public List<string> Releases()
    {
        var releases = new List<string>();
        foreach (SyncEvent sync in Result.Timeline)
        {
            string release = string.Create(CultureInfo.InvariantCulture, $"SYNC={sync.Mark} round {sync.Round}");
            if (sync.Released && (releases.Count == 0 || releases[^1] != release))
            {
                releases.Add(release);
            }
        }

        return releases;
    }

    /// <summary>
    /// The timeline as lines, "2 SYNC_WAIT(3): mark 100, channels 1,2, round 2", the channel first.
    /// </summary>
    public List<string> Timeline()
    {
        var lines = new List<string>();
        foreach (SyncEvent sync in Result.Timeline)
        {
            lines.Add(sync.Channel.ToString(CultureInfo.InvariantCulture) + " " + sync);
        }

        return lines;
    }

    /// <summary>
    /// Every diagnostic of the job: about the job itself, then those of each file in the order of the channels.
    /// </summary>
    public List<Diagnostic> AllDiagnostics()
    {
        var all = new List<Diagnostic>(JobDiagnostics.Items);
        var files = new HashSet<Diagnostics>(ReferenceEqualityComparer.Instance);
        foreach (ChannelRun channel in Result.Channels)
        {
            if (files.Add(channel.Diagnostics))
            {
                all.AddRange(channel.Diagnostics.Items);
            }
        }

        return all;
    }

    /// <summary>
    /// The codes of every diagnostic of the job.
    /// </summary>
    public List<string> Codes()
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in AllDiagnostics())
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// The one diagnostic with this code, the test failing with every diagnostic otherwise.
    /// </summary>
    public Diagnostic Single(string code)
    {
        var found = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in AllDiagnostics())
        {
            if (diagnostic.Code == code)
            {
                found.Add(diagnostic);
            }
        }

        Assert.True(found.Count == 1, $"Expected one {code}, got:\n{Text()}");
        return found[0];
    }

    /// <summary>
    /// Asserts that the job ran to its end with every channel finished and no ERROR, printing the diagnostics
    /// otherwise.
    /// </summary>
    public void AssertFinished()
    {
        Assert.False(Result.Stopped, Text());
        foreach (ChannelRun channel in Result.Channels)
        {
            Assert.True(channel.Finished, $"Channel {channel.Channel} did not finish.\n{Text()}");
        }
    }

    /// <summary>
    /// The position of the first event of a channel of this kind on this line in the order of all events; -1 for none.
    /// </summary>
    public int IndexOf(int channel, string kind, int? line = null)
    {
        for (int index = 0; index < Events.Count; index++)
        {
            VmEvent vmEvent = Events[index];
            if (vmEvent.Channel == channel && vmEvent.Kind == kind && (line is null || vmEvent.Block.Line == line))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The events of one channel in their order.
    /// </summary>
    public List<VmEvent> EventsOf(int channel)
    {
        return Events.FindAll(vmEvent => vmEvent.Channel == channel);
    }

    /// <summary>
    /// Every diagnostic of the job as text.
    /// </summary>
    public string Text()
    {
        var text = new System.Text.StringBuilder();
        foreach (Diagnostic diagnostic in AllDiagnostics())
        {
            text.Append(diagnostic.ToText()).Append('\n');
        }

        return text.ToString();
    }

    private static string FileName(int channel)
    {
        return "ch" + channel.ToString(CultureInfo.InvariantCulture) + ".ncx";
    }
}
