namespace Ncx.Cli;

// The codes of check, trace and annotate, CLI100-CLI199 (P1-07): the pipeline that the three commands share, the
// machine file of --machine that it reads, and the vars file of an INTERPRETED run (P4-01).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI100: the machine file that --machine names cannot be found or read as UTF-8 text; decided before the run
    /// starts, exit code 2 (D97, D103; architecture 10).
    /// </summary>
    public const string MachineFileUnreadable = "CLI100";

    /// <summary>
    /// CLI101: the vars file of an INTERPRETED run, the one --vars names or &lt;file&gt;.vars.toml next to the file,
    /// cannot be found or read as UTF-8 text; decided before the run starts, exit code 2 (D97; virtual machine 2.7, 3.6;
    /// machine-config 8).
    /// </summary>
    public const string VarsFileUnreadable = "CLI101";
}
