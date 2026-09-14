using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Jobs;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Jobs;

/// <summary>
/// The Nakamura WY-250L pair as one job, through the library (implementation 16, P6-01; milestone M9): both paths read
/// by the Fanuc reader with the builder tables of nakamura-ntjx.toml, written as NCX and parsed again, and run by the
/// job manifest of Fixtures/ without deadlock, the wait marks M106 to M195 paired in execution order and M199 as the
/// start synchronization. The G411 jumps on the part status stay RAW and are not followed, so every section of both
/// programs runs; the block skip switch is on, which skips the call of the geometry program O8889 that the repository
/// does not hold. The acceptance of the job is "no deadlock", not "every block executes" (the risks of the phase).
/// </summary>
public sealed class NakamuraJobTests
{
    // The marks of both paths in the order they are released: M199 first, the program start synchronization (language
    // 4.8), then M110, M112 and M106 as the programs reach them, then M190 to M195 around the workpiece transfer.
    private static readonly string[] s_marks =
    [
        "SYNC=199", "SYNC=110", "SYNC=112", "SYNC=106", "SYNC=190", "SYNC=191", "SYNC=192", "SYNC=193", "SYNC=194",
        "SYNC=195",
    ];

    // P6-01 done when: the Nakamura WY250L pair reads and analyzes without deadlock (INTERPRETED, ncx analyze), and
    // checks without one (STATIC, ncx check).
    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Static)]
    public void NakamuraJob_ConvertedPair_RunsWithoutDeadlockAndPairsEveryMark(ExecutionMode mode)
    {
        var jobDiagnostics = new Diagnostics(JobFixture.NakamuraJob);
        JobResult result = Run(mode, jobDiagnostics);

        string text = TextOf(jobDiagnostics, result);
        Assert.False(result.Stopped, text);
        Assert.DoesNotContain("ERROR", text, StringComparison.Ordinal);
        foreach (ChannelRun channel in result.Channels)
        {
            Assert.True(channel.Finished, $"Channel {channel.Channel} did not finish.\n{text}");
        }

        Assert.Equal(s_marks, ReleasedMarks(result));
    }

    // Language 4.8: M199 is the first block of both programs that waits, and both paths are released from it together.
    [Fact]
    public void NakamuraJob_M199_ReleasesBothPathsFirst()
    {
        JobResult result = Run(ExecutionMode.Interpreted, new Diagnostics(JobFixture.NakamuraJob));

        SyncEvent first = result.Timeline[0];
        Assert.Equal(199, first.Mark);
        Assert.Equal([1, 2], first.Channels);
        SyncEvent firstRelease = result.Timeline.First(sync => sync.Released);
        Assert.Equal(199, firstRelease.Mark);
    }

    // The job of the fixture over the converted pair.
    private static JobResult Run(ExecutionMode mode, Diagnostics jobDiagnostics)
    {
        MachineConfig machine = Machine();
        JobManifest? job = JobManifestLoader.LoadText(JobFixture.ReadText(JobFixture.NakamuraJob), jobDiagnostics);
        Assert.True(job is not null, jobDiagnostics.ToText());
        VmOptions options = VmOptions.ForMachine(machine) with { SkipBlocks = SkipBlocks.Every, ExpandCycles = true };
        var runner = new JobRunner(job, machine, options, mode, jobDiagnostics);
        foreach (ChannelProgram channel in job.Channels)
        {
            runner.Add(channel, Converted(channel.File, machine));
        }

        return runner.Run();
    }

    // A path as ncx convert writes it and ncx analyze reads it again: the Fanuc reader with the machine, the canonical
    // writer, the parser and the expander (phase 3, P3-02; virtual machine 1).
    private static NcxProgram Converted(string file, MachineConfig machine)
    {
        string source = Path.ChangeExtension(file, ".nc");
        NcxProgram read = new FanucReader().Read(new SourceFile(source, Fixture.ReadText("sources/" + source)),
            machine, new ReadOptions());
        NcxProgram program = Parser.Parse(NcxWriter.Write(read), file, new ParserOptions());
        Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
        return Expander.Expand(program, machine, []);
    }

    // nakamura-ntjx.toml, which names no cycle catalog (machine-config 6).
    private static MachineConfig Machine()
    {
        var diagnostics = new Diagnostics("nakamura-ntjx.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(Fixture.ReadText("machines/nakamura-ntjx.toml"),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }

    // The marks released in the order of the timeline, once per release of both paths.
    private static List<string> ReleasedMarks(JobResult result)
    {
        var marks = new List<string>();
        foreach (SyncEvent sync in result.Timeline)
        {
            if (sync.Released && sync.Channel == 1)
            {
                marks.Add("SYNC=" + sync.Mark.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return marks;
    }

    private static string TextOf(Diagnostics jobDiagnostics, JobResult result)
    {
        string text = jobDiagnostics.ToText();
        foreach (ChannelRun channel in result.Channels)
        {
            text += channel.Diagnostics.ToText();
        }

        return text;
    }
}
