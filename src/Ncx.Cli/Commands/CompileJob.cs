using Ncx.Compilers;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Plugins;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx compile --job &lt;name.ncxjob.toml&gt; [--machine &lt;toml&gt;] [--output &lt;folder&gt;] [--strict]: the
/// channel programs of a job manifest compiled by the job compiler with the compiler of the machine's controller, one
/// NC file per channel into out/&lt;machine&gt;/ or the folder of --output, on the machine the manifest names unless
/// --machine names another (architecture 10; machine-config 8; implementation 16, P6-02). The exit codes of D97, as
/// ncx compile.
/// </summary>
internal static class CompileJob
{
    /// <summary>
    /// Compiles a job.
    /// </summary>
    /// <param name="jobFile">The job manifest of --job; the diagnostics about the job carry this name (D98).</param>
    /// <param name="settings">The machine, the working directory and --strict.</param>
    /// <param name="outputFolder">The folder of --output; null for out/&lt;machine&gt;/.</param>
    /// <param name="compilers">The compilers by controller family.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string jobFile, RunSettings settings, string? outputFolder, CompilerRegistry compilers,
        TextWriter error)
    {
        var diagnostics = new Diagnostics(jobFile);

        // Code 2 is decided before the run starts and takes precedence: the job manifest, ncx.toml, the machine file
        // and the file of every channel are read first, all of them, so that all are reported (D97, architecture 10).
        string? manifestText = InputFile.Read(jobFile, DiagnosticCodes.JobManifestUnreadable, "The job manifest",
            "machine-config 8, D97", diagnostics);
        if (manifestText is null)
        {
            return Written(diagnostics, ExitCodes.NotStarted, error);
        }

        // A manifest that loads with an ERROR stops the job before it starts, with exit code 1 (D97, P2-01).
        var manifestDiagnostics = new Diagnostics(jobFile);
        JobManifest? job = JobManifestLoader.LoadText(Pipeline.WithoutByteOrderMark(manifestText), manifestDiagnostics);
        AddAll(diagnostics, manifestDiagnostics);
        if (job is null)
        {
            return Written(diagnostics, ExitCodes.Error, error);
        }

        // The machine of the job is the machine its manifest names, unless --machine names another (architecture 10;
        // implementation 16, P6-01).
        string folder = Path.GetDirectoryName(jobFile) ?? "";
        RunSettings run = settings.MachineFile is null
            ? settings with
            {
                MachineFile = JobPipeline.MachineOf(job, folder, settings.WorkingDirectory),
                MachineOfJob = jobFile,
            }
            : settings;
        RunMachine runMachine = RunMachine.Select(run, diagnostics);
        ChannelFiles? files = JobPipeline.ReadFiles(job, folder, interpreted: false, diagnostics);
        if (files is null || !runMachine.InputsRead)
        {
            return Written(diagnostics, ExitCodes.NotStarted, error);
        }

        // A machine file that loads with an ERROR stops the job, and so does a controller that ncx has no compiler for
        // (D97; architecture 8).
        if (runMachine.Machine is not MachineConfig machine
            || CompileCommand.CompilerOf(machine, compilers, diagnostics) is not ICompiler compiler)
        {
            return Written(diagnostics, ExitCodes.Error, error);
        }

        // The tool table of the machine (D10), and the plugins of the working directory, as for one file (P7-01).
        CompileOptions? options = CompileCommand.OptionsWithToolTable(machine, runMachine.MachineFile, diagnostics);
        if (options is null)
        {
            return Written(diagnostics, ExitCodes.NotStarted, error);
        }

        if (diagnostics.HasErrors)
        {
            return Written(diagnostics, ExitCodes.Error, error);
        }

        PluginSet plugins = RunPlugins.Load(run, runMachine.Project, diagnostics);
        options = options with
        {
            Rewriters = plugins.Rewriters,
            Listeners = plugins.Listeners,
            BlockWriters = plugins.BlockWriters,
        };

        // Every file parsed once, then the job compiler over the channel programs (architecture 8, 10).
        CompileResult result = new JobCompiler(compiler).Compile(jobFile, job, Parsed(job, folder, files), machine,
            options);
        AddAll(diagnostics, result.Diagnostics);

        // One file per channel into out/<machine>/ or the folder of --output; none after an ERROR (D205, as for one
        // file; the TODO(question)s of CompileCommand.Run).
        if (!diagnostics.HasErrors)
        {
            CompileCommand.WriteFiles(result.Files, CompileCommand.OutputFolderOf(settings, outputFolder, runMachine),
                diagnostics);
        }

        return Written(diagnostics, ExitCodes.OfRun(diagnostics, settings.Strict), error);
    }

    // The file of every [[channel]], parsed once, by the file value of the manifest; the diagnostics name the file as
    // the job reads it, next to the manifest (language 4.8).
    private static Dictionary<string, NcxProgram> Parsed(JobManifest job, string folder, ChannelFiles files)
    {
        var parsed = new Dictionary<string, NcxProgram>(StringComparer.Ordinal);
        foreach (ChannelProgram channel in job.Channels)
        {
            string file = Path.Combine(folder, channel.File);
            if (!parsed.ContainsKey(channel.File))
            {
                parsed[channel.File] = Parser.Parse(Pipeline.WithoutByteOrderMark(files.Texts[file]), file,
                    new ParserOptions());
            }
        }

        return parsed;
    }

    private static int Written(Diagnostics diagnostics, int exitCode, TextWriter error)
    {
        error.Write(diagnostics.ToText());
        return exitCode;
    }

    private static void AddAll(Diagnostics diagnostics, Diagnostics from)
    {
        foreach (Diagnostic diagnostic in from.Items)
        {
            diagnostics.Add(diagnostic);
        }
    }
}
