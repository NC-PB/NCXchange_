using System.CommandLine;

namespace Ncx.Cli.Commands;

/// <summary>
/// --job &lt;name.ncxjob.toml&gt; of ncx check, ncx analyze and ncx compile: a job manifest in place of the file,
/// whose channel programs run as one job in rounds, or are compiled by the job compiler, on the machine the manifest
/// names unless --machine names another (machine-config 8; virtual machine 3.7; architecture 10, F24). The file and
/// --job exclude each other, and one of them is required.
/// </summary>
internal static class JobOption
{
    /// <summary>
    /// Adds --job to a command and makes its file argument optional beside it.
    /// </summary>
    /// <param name="command">check, analyze or compile.</param>
    /// <param name="file">The file argument of the command.</param>
    /// <returns>The option, to read from the parse result.</returns>
    public static Option<string> AddTo(Command command, Argument<string> file)
    {
        var job = new Option<string>("--job")
        {
            Description = "A job manifest, <name>.ncxjob.toml, in place of the file: the programs of its channels as "
                + "one job, on the machine the manifest names unless --machine names another.",
            HelpName = "ncxjob.toml",
        };
        file.Arity = ArgumentArity.ZeroOrOne;
        command.Add(job);

        // A file or --job, not both and not neither: anything else is a usage error, exit code 2 before the run starts
        // (D97, architecture 10).
        command.Validators.Add(result =>
        {
            bool hasFile = result.GetValue(file) is not null;
            bool hasJob = result.GetValue(job) is not null;
            if (hasFile && hasJob)
            {
                result.AddError($"ncx {command.Name} takes a file or --job, not both (architecture 10)");
            }
            else if (!hasFile && !hasJob)
            {
                result.AddError($"ncx {command.Name} needs a file or --job (architecture 10)");
            }
        });
        return job;
    }
}
