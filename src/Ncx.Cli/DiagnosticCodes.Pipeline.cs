namespace Ncx.Cli;

// The codes of check, trace and annotate, CLI100-CLI199 (P1-07): the pipeline that the three commands share and the
// machine file of --machine that it reads.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI100: the machine file that --machine names cannot be found or read as UTF-8 text; decided before the run
    /// starts, exit code 2 (D97, D103; architecture 10).
    /// </summary>
    public const string MachineFileUnreadable = "CLI100";
}
