using System.CommandLine;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx check &lt;file&gt; [--machine &lt;toml&gt;] [--strict] [--skip-blocks none|all|1,3] [--expand-cycles]: the
/// static pass of the virtual machine without a target (D91): parse, expand, run STATIC and report what the stages
/// find on the standard error (D98); nothing else is written. Without --machine the file is checked against the
/// built-in default machine (D103). Exit code 0 without an ERROR, 1 with one or with a WARNING under --strict, 2 when
/// the file or the machine file cannot be read (D97; architecture 10).
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
            "Check an NCX file: parse, expand and run the virtual machine STATIC, and report what it finds. Without "
            + "--machine against the built-in default machine.");
        RunOptions options = RunOptions.AddTo(command);
        command.SetAction(parseResult => Run(options.Read(parseResult), error));
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
}
