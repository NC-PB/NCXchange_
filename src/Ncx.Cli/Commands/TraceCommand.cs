using System.CommandLine;
using Ncx.Cli.History;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx trace &lt;file&gt; [--format text|csv] [--interpreted [--vars &lt;toml&gt;]] and the options of check: one row
/// per changed state variable per executed block, channel, block, variable, old, new, on the standard output (virtual
/// machine 6), from a STATIC run or, under --interpreted, an INTERPRETED one (virtual machine 1, 3.6); the diagnostics
/// on the standard error (D98) and the exit codes of D97, as ncx check has them (architecture 10).
/// </summary>
internal static class TraceCommand
{
    private const string TextFormat = "text";
    private const string CsvFormat = "csv";

    /// <summary>
    /// The trace command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="output">The standard output, for the table.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(TextWriter output, TextWriter error)
    {
        var command = new Command(
            "trace",
            "Write one row per state variable that an executed block changed: channel, block, variable, old, new. "
            + "STATIC, or INTERPRETED under --interpreted.");
        RunOptions options = RunOptions.AddTo(command);

        // Plain text, CSV or aligned columns (virtual machine 8).
        var formatOption = new Option<string>("--format")
        {
            Description = "text: aligned columns, the default; csv: comma-separated values.",
            HelpName = "text|csv",
            DefaultValueFactory = _ => TextFormat,
        };
        formatOption.AcceptOnlyFromAmong(TextFormat, CsvFormat);
        command.Add(formatOption);

        // INTERPRETED mode executes the program with its flow and evaluates its expressions, starting the variables
        // from the vars file (virtual machine 1, 2.7, 3.6; machine-config 8).
        var interpretedOption = new Option<bool>("--interpreted")
        {
            Description =
                "Run the virtual machine INTERPRETED: variables evaluated, jumps, repeats and calls followed.",
        };
        var varsOption = new Option<string>("--vars")
        {
            Description = "The start values of the variables of an INTERPRETED run. Without it: <file>.vars.toml next "
                + "to the file, when there is one.",
            HelpName = "toml",
        };
        command.Add(interpretedOption);
        command.Add(varsOption);

        // The start values are those of an INTERPRETED run (virtual machine 3.6), so --vars needs --interpreted.
        command.Validators.Add(result =>
        {
            if (result.GetValue(varsOption) is not null && !result.GetValue(interpretedOption))
            {
                result.AddError("--vars gives the start values of an INTERPRETED run and needs --interpreted (virtual "
                    + "machine 3.6)");
            }
        });

        command.SetAction(parseResult => Run(
            options.Read(parseResult) with
            {
                Interpreted = parseResult.GetValue(interpretedOption),
                VarsFile = parseResult.GetValue(varsOption),
            },
            parseResult.GetValue(formatOption) == CsvFormat ? TraceFormat.Csv : TraceFormat.Text,
            output,
            error));
        return command;
    }

    /// <summary>
    /// Traces one file.
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options.</param>
    /// <param name="format">CSV or aligned text.</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, TraceFormat format, TextWriter output, TextWriter error)
    {
        var trace = new TraceListener();
        PipelineRun run = Pipeline.Run(settings, [trace]);

        // The table holds the rows of every block the run executed (virtual machine 6); there is none when there was no
        // program to run.
        // TODO(question): virtual machine 6 does not say what trace writes when an ERROR stops the run; it writes the
        // rows of the blocks executed before the ERROR, and nothing when the file, the machine file, the parser or the
        // expander stopped the run before its first block, until that is answered.
        if (run.Program is not null)
        {
            output.Write(TraceTable.Write(trace.Rows, format));
        }

        error.Write(run.Diagnostics.ToText());
        return run.ExitCode(settings.Strict);
    }
}
