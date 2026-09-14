using System.Globalization;
using Ncx.Analytics;
using Ncx.Core.Jobs;
using Ncx.Core.VirtualMachine;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx analyze --job &lt;name.ncxjob.toml&gt;: the channel programs of a job run as one job in rounds, INTERPRETED or
/// STATIC under --static (virtual machine 3.7), every channel with the analytics subscribed over the block range of its
/// own file (virtual machine 8, D67), and the reports of every channel written in the order of the manifest
/// (architecture 10; implementation 16, P6-01). The exit codes of D97, as ncx analyze.
/// </summary>
internal static class AnalyzeJob
{
    // The name a report gives the built-in default machine (D103), which a job with a machine never runs on.
    private const string DefaultMachineName = "the built-in default machine";

    /// <summary>
    /// Analyzes a job.
    /// </summary>
    /// <param name="jobFile">The job manifest of --job.</param>
    /// <param name="settings">The machine and the options of the run.</param>
    /// <param name="analyze">The analytics, the block range and the format.</param>
    /// <param name="analytics">The analytics by name.</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string jobFile, RunSettings settings, AnalyzeSettings analyze, AnalyticsRegistry analytics,
        TextWriter output, TextWriter error)
    {
        // A job runs INTERPRETED by default, STATIC under --static, and always with ExpandCycles, as one file does
        // (D37; virtual machine 1, 3.3, 8).
        RunSettings run = settings with { ExpandCycles = true };
        var channels = new List<ChannelRun>();
        var reports = new List<List<IAnalytic>>();
        JobPipelineRun result = JobPipeline.Run(jobFile, run, (machine, machineFile, channel) =>
        {
            var options = new AnalyticOptions
            {
                Machine = machine,
                MachineName = machineFile is null ? DefaultMachineName : Path.GetFileName(machineFile),
                FileName = Path.GetFileName(channel.Program.FileName),
                Range = analyze.Range,
                Format = analyze.Format,
                Mode = run.Interpreted ? ExecutionMode.Interpreted : ExecutionMode.Static,
            };
            var subscribed = new List<IAnalytic>();
            foreach (string name in analyze.Analytics)
            {
                if (analytics.Create(name, options) is IAnalytic analytic)
                {
                    subscribed.Add(analytic);
                }
            }

            channels.Add(channel);
            reports.Add(subscribed);
            return subscribed;
        });

        // The reports cover the blocks the channels executed, after a line that says so when an ERROR stopped the job,
        // and nothing is written when the job stopped before its first round, as for one file (the TODO(question) of
        // AnalyzeCommand.Run, wave-2 question #42).
        // TODO(question): virtual machine 8 and architecture 10 do not say how ncx analyze --job lays out the reports
        // of several channels; the reports of each channel follow a line that names the channel, its file and the
        // program the manifest names, channel by channel in the order of the manifest, until that is answered.
        if (result.Result is JobResult { Rounds: > 0 } job)
        {
            if (job.Stopped)
            {
                output.Write(TextTable.Line("The run stopped at an ERROR; the reports cover the blocks executed before "
                    + "it (see the diagnostics).", analyze.Format));
                output.Write('\n');
            }

            for (int index = 0; index < channels.Count; index++)
            {
                output.Write(index == 0 ? "" : "\n");
                output.Write(TextTable.Line(Heading(channels[index]), analyze.Format));
                foreach (IAnalytic analytic in reports[index])
                {
                    output.Write('\n');
                    output.Write(analytic.Report());
                }
            }
        }

        error.Write(result.Diagnostics.ToText());
        return result.ExitCode(settings.Strict);
    }

    // The line above the reports of a channel: "Channel 2: shaft.ncx, program SHAFT_CH2".
    private static string Heading(ChannelRun channel)
    {
        string heading = string.Create(CultureInfo.InvariantCulture,
            $"Channel {channel.Channel}: {Path.GetFileName(channel.Program.FileName)}");
        return channel.ProgramName is string program ? heading + ", program " + program : heading;
    }
}
