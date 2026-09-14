namespace Ncx.Cli;

// The codes of ncx check --job and ncx analyze --job, CLI500-CLI549 (P6-01): the job manifest and the files of its
// channels, which a job reads before it runs.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI500: the job manifest of --job cannot be read as UTF-8 text; decided before the run starts, exit code 2 (D97;
    /// machine-config 8; architecture 10).
    /// </summary>
    public const string JobManifestUnreadable = "CLI500";

    /// <summary>
    /// CLI501: the file of a [[channel]] of the job manifest cannot be read as UTF-8 text; decided before the run
    /// starts, exit code 2 (D97; machine-config 8; language 4.8).
    /// </summary>
    public const string ChannelFileUnreadable = "CLI501";
}
