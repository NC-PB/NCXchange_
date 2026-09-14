using System.Globalization;
using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Jobs;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Cli;

/// <summary>
/// The stages of ncx check --job and ncx analyze --job (architecture 10; virtual machine 3.7; machine-config 8): read
/// the job manifest, the machine it names unless --machine names another, and the file of every channel with its vars
/// file; parse and expand each file once; run the channel programs as one job in rounds, STATIC for check and
/// INTERPRETED for analyze; collect the diagnostics of the manifest, the machine, the job and every file.
/// </summary>
internal static class JobPipeline
{
    /// <summary>
    /// Runs the channel programs of a job manifest as one job.
    /// </summary>
    /// <param name="jobFile">The job manifest as the command line names it, &lt;name&gt;.ncxjob.toml; the diagnostics
    /// about the job carry this name (D98).</param>
    /// <param name="settings">The machine and the options of the command line; its file is not read.</param>
    /// <param name="listenersFor">Makes the listeners of one channel once the machine is loaded, from the machine, the
    /// name of its file and the channel; not called when the job does not start.</param>
    public static JobPipelineRun Run(string jobFile, RunSettings settings,
        Func<MachineConfig, string?, ChannelRun, IReadOnlyList<IVmListener>> listenersFor)
    {
        var diagnostics = new Diagnostics(jobFile);

        // Code 2 is decided before the run starts and takes precedence: the job manifest, ncx.toml, the machine file,
        // the file of every channel and its vars file are read first, all of them, so that all are reported (D97,
        // architecture 10).
        string? manifestText = InputFile.Read(jobFile, DiagnosticCodes.JobManifestUnreadable, "The job manifest",
            "machine-config 8, D97", diagnostics);
        if (manifestText is null)
        {
            return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = false };
        }

        // A manifest that loads with an ERROR stops the job before it starts, with exit code 1 (D97, P2-01).
        var manifestDiagnostics = new Diagnostics(jobFile);
        JobManifest? job = JobManifestLoader.LoadText(Pipeline.WithoutByteOrderMark(manifestText), manifestDiagnostics);
        AddAll(diagnostics, manifestDiagnostics);
        if (job is null)
        {
            return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = true };
        }

        // The machine of the job is the machine its manifest names, unless --machine names another (implementation 16,
        // P6-01).
        string folder = Path.GetDirectoryName(jobFile) ?? "";
        RunSettings run = settings.MachineFile is null
            ? settings with { MachineFile = MachineOf(job, folder, settings.WorkingDirectory), MachineOfJob = jobFile }
            : settings;
        RunMachine runMachine = RunMachine.Select(run, diagnostics);
        ChannelFiles? files = ReadFiles(job, folder, settings.Interpreted, diagnostics);
        if (files is null || !runMachine.InputsRead)
        {
            return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = false };
        }

        // ncx.toml, a machine file or a cycle catalog that loads with an ERROR stops the job before it starts, and so
        // does a vars file with an ERROR (D97; P1-07, P4-01).
        if (runMachine.Machine is not MachineConfig machine)
        {
            return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = true };
        }

        var startValues = new Dictionary<string, IReadOnlyDictionary<string, Value>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> varsFile in files.VarsFiles)
        {
            if (Pipeline.LoadVars(varsFile.Value, files.VarsTexts[varsFile.Key], diagnostics)
                is not IReadOnlyDictionary<string, Value> values)
            {
                return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = true };
            }

            startValues[varsFile.Key] = values;
        }

        // Every file is parsed and expanded once (virtual machine 1, architecture 5.5); an ERROR of the parser or the
        // expander stops the job before its first round (virtual machine 2.9).
        var programs = new Dictionary<string, NcxProgram>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> text in files.Texts)
        {
            NcxProgram program = Parser.Parse(Pipeline.WithoutByteOrderMark(text.Value), text.Key, new ParserOptions());
            programs[text.Key] = Expander.Expand(program, machine, []);
        }

        // One job of the channel programs, with the run options of the command line over those the machine gives (D37,
        // D53; machine-config 7): STATIC for check, INTERPRETED for analyze (virtual machine 1). An INTERPRETED run
        // loads the external programs its CALLs name from the working directory (virtual machine 3.6).
        VmOptions options = VmOptions.ForMachine(machine) with
        {
            SkipBlocks = settings.SkipBlocks,
            ExpandCycles = settings.ExpandCycles,
        };
        ExecutionMode mode = settings.Interpreted ? ExecutionMode.Interpreted : ExecutionMode.Static;
        Func<string, NcxProgram?>? externalPrograms = settings.Interpreted
            ? name => Pipeline.LoadExternalProgram(name, settings.WorkingDirectory, machine, [])
            : null;
        var jobDiagnostics = new Diagnostics(jobFile);
        var runner = new JobRunner(job, machine, options, mode, jobDiagnostics);
        var ranFiles = new List<NcxProgram>();
        foreach (ChannelProgram channel in job.Channels)
        {
            string file = Path.Combine(folder, channel.File);
            NcxProgram program = programs[file];
            ChannelRun channelRun = runner.Add(channel, program, startValues.GetValueOrDefault(file), externalPrograms);
            foreach (IVmListener listener in listenersFor(machine, runMachine.FileName, channelRun))
            {
                channelRun.Vm.Subscribe(listener);
            }

            if (!ranFiles.Exists(ran => ReferenceEquals(ran, program)))
            {
                ranFiles.Add(program);
            }
        }

        JobResult result = runner.Run();

        // Collect: what the job reported about itself, then what every file reported, in the order of the manifest
        // (D98).
        AddAll(diagnostics, jobDiagnostics);
        foreach (NcxProgram program in ranFiles)
        {
            AddAll(diagnostics, program.Diagnostics);
        }

        return new JobPipelineRun { Diagnostics = diagnostics, InputsRead = true, Result = result };
    }

    // The machine file that the manifest names: a file next to the manifest, where the files of its channels stand
    // (language 4.8), else the value as --machine takes it, a path from the working directory or a name in the machine
    // folders (P2-04).
    // TODO(question): machine-config 8 names the machine of a job ("nakamura-ntjx.toml") without saying where ncx looks
    // for its file, next to the manifest like the files of the channels or where --machine looks; a file next to the
    // manifest is taken first, then the value as --machine takes it, until that is answered.
    private static string MachineOf(JobManifest job, string folder, string workingDirectory)
    {
        string nextToManifest = Path.Combine(folder, job.Machine);
        return File.Exists(Path.Combine(workingDirectory, nextToManifest)) ? nextToManifest : job.Machine;
    }

    // The file of every channel, next to the manifest (language 4.8), read once when two channels run programs of one
    // file (language 4.14), and in an INTERPRETED run the vars file next to each (virtual machine 3.6; machine-config
    // 10). Null when one of them cannot be read, which decides exit code 2 (D97); every one is read, so that all are
    // reported.
    private static ChannelFiles? ReadFiles(JobManifest job, string folder, bool interpreted, Diagnostics diagnostics)
    {
        var files = new ChannelFiles();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool read = true;
        foreach (ChannelProgram channel in job.Channels)
        {
            string file = Path.Combine(folder, channel.File);
            if (!seen.Add(file))
            {
                continue;
            }

            string subject = string.Create(CultureInfo.InvariantCulture,
                $"The file of channel {channel.Id} of the job");
            string? text = InputFile.Read(file, DiagnosticCodes.ChannelFileUnreadable, subject, "machine-config 8, D97",
                diagnostics);
            if (text is null)
            {
                read = false;
                continue;
            }

            files.Texts[file] = text;
            if (!interpreted || Pipeline.OwnVarsFile(file) is not string varsFile)
            {
                continue;
            }

            string? varsText = InputFile.Read(varsFile, DiagnosticCodes.VarsFileUnreadable, "The vars file",
                "virtual machine 2.7, 3.6; machine-config 8", diagnostics);
            if (varsText is null)
            {
                read = false;
                continue;
            }

            files.VarsFiles[file] = varsFile;
            files.VarsTexts[file] = varsText;
        }

        return read ? files : null;
    }

    // The diagnostics of one file after those reported before it (D98).
    private static void AddAll(Diagnostics diagnostics, Diagnostics fileDiagnostics)
    {
        foreach (Diagnostic diagnostic in fileDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }
    }
}
