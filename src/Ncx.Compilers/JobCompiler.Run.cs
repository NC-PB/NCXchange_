using Ncx.Core.Expander;
using Ncx.Core.Jobs;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Compilers;

// The expansion and the STATIC runs of a job before it is compiled (architecture 5.5; virtual machine 3.7; architecture
// 5.4; implementation 16, P6-02): every file expanded once, then one virtual machine per channel program in rounds, as
// ncx check --job runs it, with a listener per channel that records the marks it passes, the mark every block of its
// expanded program stands behind and the subprograms it calls; and the run of the job as it would be written, with the
// SYNCs the job compiler generated.
public sealed partial class JobCompiler
{
    // The rules of the job scheduler (virtual machine 3.7, 5; machine-config 8), which the compile of one channel
    // program cannot report, since it runs one channel.
    private static readonly string[] s_jobRules =
    [
        Ncx.Core.Model.DiagnosticCodes.SyncDeadlock,
        Ncx.Core.Model.DiagnosticCodes.SpindleSharedBetweenMarks,
        Ncx.Core.Model.DiagnosticCodes.AxisSharedBetweenMarks,
        Ncx.Core.Model.DiagnosticCodes.ChannelNotInJob,
        Ncx.Core.Model.DiagnosticCodes.ChannelAlreadyStarted,
        Ncx.Core.Model.DiagnosticCodes.ChannelOtherThanHeader,
        Ncx.Core.Model.DiagnosticCodes.ChannelTwiceInJob,
    ];

    // Every file of the job expanded once, with the rules of the machine and the rewriters of the plugins, into
    // generated NCX blocks (architecture 5.5, language 4.15); channels that run programs of one file share it (language
    // 4.14). The job compiler places the channel-bound words of the expanded program, those the expansion generates as
    // well as those of the file, since every function the compiler writes is bound (virtual machine 3.8 rule 2a, D56),
    // and the compiler of the family writes the channel programs without expanding them again. A rewriter learns the
    // channel of the job for a program the manifest runs, since the channel of the job applies (machine-config 8,
    // D106); the run sees every section on the channel of its file, as ncx check --job does.
    private static Dictionary<NcxProgram, NcxProgram> ExpandFiles(JobManifest job,
        IReadOnlyDictionary<string, NcxProgram> files, MachineConfig machine, CompileOptions options)
    {
        var expanded = new Dictionary<NcxProgram, NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (NcxProgram file in FilesOf(job, files))
        {
            NcxProgram onJobChannels = file with { Sections = SectionsOnJobChannels(file, job, files) };
            NcxProgram result = Expander.Expand(onJobChannels, machine, options.Rewriters);
            var sections = new List<Section>();
            for (int index = 0; index < result.Sections.Count; index++)
            {
                sections.Add(result.Sections[index] with { Channel = file.Sections[index].Channel });
            }

            expanded[file] = result with { Sections = sections };
        }

        return expanded;
    }

    // The sections of a file, each program that a [[channel]] of the manifest runs on the channel of that [[channel]]
    // (machine-config 8, D48).
    private static List<Section> SectionsOnJobChannels(NcxProgram file, JobManifest job,
        IReadOnlyDictionary<string, NcxProgram> files)
    {
        var sections = new List<Section>();
        foreach (Section section in file.Sections)
        {
            int channel = section.Channel;
            foreach (ChannelProgram run in job.Channels)
            {
                if (ReferenceEquals(FileOf(run, files), file)
                    && ReferenceEquals(FindProgram(file, run.Program), section))
                {
                    channel = run.Id;
                }
            }

            sections.Add(section with { Channel = channel });
        }

        return sections;
    }

    // The job runs STATIC once (virtual machine 1, 3.7). A job that an ERROR stops, the deadlock among them, reports
    // what ncx check --job reports and is not compiled (virtual machine 2.9); otherwise the rules of the job are
    // reported here, and what the virtual machine finds on the blocks the compile of each channel program reports.
    // Null after the ERROR.
    private static List<JobChannel>? RunJob(string jobFile, JobManifest job,
        IReadOnlyDictionary<string, NcxProgram> files, Dictionary<NcxProgram, NcxProgram> expanded,
        MachineConfig machine, Diagnostics diagnostics)
    {
        // The run reports on diagnostics of its own, so that what the parser and the expander found goes to the
        // compile of the first channel program of its file, once.
        var runs = new Dictionary<NcxProgram, NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (NcxProgram file in FilesOf(job, files))
        {
            runs[file] = expanded[file] with { Diagnostics = new Diagnostics(file.FileName) };
        }

        // Every executed block raises BLOCK_WRITE, so that the listener of a channel sees the blocks between its marks
        // (virtual machine 7).
        var jobDiagnostics = new Diagnostics(jobFile);
        var runner = new JobRunner(job, machine, VmOptions.ForMachine(machine) with { RaiseBlockWrite = true },
            ExecutionMode.Static, jobDiagnostics);
        var marks = new List<ChannelMarks>();
        foreach (ChannelProgram channel in job.Channels)
        {
            NcxProgram file = FileOf(channel, files);
            var channelMarks = new ChannelMarks(expanded[file]);
            runner.Add(channel, runs[file]).Vm.Subscribe(channelMarks);
            marks.Add(channelMarks);
        }

        JobResult result = runner.Run();
        AddAll(diagnostics, jobDiagnostics);
        foreach (NcxProgram file in FilesOf(job, files))
        {
            if (result.Stopped)
            {
                AddAll(diagnostics, expanded[file].Diagnostics);
            }

            AddRun(diagnostics, runs[file].Diagnostics, result.Stopped);
        }

        return result.Stopped ? null : Channels(job, files, expanded, result, marks);
    }

    // The job as it would be written runs STATIC once more, every channel program with the SYNCs the job compiler
    // generated for start_mark and for the words bound to every channel (machine-config 5; D56): all channels waiting
    // with no releasable mark is the deadlock ERROR (virtual machine 3.7), and nothing is written (2.9). The run of the
    // written job reports on diagnostics of its own, since the compile of each channel program reports what the
    // virtual machine finds on its blocks; only the deadlock is taken from it. True after the ERROR.
    // TODO(question): machine-config 5 writes start_mark "as the first block of every channel program" and D56 has the
    // job compiler insert "the SYNC it needs", but no document says where such a SYNC stands when a channel is started
    // by the START_CHANNEL of another or waits for the end of another with WAIT_CHANNEL (language 4.8), so that it can
    // never be released; a generated SYNC that a channel waits at in the deadlock is the ERROR CMP712 that names the
    // deadlock, until that is answered.
    private static bool RunWrittenJob(string jobFile, JobManifest job, List<JobChannel> channels,
        MachineConfig machine, Diagnostics diagnostics)
    {
        var jobDiagnostics = new Diagnostics(jobFile);
        var runner = new JobRunner(job, machine, VmOptions.ForMachine(machine) with { RaiseBlockWrite = true },
            ExecutionMode.Static, jobDiagnostics);
        var waits = new List<ChannelMarks>();
        for (int index = 0; index < channels.Count; index++)
        {
            JobChannel channel = channels[index];
            NcxProgram written = channel.Build() with { Diagnostics = new Diagnostics(channel.File.FileName) };
            var marks = new ChannelMarks(written);
            runner.Add(job.Channels[index], written).Vm.Subscribe(marks);
            waits.Add(marks);
        }

        JobResult result = runner.Run();
        if (!result.Stopped || DeadlockOf(result, jobDiagnostics) is not Diagnostic deadlock)
        {
            return false;
        }

        // The ERROR stands on every SYNC of the job compiler that a channel waits at in the deadlock, the block the
        // documents give the job compiler no other place for (D98); a deadlock that no such SYNC takes part in is the
        // deadlock ERROR of the job as the scheduler reports it (virtual machine 3.7).
        bool reported = false;
        for (int index = 0; index < channels.Count; index++)
        {
            if (waits[index].WaitingAt is Block { Generated: { Source: Writer } generated } sync)
            {
                var report = new Diagnostics(channels[index].File.FileName);
                report.Error(sync, DiagnosticCodes.GeneratedSyncDeadlocks,
                    $"{sync.Find("SYNC")?.ToCanonical()}, which the job compiler writes for {generated.Reason}, can "
                    + $"never be released in the job as written (machine-config 5, D56). {deadlock.Message}");
                AddAll(diagnostics, report);
                reported = true;
            }
        }

        if (!reported)
        {
            diagnostics.Add(deadlock);
        }

        return true;
    }

    // The deadlock ERROR of a run of the job, where the job scheduler reported it: on the block the first waiting
    // channel waits at, or on the manifest (virtual machine 3.7); null when the run stopped on another ERROR, which the
    // compile of the channel program reports.
    private static Diagnostic? DeadlockOf(JobResult result, Diagnostics jobDiagnostics)
    {
        var lists = new List<Diagnostics> { jobDiagnostics };
        foreach (ChannelRun channel in result.Channels)
        {
            lists.Add(channel.Diagnostics);
        }

        foreach (Diagnostics list in lists)
        {
            foreach (Diagnostic diagnostic in list.Items)
            {
                if (diagnostic.Code == Ncx.Core.Model.DiagnosticCodes.SyncDeadlock)
                {
                    return diagnostic;
                }
            }
        }

        return null;
    }

    // The channels of the job in the order of the manifest, each with the program it ran in its expanded file and the
    // marks it passed; what the parser and the expander found in a file starts the program of its first channel. A
    // subprogram that no channel calls stands in no channel file, with a WARNING.
    private static List<JobChannel> Channels(JobManifest job, IReadOnlyDictionary<string, NcxProgram> files,
        Dictionary<NcxProgram, NcxProgram> expanded, JobResult result, List<ChannelMarks> marks)
    {
        var channels = new List<JobChannel>();
        var started = new HashSet<NcxProgram>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < job.Channels.Count; index++)
        {
            NcxProgram file = expanded[FileOf(job.Channels[index], files)];
            channels.Add(new JobChannel
            {
                Channel = job.Channels[index].Id,
                File = file,
                Program = FindProgram(file, result.Channels[index].ProgramName)
                    ?? throw new InvalidOperationException("A job that ran has run a program of the file on every "
                        + "channel."),
                Marks = marks[index],
                FileDiagnostics = started.Add(file) ? file.Diagnostics : new Diagnostics(file.FileName),
                Diagnostics = new Diagnostics(file.FileName),
            });
        }

        ReportUncalledSubs(channels);
        return channels;
    }

    // What the run found in one file: everything, after what the parser found, when the job stopped; else the rules of
    // the job alone.
    private static void AddRun(Diagnostics diagnostics, Diagnostics run, bool stopped)
    {
        foreach (Diagnostic diagnostic in run.Items)
        {
            if (stopped || s_jobRules.Contains(diagnostic.Code))
            {
                diagnostics.Add(diagnostic);
            }
        }
    }

    // The program of a file that runs on a channel: the program the manifest or a START_CHANNEL names, the first of the
    // file without a name (language 4.13; machine-config 8, D48); null when the file has no such program, which the
    // run of the job reports.
    private static Section? FindProgram(NcxProgram file, string? name)
    {
        foreach (Section program in file.Programs)
        {
            if (name is null || program.Name == name)
            {
                return program;
            }
        }

        return null;
    }

    // A subprogram of a file of the job that no channel program calls stands in no channel file: every channel file
    // holds the subprograms its program calls (language 4.13, D99).
    // TODO(question): the documents do not say where a job compile writes a subprogram that no channel program calls;
    // it is written into no channel file, with a WARNING as under program_layout = "file_per_program" (CMP003), until
    // that is answered.
    private static void ReportUncalledSubs(List<JobChannel> channels)
    {
        var reported = new HashSet<NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (JobChannel channel in channels)
        {
            if (!reported.Add(channel.File))
            {
                continue;
            }

            foreach (Section sub in channel.File.Subs)
            {
                if (!CalledByAChannel(channels, channel.File, sub))
                {
                    channel.Diagnostics.Warning(channel.File.Blocks[sub.FirstBlock],
                        DiagnosticCodes.SubprogramOfNoChannel,
                        $"SUB {sub.Name} is called by no channel program of the job, and a channel file holds the "
                        + "subprograms its program calls, so it is in no output file (language 4.13, D99).");
                }
            }
        }
    }

    private static bool CalledByAChannel(List<JobChannel> channels, NcxProgram file, Section sub)
    {
        foreach (JobChannel channel in channels)
        {
            if (ReferenceEquals(channel.File, file) && channel.Marks.Subs.Contains(sub))
            {
                return true;
            }
        }

        return false;
    }
}
