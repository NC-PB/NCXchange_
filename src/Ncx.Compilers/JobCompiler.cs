using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// Compiles a job for a machine of several channels (implementation 16, P6-02; architecture 8, 10; virtual machine
/// 3.7, 3.8 rule 2a; D56): per channel of the job manifest a compile of its program through the compiler of the
/// machine's family, with a view of the job, so that SYNC is written as the wait code of [sync] for the channels of the
/// job; start_mark as the first block of every channel program; the marks within mark_range or the range of their
/// [sync] group; a word the machine accepts only from one channel moved to that channel's program at the same mark, a
/// word it needs in every channel program duplicated into each behind a generated SYNC; one output file per channel,
/// named as [format] channel_files says. Every file of the job is expanded once, and the channel-bound words of the
/// expanded programs are placed, those an expansion rule generates as well as those of the file. The job runs STATIC
/// once first, as ncx check --job runs it, which pairs the marks of the channels and reports the deadlock, and once
/// more as it would be written, with the SYNCs the job compiler generated, whose deadlock is an ERROR as well.
/// </summary>
public sealed partial class JobCompiler
{
    private readonly ICompiler _compiler;

    /// <summary>
    /// A job compiler that writes every channel program with the compiler of the machine's family (architecture 8).
    /// </summary>
    /// <param name="compiler">The compiler of the controller of the machine file.</param>
    public JobCompiler(ICompiler compiler)
    {
        _compiler = compiler;
    }

    /// <summary>
    /// Compiles the channel programs of a job manifest. Never throws on bad input: what the job, a program or the
    /// machine lacks is a diagnostic of the result (code-guidelines 6).
    /// </summary>
    /// <param name="jobFile">The job manifest as the user names it, which the diagnostics about the job carry
    /// (D98).</param>
    /// <param name="job">The job manifest: the program of every channel (machine-config 8).</param>
    /// <param name="files">The parsed NCX file of every file the manifest names, by its file value; channels that
    /// run programs of one file share it.</param>
    /// <param name="machine">The machine of the job, with its cycle catalog.</param>
    /// <param name="options">The plugins and the tool table.</param>
    /// <returns>One output file per channel in the order of the manifest, none after an ERROR, and the diagnostics of
    /// the job and of every channel.</returns>
    public CompileResult Compile(string jobFile, JobManifest job, IReadOnlyDictionary<string, NcxProgram> files,
        MachineConfig machine, CompileOptions options)
    {
        var diagnostics = new Diagnostics(jobFile);

        // 1. Every file of the job expanded once; an ERROR of the parser or the expander stops the job before anything
        // runs (virtual machine 2.9).
        Dictionary<NcxProgram, NcxProgram> expanded = ExpandFiles(job, files, machine, options);
        if (ReportErrorsBeforeTheRun(job, files, expanded, diagnostics))
        {
            return Stopped(diagnostics);
        }

        // 2. The job runs STATIC once, as ncx check --job runs it: the marks each channel passes in execution order,
        // which pair the channels (virtual machine 3.7), and the rules of the job, the deadlock among them.
        List<JobChannel>? channels = RunJob(jobFile, job, files, expanded, machine, diagnostics);
        if (channels is null)
        {
            return Stopped(diagnostics);
        }

        // 3. The marks within their ranges, start_mark as the first block of every channel program, the channel-bound
        // words of the expanded programs moved or duplicated (machine-config 5; virtual machine 3.8 rule 2a; D56).
        CheckMarks(channels, machine);
        InsertStartMark(channels, machine);
        BindWords(channels, machine);
        if (AddChannelDiagnostics(channels, diagnostics))
        {
            return Stopped(diagnostics);
        }

        // 4. The job as it would be written, with the SYNCs the job compiler generated, runs STATIC once more: a SYNC
        // that can never be released is the deadlock ERROR of the job, and nothing is written (virtual machine 3.7).
        if (RunWrittenJob(jobFile, job, channels, machine, diagnostics))
        {
            return Stopped(diagnostics);
        }

        // 5. Every channel program through the compiler of the family, with the view of the job, which tells it that
        // the program comes expanded; one file per channel, and none when a channel has an ERROR, since a run stops on
        // ERROR (virtual machine 2.9; D205).
        var outputs = new List<ChannelOutput>();
        List<int> jobChannels = ChannelsOf(channels);
        MachineConfig jobMachine = JobMachine(machine, jobChannels);
        foreach (JobChannel channel in channels)
        {
            var view = new JobView { Channel = channel.Channel, Channels = jobChannels };
            CompileResult result = _compiler.Compile(channel.Build(), jobMachine, options with { Job = view });
            foreach (Diagnostic diagnostic in result.Diagnostics.Items)
            {
                diagnostics.Add(diagnostic);
            }

            foreach (CompiledFile file in result.Files)
            {
                outputs.Add(new ChannelOutput { Channel = channel.Channel, Program = channel.Program, File = file });
            }
        }

        if (diagnostics.HasErrors)
        {
            return Stopped(diagnostics);
        }

        return new CompileResult { Files = ChannelOutputNaming.Name(outputs, job, machine), Diagnostics = diagnostics };
    }

    // The machine as a channel program of the job sees it (language 4.8; machine-config 5): a SYNC without WITH waits
    // for every channel of the job, and the marks were checked against mark_range and the ranges of the groups of
    // [sync] by the job compiler, so the compiler of the family checks none again.
    private static MachineConfig JobMachine(MachineConfig machine, List<int> jobChannels)
    {
        return machine with
        {
            Machine = machine.Machine with { Channels = jobChannels },
            Sync = machine.Sync is SyncConfig sync ? sync with { MarkRange = null } : null,
        };
    }

    // The channels of the job in ascending order (language 4.8, WITH: default all channels of the job).
    private static List<int> ChannelsOf(List<JobChannel> channels)
    {
        var ids = new List<int>();
        foreach (JobChannel channel in channels)
        {
            ids.Add(channel.Channel);
        }

        ids.Sort();
        return ids;
    }

    // An ERROR of the parser or the expander in any file of the job is reported with everything the parser and the
    // expander reported, once per file.
    private static bool ReportErrorsBeforeTheRun(JobManifest job, IReadOnlyDictionary<string, NcxProgram> files,
        Dictionary<NcxProgram, NcxProgram> expanded, Diagnostics diagnostics)
    {
        bool errors = false;
        foreach (NcxProgram file in FilesOf(job, files))
        {
            errors |= expanded[file].Diagnostics.HasErrors;
        }

        if (errors)
        {
            foreach (NcxProgram file in FilesOf(job, files))
            {
                AddAll(diagnostics, expanded[file].Diagnostics);
            }
        }

        return errors;
    }

    // What the job compiler reported on the files of the channels, after the job's own; true after an ERROR.
    private static bool AddChannelDiagnostics(List<JobChannel> channels, Diagnostics diagnostics)
    {
        foreach (JobChannel channel in channels)
        {
            AddAll(diagnostics, channel.Diagnostics);
        }

        return diagnostics.HasErrors;
    }

    // The file of every [[channel]], once, in the order of the manifest.
    private static List<NcxProgram> FilesOf(JobManifest job, IReadOnlyDictionary<string, NcxProgram> files)
    {
        var distinct = new List<NcxProgram>();
        foreach (ChannelProgram channel in job.Channels)
        {
            NcxProgram file = FileOf(channel, files);
            if (!distinct.Exists(known => ReferenceEquals(known, file)))
            {
                distinct.Add(file);
            }
        }

        return distinct;
    }

    private static NcxProgram FileOf(ChannelProgram channel, IReadOnlyDictionary<string, NcxProgram> files)
    {
        return files.TryGetValue(channel.File, out NcxProgram? file)
            ? file
            : throw new ArgumentException($"The file {channel.File} of channel {channel.Id} is not given.",
                nameof(files));
    }

    private static void AddAll(Diagnostics diagnostics, Diagnostics from)
    {
        foreach (Diagnostic diagnostic in from.Items)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static CompileResult Stopped(Diagnostics diagnostics)
    {
        return new CompileResult { Files = [], Diagnostics = diagnostics };
    }
}
