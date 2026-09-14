using System.CommandLine;
using Ncx.Analytics;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx analyze &lt;file&gt; [--machine &lt;toml&gt;] [--vars &lt;toml&gt;] [--from &lt;line&gt;] [--to &lt;line&gt;]
/// [--analytic tools,runtime] [--static] [--format text|csv] [--skip-blocks none|all|1,3] [--strict]: parse, expand,
/// run the virtual machine INTERPRETED, or STATIC under --static, with the analytics subscribed, and write their
/// reports over the block range on the standard output, the diagnostics on the standard error (architecture 10;
/// virtual machine 1, 8; D67; implementation 14, P4-02). Without --machine the built-in default machine (D103). With
/// --job &lt;name.ncxjob.toml&gt; in place of the file the channel programs of a job run as one job (AnalyzeJob). Exit
/// codes of D97, as ncx check.
/// </summary>
internal static class AnalyzeCommand
{
    private const string TextFormat = "text";
    private const string CsvFormat = "csv";
    private const char NameSeparator = ',';

    // The name a report gives the built-in default machine (D103).
    private const string DefaultMachineName = "the built-in default machine";

    /// <summary>
    /// The analyze command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="analytics">The analytics by name, registered in Program.cs.</param>
    /// <param name="output">The standard output, for the reports.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(AnalyticsRegistry analytics, TextWriter output, TextWriter error)
    {
        var fileArgument = new Argument<string>("file") { Description = "The NCX file." };
        var machineOption = new Option<string>("--machine")
        {
            Description = "The machine file to run against, by name in machines/ or by path. Without it: the machine "
                + "that ncx.toml names, else the built-in default machine.",
            HelpName = "toml",
        };
        var varsOption = new Option<string>("--vars")
        {
            Description = "The start values of the variables. Without it: <file>.vars.toml next to the file, when "
                + "there is one.",
            HelpName = "toml",
        };
        var fromOption = new Option<int?>("--from")
        {
            Description = "The first NCX line of the block range. Without it: the start of the file.",
            HelpName = "line",
        };
        var toOption = new Option<int?>("--to")
        {
            Description = "The last NCX line of the block range. Without it: the end of the file.",
            HelpName = "line",
        };
        var analyticOption = new Option<string>("--analytic")
        {
            Description = $"The analytics to run, separated by commas: {string.Join(NameSeparator, analytics.Names)}. "
                + "Without it: all of them.",
            HelpName = "names",
        };
        var staticOption = new Option<bool>("--static")
        {
            Description = "Run the virtual machine STATIC, the one-pass form, instead of INTERPRETED.",
        };
        var formatOption = new Option<string>("--format")
        {
            Description = "text: aligned columns, the default; csv: comma-separated values.",
            HelpName = "text|csv",
            DefaultValueFactory = _ => TextFormat,
        };
        formatOption.AcceptOnlyFromAmong(TextFormat, CsvFormat);

        // --skip-blocks and --strict as check has them (D53, D97).
        Option<SkipBlocks> skipBlocksOption = RunOptions.SkipBlocksOption();
        Option<bool> strictOption = RunOptions.StrictOption();
        var command = new Command(
            "analyze",
            "Run an NCX file INTERPRETED and write the tool list, the runtime estimate, the segment length and the "
            + "tool vector change over a block range. Without --machine against the built-in default machine.")
        {
            fileArgument,
            machineOption,
            varsOption,
            fromOption,
            toOption,
            analyticOption,
            staticOption,
            formatOption,
            skipBlocksOption,
            strictOption,
        };

        // --job runs the channel programs of a job manifest in place of the file (machine-config 8; virtual machine
        // 3.7).
        Option<string> jobOption = JobOption.AddTo(command, fileArgument);

        // The block range is NCX line numbers of the file from --from to --to (virtual machine 8, D67); the vars file
        // gives the start values of an INTERPRETED run (virtual machine 3.6); --analytic names registered analytics.
        // Anything else is a usage error, exit code 2 before the run starts (D97).
        command.Validators.Add(result =>
        {
            // The files of a job take their start values from <file>.vars.toml next to each (virtual machine 3.6,
            // machine-config 8); --vars names those of one file.
            if (result.GetValue(varsOption) is not null && result.GetValue(jobOption) is not null)
            {
                result.AddError("--vars gives the start values of one file and does not go with --job, whose files "
                    + "take theirs from <file>.vars.toml next to each (virtual machine 3.6, machine-config 8)");
            }

            int? from = result.GetValue(fromOption);
            int? to = result.GetValue(toOption);
            if (from is < 1 || to is < 1)
            {
                result.AddError("--from and --to are NCX line numbers of the file, 1 or more (virtual machine 8, D67)");
            }
            else if (from is int first && to is int last && first > last)
            {
                result.AddError($"--from {first} lies after --to {last}; the block range runs from --from to --to "
                    + "(virtual machine 8, D67)");
            }

            if (result.GetValue(varsOption) is not null && result.GetValue(staticOption))
            {
                result.AddError("--vars gives the start values of an INTERPRETED run and does not go with --static "
                    + "(virtual machine 3.6)");
            }

            if (UnknownAnalytic(result.GetValue(analyticOption), analytics) is string unknown)
            {
                result.AddError($"--analytic names {unknown}, which is no analytic of ncx analyze; the analytics are "
                    + $"{string.Join(", ", analytics.Names)} (architecture 9)");
            }
        });

        command.SetAction(parseResult =>
        {
            var settings = new RunSettings
            {
                File = parseResult.GetValue(fileArgument) ?? "",
                MachineFile = parseResult.GetValue(machineOption),
                Strict = parseResult.GetValue(strictOption),
                SkipBlocks = parseResult.GetValue(skipBlocksOption) ?? SkipBlocks.None,
                Interpreted = !parseResult.GetValue(staticOption),
                VarsFile = parseResult.GetValue(varsOption),
            };
            var analyze = new AnalyzeSettings
            {
                Analytics = NamesOf(parseResult.GetValue(analyticOption), analytics),
                Range = new BlockRange { From = parseResult.GetValue(fromOption), To = parseResult.GetValue(toOption) },
                Format = parseResult.GetValue(formatOption) == CsvFormat ? TableFormat.Csv : TableFormat.Text,
            };
            return parseResult.GetValue(jobOption) is string job
                ? AnalyzeJob.Run(job, settings, analyze, analytics, output, error)
                : Run(settings, analyze, analytics, output, error);
        });
        return command;
    }

    /// <summary>
    /// Analyzes one file.
    /// </summary>
    /// <param name="settings">The file, the machine and the options of the run.</param>
    /// <param name="analyze">The analytics, the block range and the format.</param>
    /// <param name="analytics">The analytics by name.</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, AnalyzeSettings analyze, AnalyticsRegistry analytics,
        TextWriter output, TextWriter error)
    {
        // analyze runs INTERPRETED by default, STATIC under --static (virtual machine 1; implementation 14, P4-02), and
        // always with ExpandCycles, the VM option for analytics that raises the motions of a cycle call, whose plunges
        // the runtime estimate times and the tool list counts (D37; virtual machine 3.3, 8).
        RunSettings run = settings with { ExpandCycles = true };
        var subscribed = new List<IAnalytic>();
        PipelineRun result = Pipeline.Run(run, (machine, machineFile) =>
        {
            var options = new AnalyticOptions
            {
                Machine = machine,
                MachineName = machineFile is null ? DefaultMachineName : Path.GetFileName(machineFile),
                FileName = Path.GetFileName(settings.File),
                Range = analyze.Range,
                Format = analyze.Format,
                Mode = run.Interpreted ? ExecutionMode.Interpreted : ExecutionMode.Static,
            };
            foreach (string name in analyze.Analytics)
            {
                if (analytics.Create(name, options) is IAnalytic analytic)
                {
                    subscribed.Add(analytic);
                }
            }

            return subscribed;
        });

        // The reports cover the blocks the run executed; there are none when there was no program to run.
        // TODO(question): virtual machine 8 does not say what analyze writes when an ERROR stops the run, the question
        // wave-2 question #42 asks for trace and annotate. The reports cover the blocks executed before the ERROR,
        // after a line that says so, and nothing is written when the file, the machine file, the parser or the
        // expander stopped the run before its first block, as trace does, until that is answered.
        if (result.Program is not null)
        {
            if (result.Diagnostics.HasErrors)
            {
                output.Write(TextTable.Line("The run stopped at an ERROR; the reports cover the blocks executed before "
                    + "it (see the diagnostics).", analyze.Format));
                output.Write('\n');
            }

            for (int index = 0; index < subscribed.Count; index++)
            {
                output.Write(index == 0 ? "" : "\n");
                output.Write(subscribed[index].Report());
            }
        }

        error.Write(result.Diagnostics.ToText());
        return result.ExitCode(settings.Strict);
    }

    // The analytics --analytic names, separated by commas, each once in the order written; all registered analytics in
    // the order of their registration without it (architecture 9).
    private static List<string> NamesOf(string? value, AnalyticsRegistry analytics)
    {
        var names = new List<string>();
        if (value is null)
        {
            names.AddRange(analytics.Names);
            return names;
        }

        foreach (string part in value.Split(NameSeparator))
        {
            string name = part.Trim();
            if (name.Length > 0 && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // The first name of --analytic that no analytic is registered under, or the empty value when it names none; null
    // when every name is known.
    private static string? UnknownAnalytic(string? value, AnalyticsRegistry analytics)
    {
        if (value is null)
        {
            return null;
        }

        List<string> names = NamesOf(value, analytics);
        if (names.Count == 0)
        {
            return "no analytic";
        }

        foreach (string name in names)
        {
            if (!analytics.Names.Contains(name))
            {
                return name;
            }
        }

        return null;
    }
}
