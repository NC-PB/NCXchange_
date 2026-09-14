using System.CommandLine;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx check &lt;file&gt; [--machine &lt;toml&gt;] [--strict] [--skip-blocks none|all|1,3] [--expand-cycles]: the
/// static pass of the virtual machine without a target (D91): parse, expand, run STATIC and report what the stages
/// find on the standard error (D98); nothing else is written. Without --machine the file is checked against the
/// built-in default machine (D103). ncx check --job &lt;name.ncxjob.toml&gt; checks the files of a job, whose channel
/// programs run STATIC as one job in rounds (virtual machine 3.7; machine-config 8). Exit code 0 without an ERROR, 1
/// with one or with a WARNING under --strict, 2 when an input cannot be read (D97; architecture 10).
/// </summary>
internal static class CheckCommand
{
    /// <summary>
    /// The check command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(TextWriter error)
    {
        var command = new Command(
            "check",
            "Check an NCX file, or the files of a job with --job: parse, expand and run the virtual machine STATIC, "
            + "and report what it finds. Without --machine against the built-in default machine, or a job's machine.");
        RunOptions options = RunOptions.AddTo(command);
        Option<string> jobOption = JobOption.AddTo(command, options.File);
        command.SetAction(parseResult => parseResult.GetValue(jobOption) is string job
            ? RunJob(job, options.Read(parseResult), error)
            : Run(options.Read(parseResult), error));
        return command;
    }

    /// <summary>
    /// Checks one file (virtual machine 1, 5).
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, TextWriter error)
    {
        // The output of check is the diagnostics only (architecture 10), in the order the stages reported them (D98).
        PipelineRun run = Pipeline.Run(settings, []);
        error.Write(run.Diagnostics.ToText());
        return run.ExitCode(settings.Strict);
    }

    /// <summary>
    /// Checks the files of a job: the channel programs run STATIC as one job in rounds, with the deadlock and the
    /// shared resources of the job (virtual machine 3.7), and the rest of every file is walked as check walks one file
    /// (virtual machine 1, 3.9).
    /// </summary>
    /// <param name="jobFile">The job manifest of --job.</param>
    /// <param name="settings">The machine and the shared options.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int RunJob(string jobFile, RunSettings settings, TextWriter error)
    {
        JobPipelineRun run = JobPipeline.Run(jobFile, settings with { Interpreted = false }, (_, _, _) => []);
        error.Write(run.Diagnostics.ToText());
        return run.ExitCode(settings.Strict);
    }
}
